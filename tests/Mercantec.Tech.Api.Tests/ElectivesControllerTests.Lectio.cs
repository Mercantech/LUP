using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Api.Integrations.Lectio;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class ElectivesControllerTests
{
    [Fact]
    public async Task TeacherCanFindAndNameReservedSeatsWithoutDoubleCounting()
    {
        using var http = new HttpClient(new ElectiveDirectoryStub()) { BaseAddress = new("https://lectio.example/api/") };
        var collector = new LectioCollectorClient(http, NullLogger<LectioCollectorClient>.Instance);
        var teacher = As(Teacher, true);
        teacher.Request.Headers.Authorization = "Bearer test-teacher";

        var item = Read<ElectiveView>(await teacher.Create(Request() with { Capacity = 2, ReservedSeats = 1, Status = "closed" }, default));
        var found = Read<ElectiveLectioStudent[]>(await teacher.SearchStudents("2p", collector, default));
        Assert.Equal(["101", "102"], found.Select(s => s.LectioId));
        Assert.Empty(Read<ElectiveLectioStudent[]>(await teacher.SearchStudents("A", collector, default)));
        var student = As(Student);
        Assert.IsType<ForbidResult>(await student.SearchStudents("Ada", collector, default));
        Assert.IsType<ForbidResult>(await student.AddLectioParticipant(item.Id, "101", new(true), collector, default));
        Assert.IsType<ForbidResult>(await student.RemoveLectioParticipant(item.Id, "101", default));
        Assert.IsType<NotFoundObjectResult>(await teacher.AddLectioParticipant(item.Id, "unknown", new(true), collector, default));

        var saved = Read<ElectiveView>(await teacher.AddLectioParticipant(item.Id, "101", new(true), collector, default));
        Assert.Equal(0, saved.ReservedSeats);
        Assert.Equal(1, saved.EnrollmentCount);
        Assert.Equal(saved, Read<ElectiveView>(await teacher.AddLectioParticipant(item.Id, "101", new(true), collector, default)));
        Assert.Equal("elective_reserved_seat_unavailable", Error(Assert.IsType<ConflictObjectResult>(
            await teacher.AddLectioParticipant(item.Id, "102", new(true), collector, default))));
        var ada = Assert.Single(Read<ElectiveParticipant[]>(await teacher.Participants(item.Id, default)));
        Assert.Equal("Ada", ada.DisplayName);
        Assert.Null(ada.UserSub);
        Assert.NotEqual(Guid.Empty, ada.Id);
        var full = Read<ElectiveView>(await teacher.AddLectioParticipant(item.Id, "102", new(false), collector, default));
        Assert.Equal(2, full.EnrollmentCount);
        Assert.True(Assert.Single(Read<ElectiveWorkspace>(await student.List(default)).Electives).IsEnrolled);
        Assert.Equal("elective_full", Error(Assert.IsType<ConflictObjectResult>(
            await teacher.AddLectioParticipant(item.Id, "103", new(false), collector, default))));
        Assert.Equal(1, Read<ElectiveView>(await teacher.RemoveLectioParticipant(item.Id, "101", default)).EnrollmentCount);
        Assert.Equal(1, Read<ElectiveView>(await teacher.RemoveLectioParticipant(item.Id, "101", default)).EnrollmentCount);
        Assert.Equal("102", Assert.Single(Read<ElectiveParticipant[]>(await teacher.Participants(item.Id, default))).LectioId);
    }

    [Fact]
    public async Task AddingAlreadyEnrolledAccountOnFullOfferingKeepsOriginalRegistration()
    {
        using var http = new HttpClient(new ElectiveDirectoryStub()) { BaseAddress = new("https://lectio.example/api/") };
        var collector = new LectioCollectorClient(http, NullLogger<LectioCollectorClient>.Instance);
        var teacher = As(Teacher, true);
        teacher.Request.Headers.Authorization = "Bearer test-teacher";
        var item = Read<ElectiveView>(await teacher.Create(Request() with { Capacity = 1 }, default));
        var student = As(Student);
        await student.Enroll(item.Id, new(true), default);
        var before = await _db.ElectiveEnrollments.AsNoTracking().SingleAsync();
        var result = Read<ElectiveView>(await teacher.AddLectioParticipant(item.Id, "102", new(false), collector, default));
        Assert.Equal(1, result.EnrollmentCount);
        var person = Assert.Single(Read<ElectiveParticipant[]>(await teacher.Participants(item.Id, default)));
        Assert.Equal(before.Id, person.Id);
        Assert.Equal("102", person.LectioId);
        Assert.Equal(Student, person.UserSub);
        Assert.Equal(0, Read<ElectiveView>(await student.Withdraw(item.Id, default)).EnrollmentCount);

        var template = Read<ElectiveView>(await teacher.Create(Request() with { Status = "template" }, default));
        Assert.IsType<BadRequestObjectResult>(await teacher.AddLectioParticipant(template.Id, "101", new(false), collector, default));
    }

    private sealed class ElectiveDirectoryStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("test-teacher", request.Headers.Authorization?.Parameter);
            object result = request.RequestUri!.AbsolutePath switch
            {
                "/api/programmeringselever" => new { students = new[]
                {
                    new { lectioId = "101", name = "Ada", className = "H2p" },
                    new { lectioId = "102", name = "Bo", className = "H2p" },
                    new { lectioId = "103", name = "Kim", className = "H3" }
                } },
                "/api/auth-links/resolve" => new { links = new[] { new { lectioId = "102", authSub = Student } } },
                _ => throw new InvalidOperationException("Unexpected Collector endpoint")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) });
        }
    }
}
