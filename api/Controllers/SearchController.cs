using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Search;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/search")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SearchController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GlobalSearchResult>> Search(CancellationToken ct, string? q = null,
        string category = GlobalSearchCategories.All, int offset = 0, int limit = 30, bool publishedOnly = false)
    {
        if (UserClaims.GetSub(User) is not Guid sub || sub == Guid.Empty) return Unauthorized();
        if (q is not null && (q.Length > 200 || q.Any(c => char.IsControl(c) && !char.IsWhiteSpace(c)))
            || offset is < 0 or > 100000 || limit is < 1 or > 100 || !GlobalSearchCategories.IsValid(category))
            return BadRequest(new { error = "invalid_search" });
        if (string.IsNullOrWhiteSpace(q)) return Ok(new GlobalSearchResult([], 0));

        var query = string.Join(' ', PublicGlobalSearch.SearchWords(q));
        return Ok(await new GlobalSearchService(db, clock).SearchAsync(User, sub, query, category, offset, limit, publishedOnly, ct));
    }
}
