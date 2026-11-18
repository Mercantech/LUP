using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public class ElectiveMembershipClaimTests
{
    [Fact]
    public async Task LinkingClaimsLectioEnrollmentsAndMergesExistingSelfEnrollmentOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var sub = Guid.NewGuid();
        var anotherStudent = Guid.NewGuid();
        var newLink = Hold();
        var alreadyEnrolled = Hold();
        var otherIdentity = Hold();
        var alreadyClaimed = Hold();
        var selfRegistration = Entry(alreadyEnrolled, sub);
        db.ElectiveEnrollments.AddRange(
            Entry(newLink, lectioId: "elev-a"),
            selfRegistration, Entry(alreadyEnrolled, lectioId: "elev-a"),
            Entry(otherIdentity, sub, "elev-b"), Entry(otherIdentity, lectioId: "elev-a"),
            Entry(alreadyClaimed, anotherStudent, "elev-a"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new MembershipClaimService(db);
        await service.ClaimByLectioIdAsync(sub, "elev-a");
        await service.ClaimByLectioIdAsync(sub, "elev-a");

        var rows = await db.ElectiveEnrollments.AsNoTracking().ToListAsync();
        Assert.Equal(5, rows.Count);
        Assert.Equal(sub, Assert.Single(rows, x => x.ElectiveId == newLink.Id).UserSub);
        var merged = Assert.Single(rows, x => x.ElectiveId == alreadyEnrolled.Id);
        Assert.Equal(selfRegistration.Id, merged.Id);
        Assert.Equal(sub, merged.UserSub);
        Assert.Equal("elev-a", merged.LectioId);
        Assert.Contains(rows, x => x.ElectiveId == otherIdentity.Id && x.LectioId == "elev-a" && x.UserSub is null);
        Assert.Contains(rows, x => x.ElectiveId == otherIdentity.Id && x.LectioId == "elev-b" && x.UserSub == sub);
        Assert.Equal(anotherStudent, Assert.Single(rows, x => x.ElectiveId == alreadyClaimed.Id).UserSub);
    }

    private static Elective Hold() => new() { Id = Guid.NewGuid(), Title = "Testhold", Status = "open" };

    private static ElectiveEnrollment Entry(Elective elective, Guid? sub = null, string? lectioId = null) =>
        new() { Elective = elective, UserSub = sub, LectioId = lectioId, DisplayName = "Testelev", EnrolledAtUtc = DateTimeOffset.UtcNow };
}
