using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me/onboarding")]
public sealed class OnboardingController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<OnboardingStepDto[]>> Get(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub || sub == Guid.Empty) return Unauthorized();

        if (await db.OnboardingDismissals.AnyAsync(x => x.UserSub == sub, ct))
            return Array.Empty<OnboardingStepDto>();

        var audience = UserClaims.IsTeacher(User) ? OnboardingStep.Teacher : OnboardingStep.Student;
        return await db.OnboardingSteps
            .Where(x => x.Audience == audience)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new OnboardingStepDto(x.Title, x.Text))
            .ToArrayAsync(ct);
    }

    [HttpPut]
    public async Task<IActionResult> Dismiss(DismissOnboardingRequest request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub || sub == Guid.Empty) return Unauthorized();
        if (request.ExpectedSubject != sub) return Conflict();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO onboarding_dismissals ("UserSub") VALUES ({sub})
            ON CONFLICT ("UserSub") DO NOTHING
            """, ct);
        return NoContent();
    }
}
