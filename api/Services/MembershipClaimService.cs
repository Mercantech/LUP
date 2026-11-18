using Mercantec.Tech.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Services;

/// <summary>Kobler shadow-medlemskaber til Auth-sub når Lectio-id claims.</summary>
public class MembershipClaimService(AppDbContext db)
{
    public async Task<int> ClaimByLectioIdAsync(Guid userSub, string lectioId, CancellationToken ct = default)
    {
        lectioId = lectioId.Trim();
        if (string.IsNullOrWhiteSpace(lectioId))
            return 0;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var shadows = await db.Memberships
            .Where(m => m.LectioId == lectioId && m.UserSub == null)
            .ToListAsync(ct);

        var claimed = 0;
        foreach (var m in shadows)
        {
            // Undgå unikheds-konflikt hvis brugeren allerede er claimed på samme hold
            var already = await db.Memberships.AnyAsync(
                x => x.HoldId == m.HoldId && x.UserSub == userSub && x.Id != m.Id, ct);
            if (already)
            {
                db.Memberships.Remove(m);
                continue;
            }

            m.UserSub = userSub;
            claimed++;
        }

        if (shadows.Count > 0)
            await db.SaveChangesAsync(ct);

        await ClaimElectiveEnrollmentsAsync(userSub, lectioId, ct);
        await transaction.CommitAsync(ct);
        return claimed;
    }

    private async Task ClaimElectiveEnrollmentsAsync(Guid userSub, string lectioId, CancellationToken ct)
    {
        var electiveIds = await db.ElectiveEnrollments
            .Where(e => e.LectioId == lectioId && e.UserSub == null)
            .Select(e => e.ElectiveId).ToListAsync(ct);
        foreach (var electiveId in electiveIds.Order())
        {
            await db.LockElectiveAsync(electiveId, ct);
            var registrations = db.ElectiveEnrollments.Where(e => e.ElectiveId == electiveId);
            var shadow = await registrations.SingleOrDefaultAsync(e => e.LectioId == lectioId && e.UserSub == null, ct);
            if (shadow is null) continue;

            var existing = await registrations.SingleOrDefaultAsync(e => e.UserSub == userSub, ct);
            if (existing is null)
            {
                shadow.UserSub = userSub;
            }
            else if (existing.LectioId is null)
            {
                // Keep the student's original enrollment. Release the unique Lectio ID before attaching it.
                db.ElectiveEnrollments.Remove(shadow);
                await db.SaveChangesAsync(ct);
                existing.LectioId = lectioId;
            }
            // An enrollment already linked to another Lectio identity must not be reassigned.
        }

        if (electiveIds.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
