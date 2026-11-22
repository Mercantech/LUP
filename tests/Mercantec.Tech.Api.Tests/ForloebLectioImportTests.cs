using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.Lectio;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mercantec.Tech.Api.Tests;

public sealed class ForloebLectioImportTests
{
    [Fact]
    public async Task Attaching_adds_a_source_to_the_existing_hold_and_skips_both_kinds_of_roster_duplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var teacher = Guid.NewGuid();
        var alreadyLinked = Guid.NewGuid();
        var newAccount = Guid.NewGuid();
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "Mit hold", CreatedBySub = teacher };
        var hold = new Hold { Id = Guid.NewGuid(), Name = course.Name, Forloeb = course, LectioHoldKey = "he:1" };
        db.Hold.Add(hold);
        db.Memberships.AddRange(
            new Membership { Id = Guid.NewGuid(), HoldId = hold.Id, LectioId = "11", DisplayName = "Manuelt navn", Source = "manual" },
            new Membership { Id = Guid.NewGuid(), HoldId = hold.Id, UserSub = alreadyLinked, Source = "manual" });
        await db.SaveChangesAsync();

        using var collector = new HttpClient(new Collector(_ => Task.FromResult<object>(new
        {
            hold = new { lectioHoldKey = "he:2", name = "Ekstra hold", members = new[]
            {
                new { studentLectioId = "11", studentName = "Samme Lectio-id" },
                new { studentLectioId = "12", studentName = "Samme konto" },
                new { studentLectioId = "13", studentName = "Ny elev" },
                new { studentLectioId = "13", studentName = "Gentaget elev" }
            }}
        }), new { links = new[] { new { lectioId = "12", authSub = alreadyLinked }, new { lectioId = "13", authSub = newAccount } } }))
        { BaseAddress = new Uri("https://collector.example/api/") };
        var request = new ForloebController.AttachLectioRequest("he:2");
        Assert.IsType<NotFoundResult>(await Controller(db, collector, Guid.NewGuid()).AttachLectio(course.Id, request, default));
        var editor = Controller(db, collector, teacher);
        var result = Assert.IsType<OkObjectResult>(await editor.AttachLectio(course.Id, request, default));
        Assert.Equal(1, JsonSerializer.SerializeToElement(result.Value).GetProperty("added").GetInt32());
        Assert.IsType<ConflictObjectResult>(await editor.AttachLectio(course.Id, request, default));
        Assert.Single(await db.Hold.ToArrayAsync());
        Assert.Equal("he:2", (await db.HoldLectioSources.SingleAsync()).LectioHoldKey);
        var roster = await db.Memberships.ToArrayAsync();
        Assert.Equal(3, roster.Length);
        Assert.Equal("Manuelt navn", Assert.Single(roster, member => member.LectioId == "11").DisplayName);
        Assert.Equal(newAccount, Assert.Single(roster, member => member.LectioId == "13").UserSub);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_import_preserves_the_winner_and_only_links_to_an_accessible_hold(bool canAccessWinner)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var teacherId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        const string holdKey = "h2-pro";

        using var collector = new HttpClient(new Collector(async ct =>
        {
            await using var otherRequest = new AppDbContext(options);
            var hold = new Hold
            {
                Id = Guid.NewGuid(),
                Name = "Programmering",
                LectioHoldKey = holdKey,
                Forloeb = new Forloeb { Id = winnerId, Name = "Programmering", CreatedBySub = canAccessWinner ? teacherId : Guid.NewGuid() }
            };
            hold.LectioSources.Add(new HoldLectioSource { Id = Guid.NewGuid(), LectioHoldKey = holdKey });
            otherRequest.Hold.Add(hold);
            await otherRequest.SaveChangesAsync(ct);
            return new
            {
                name = "Programmering",
                members = new[] { new { studentLectioId = "1", studentName = "Ada" } }
            };
        })) { BaseAddress = new Uri("https://collector.example/api/") };
        var controller = Controller(db, collector, teacherId);

        var result = await controller.CreateFromLectio(new(holdKey, null, "Andet forsøg", null), default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var json = JsonSerializer.SerializeToElement(conflict.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("already_imported", json.GetProperty("error").GetString());
        var links = json.GetProperty("conflicts").EnumerateArray();
        if (canAccessWinner)
            Assert.Equal(winnerId, Assert.Single(links).GetProperty("forloebId").GetGuid());
        else
            Assert.Empty(links);
        Assert.Equal(winnerId, (await db.Forloeb.SingleAsync()).Id);
        Assert.Single(await db.Hold.ToArrayAsync());
        Assert.Single(await db.HoldLectioSources.ToArrayAsync());
        Assert.Empty(await db.Memberships.ToArrayAsync());
    }

    [Fact]
    public async Task Import_requires_a_roster_and_deduplicates_the_collectors_canonical_hold_key()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var payloads = new Queue<object>([
            new { name = "Programmering" },
            new { lectioHoldKey = "sd:123", name = "Programmering", members = Array.Empty<object>() },
            new { lectioHoldKey = "sd:123", name = "Programmering", members = Array.Empty<object>() }
        ]);
        using var collector = new HttpClient(new Collector(_ => Task.FromResult(payloads.Dequeue())))
        {
            BaseAddress = new Uri("https://collector.example/api/")
        };
        var controller = Controller(db, collector, Guid.NewGuid());
        var request = new ForloebController.FromLectioRequest("123", null, null, null, HoldName: "H2 programmering");

        var invalidName = await controller.CreateFromLectio(request with { HoldName = new string('x', 201) }, default);
        Assert.IsType<BadRequestObjectResult>(invalidName.Result);

        var malformed = await controller.CreateFromLectio(request, default);
        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(malformed.Result).StatusCode);
        Assert.Empty(await db.Forloeb.ToArrayAsync());

        var emptyRoster = await controller.CreateFromLectio(request, default);
        Assert.IsType<CreatedAtActionResult>(emptyRoster.Result);
        Assert.Equal("sd:123", (await db.HoldLectioSources.SingleAsync()).LectioHoldKey);
        Assert.Equal("H2 programmering", (await db.Hold.SingleAsync()).Name);
        Assert.Empty(await db.Memberships.ToArrayAsync());

        var alias = await controller.CreateFromLectio(request, default);
        Assert.IsType<ConflictObjectResult>(alias.Result);
        var caseVariant = await controller.CreateFromLectio(request with { LectioHoldKey = "SD:123" }, default);
        Assert.IsType<ConflictObjectResult>(caseVariant.Result);
        Assert.Single(await db.Forloeb.ToArrayAsync());
        Assert.Empty(payloads);
    }

    private static ForloebController Controller(AppDbContext db, HttpClient collector, Guid teacherId)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", teacherId.ToString()), new Claim("role", "Teacher")], "test"))
        };
        http.Request.Headers.Authorization = "Bearer fixture-token";
        return new ForloebController(db, new LectioCollectorClient(collector, NullLogger<LectioCollectorClient>.Instance))
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private sealed class Collector(Func<CancellationToken, Task<object>> loadRoster, object? authLinks = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("fixture-token", request.Headers.Authorization?.Parameter);
            if (request.RequestUri!.AbsolutePath.StartsWith("/api/holds/by-lectio/", StringComparison.Ordinal))
            {
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(await loadRoster(ct))
                };
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(authLinks ?? new { links = Array.Empty<object>() }) };
        }
    }
}
