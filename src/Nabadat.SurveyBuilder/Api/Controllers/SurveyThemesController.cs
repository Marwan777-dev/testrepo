using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Appearance;
using Nabadat.SurveyBuilder.Application.Exceptions;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F4 survey appearance endpoints (contracts/surveys.md theme routes): resolve the effective tokens
/// (Inherited ⇒ tenant guidelines; Customize ⇒ the survey's theme) and save a Customize theme.
/// Logo/background upload (multipart → <c>IFileStorageService</c>) is wired when the shared file
/// adapter ships (TODO-M01-006). Authentication via <c>[Authorize]</c>; errors via the API-05 envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{id:guid}/theme")]
public sealed class SurveyThemesController : ControllerBase
{
    private readonly AppearanceService _appearance;

    public SurveyThemesController(AppearanceService appearance) => _appearance = appearance;

    [HttpGet]
    public async Task<ActionResult<ThemeView>> Get(Guid id, CancellationToken ct)
    {
        var resolved = await _appearance.ResolveAsync(id, ct);
        return Ok(ThemeView.From(resolved));
    }

    [HttpPut]
    public async Task<ActionResult<ThemeView>> Update(Guid id, [FromBody] UpdateThemeRequest request, CancellationToken ct)
    {
        var result = await _appearance.SaveAsync(request.ToCommand(id), ct);
        if (!result.IsValid)
        {
            throw new SurveyBuilderException(result.Errors[0], 400, "The theme is invalid.");
        }

        var resolved = await _appearance.ResolveAsync(id, ct);
        return Ok(ThemeView.From(resolved));
    }
}
