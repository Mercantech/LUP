using System.Net.Http.Headers;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.Lectio;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class ElectivesController
{
    [HttpGet("lectio-students")]
    public async Task<IActionResult> SearchStudents([FromQuery] string? q,
        [FromServices] LectioCollectorClient collector, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var query = q?.Trim();
        if (query is not { Length: >= 2 and <= 200 }) return Ok(Array.Empty<ElectiveLectioStudent>());

        return await WithCollector(async bearer =>
        {
            var directory = await collector.GetProgrammeringseleverAsync(bearer, ct);
            var results = ReadStudents(directory)
                .Where(s => s.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || s.LectioId.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || s.ClassName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                .OrderByDescending(s => s.LectioId.Equals(query, StringComparison.OrdinalIgnoreCase))
                .ThenBy(s => s.DisplayName).ThenBy(s => s.LectioId).Take(30).ToArray();
            return Ok(results);
        }, ct);
    }

    [HttpPut("{id:guid}/participants/lectio/{lectioId}")]
    public async Task<IActionResult> AddLectioParticipant(Guid id, string lectioId, AddElectiveParticipant request,
        [FromServices] LectioCollectorClient collector, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!ValidLectioId(lectioId)) return BadRequest(new { error = "invalid_lectio_id" });

        return await WithCollector(async bearer =>
        {
            var directory = await collector.GetProgrammeringseleverAsync(bearer, ct);
            var student = ReadStudents(directory).FirstOrDefault(s => s.LectioId.Equals(lectioId, StringComparison.OrdinalIgnoreCase));
            if (student is null) return NotFound(new { error = "lectio_student_not_found" });
            var links = await collector.ResolveAuthLinksAsync([student.LectioId], bearer, ct);
            Guid? sub = links.TryGetValue(student.LectioId, out var linked) && linked != Guid.Empty ? linked : null;
            return await AddToRoster(id, student, sub, request.UseReservedSeat, ct);
        }, ct);
    }

    private async Task<IActionResult> AddToRoster(Guid id, ElectiveLectioStudent student, Guid? sub,
        bool useReservedSeat, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockElectiveAsync(id, ct);
        var elective = await db.Electives.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (elective is null) return NotFound();
        if (elective.Status == "template") return BadRequest(new { error = "elective_participants_require_offering" });

        var roster = db.ElectiveEnrollments.Where(e => e.ElectiveId == id);
        var existing = await roster.Where(e => e.LectioId == student.LectioId || (sub != null && e.UserSub == sub))
            .ToListAsync(ct);
        if (existing.Any(e => (e.LectioId != null && e.LectioId != student.LectioId)
            || (e.UserSub != null && sub != null && e.UserSub != sub)))
            return Conflict(new { error = "elective_identity_conflict" });

        if (existing.Count == 0)
        {
            if (useReservedSeat && elective.ReservedSeats == 0)
                return Conflict(new { error = "elective_reserved_seat_unavailable" });
            if (!useReservedSeat && elective.Capacity is int capacity && await roster.CountAsync(ct) >= capacity - elective.ReservedSeats)
                return Conflict(new { error = "elective_full" });
            if (useReservedSeat)
            {
                elective.ReservedSeats--;
                elective.Version++;
            }
            db.ElectiveEnrollments.Add(new ElectiveEnrollment
            {
                ElectiveId = id, UserSub = sub, LectioId = student.LectioId,
                DisplayName = student.DisplayName, EnrolledAtUtc = clock.GetUtcNow()
            });
        }
        else
        {
            // A student may have self-enrolled before their Lectio identity was linked.
            var target = existing.FirstOrDefault(e => e.UserSub.HasValue) ?? existing[0];
            if (existing.Count > 1)
            {
                var duplicate = existing.Single(e => e.Id != target.Id);
                target.EnrolledAtUtc = existing.Min(e => e.EnrolledAtUtc);
                db.ElectiveEnrollments.Remove(duplicate);
                await db.SaveChangesAsync(ct);
            }
            target.LectioId = student.LectioId;
            target.UserSub ??= sub;
            target.DisplayName = student.DisplayName;
        }

        await db.SaveChangesAsync(ct);
        var count = await roster.CountAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(View(elective, count, false, false, LocalDate(clock.GetUtcNow())));
    }

    [HttpDelete("{id:guid}/participants/lectio/{lectioId}")]
    public async Task<IActionResult> RemoveLectioParticipant(Guid id, string lectioId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!ValidLectioId(lectioId)) return BadRequest(new { error = "invalid_lectio_id" });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockElectiveAsync(id, ct);
        var elective = await db.Electives.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (elective is null) return NotFound();
        if (elective.Status == "template") return BadRequest(new { error = "elective_participants_require_offering" });

        var roster = db.ElectiveEnrollments.Where(e => e.ElectiveId == id);
        await roster.Where(e => e.LectioId == lectioId).ExecuteDeleteAsync(ct);
        var count = await roster.CountAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(View(elective, count, false, false, LocalDate(clock.GetUtcNow())));
    }

    private async Task<IActionResult> WithCollector(Func<string, Task<IActionResult>> action, CancellationToken ct)
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            || !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(header.Parameter))
            return Unauthorized();
        try { return await action(header.Parameter); }
        catch (LectioCollectorException ex)
        {
            return StatusCode(ex.StatusCode is 401 or 403 ? ex.StatusCode : 502, new { error = "lectio_unavailable" });
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return StatusCode(502, new { error = "lectio_unavailable" });
        }
    }

    private static bool ValidLectioId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && !id.Any(char.IsControl);

    private static IEnumerable<ElectiveLectioStudent> ReadStudents(JsonElement directory)
    {
        if (directory.ValueKind != JsonValueKind.Object || !directory.TryGetProperty("students", out var students)
            || students.ValueKind != JsonValueKind.Array)
            throw new JsonException("Invalid Lectio directory.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in students.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || ReadString(row, "lectioId") is not { } id
                || !ValidLectioId(id) || !seen.Add(id)) continue;
            yield return new(id, UserClaims.CleanEditorName(ReadString(row, "name")) ?? id, ReadString(row, "className"));
        }
    }

    private static string? ReadString(JsonElement row, string key) =>
        row.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString()?.Trim() : null;
}
