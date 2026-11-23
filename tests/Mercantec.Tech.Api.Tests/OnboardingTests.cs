using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class OnboardingTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = OpenDatabase();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Theory]
    [InlineData("Student", "Mine handlinger")]
    [InlineData("Teacher", "Til opfølgning")]
    public async Task Seeded_welcome_and_dismissal_follow_the_account(string role, string expectedText)
    {
        var account = Guid.NewGuid();
        await using (var db = OpenDatabase())
        {
            var firstSession = For(db, account, role);
            var step = Assert.Single((await firstSession.Get(default)).Value!);
            Assert.Equal("Velkommen til LUP", step.Title);
            Assert.Contains(expectedText, step.Text);
            Assert.IsType<NoContentResult>(await firstSession.Dismiss(new(account), default));
            Assert.IsType<NoContentResult>(await firstSession.Dismiss(new(account), default));
        }

        await using var nextDatabase = OpenDatabase();
        Assert.Empty((await For(nextDatabase, account, role).Get(default)).Value!);
        Assert.Empty((await For(nextDatabase, account, "Admin").Get(default)).Value!);
        Assert.Single((await For(nextDatabase, Guid.NewGuid(), role).Get(default)).Value!);
        Assert.Equal(1, await nextDatabase.OnboardingDismissals.CountAsync());
    }

    [Fact]
    public async Task Steps_use_configured_order_and_a_changed_account_cannot_be_dismissed()
    {
        await using var db = OpenDatabase();
        db.OnboardingSteps.Add(new OnboardingStep
        {
            Audience = OnboardingStep.Student,
            Title = "Find rundt i LUP",
            Text = "Læs derefter velkomsten.",
            SortOrder = 0
        });
        await db.SaveChangesAsync();

        var student = For(db, Guid.NewGuid(), "Student");
        Assert.IsType<ConflictResult>(await student.Dismiss(new(Guid.NewGuid()), default));
        Assert.Equal(new[] { "Find rundt i LUP", "Velkommen til LUP" },
            (await student.Get(default)).Value!.Select(step => step.Title));
        Assert.Empty(await db.OnboardingDismissals.ToListAsync());
    }

    private AppDbContext OpenDatabase() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private static OnboardingController For(AppDbContext db, Guid subject, string role) => new(db)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", subject.ToString()), new("role", role)], "test"))
            }
        }
    };
}
