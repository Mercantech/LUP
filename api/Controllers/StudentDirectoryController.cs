using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Students;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>Intentional staff-wide directory; students cannot read or edit other students' placement data.</summary>
[ApiController, Authorize, Route("api/students")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StudentDirectoryController(StudentDirectoryService directory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StudentDirectoryResponse>> List(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid teacher) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (Bearer() is not string bearer) return Unauthorized();
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await directory.ListAsync(bearer, ct, teacher));
    }

    [HttpPut("{key}/details")]
    public async Task<ActionResult<StudentDirectoryStudent>> Update(string key, UpdateStudentDirectoryDetails request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid teacher) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (Bearer() is not string bearer) return Unauthorized();
        if (!StudentDirectoryService.ValidRequest(request)) return BadRequest(new { error = "invalid_student_details" });
        if (!Request.Headers.TryGetValue(ProfileNameRules.SubjectHeader, out var expected)
            || !Guid.TryParse(expected.ToString(), out var expectedTeacher) || expectedTeacher != teacher)
            return Conflict(new { error = "account_changed", message = "Kontoen er skiftet. Hent elevoversigten igen." });
        var student = (await directory.ListAsync(bearer, ct, teacher, includeTeacherOverrides: !request.ResetToLectio)).Students.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (student is null) return NotFound();
        try
        {
            var updated = await directory.UpdateAsync(student, request, teacher, ct);
            Response.Headers.CacheControl = "private, no-store";
            return Ok(updated);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "student_details_conflict", message = "Elevoplysningerne blev ændret samtidig. Hent oversigten igen." });
        }
    }

    private string? Bearer()
    {
        var authorization = Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && authorization.Length > 7 ? authorization[7..].Trim() : null;
    }
}
