using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Characters;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Web.Characters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/character-artwork")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CharacterArtworkController(AppDbContext db) : ControllerBase
{
    private readonly CharacterArtworkLibrary library = new(db);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, string? characterSlug = null, int offset = 0)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        if (offset is < 0 or > int.MaxValue - 12) return BadRequest();
        var character = characterSlug is null ? null : CharacterCatalog.Find(characterSlug);
        if (characterSlug is not null && character is null) return NotFound();
        return Ok(await library.ListAsync(User, character?.Slug, offset, ct));
    }

    [HttpGet("characters")]
    public IActionResult Characters()
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        return Ok(CharacterCatalog.All.Where(item => NotebookCoverStyle.IsSupportedCharacter(item.Slug))
            .Select(item => new { item.Slug, item.Name }).ToArray());
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct, bool thumbnail = false)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        var bytes = await library.ReadAsync(id, User, thumbnail, ct);
        if (bytes is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(bytes, "image/webp");
    }
}
