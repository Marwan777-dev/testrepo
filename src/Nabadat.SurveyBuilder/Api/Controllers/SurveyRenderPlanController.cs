using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// Diagnostics endpoint for the FR-10.4 render plan (T150). Complements the canonical
/// <c>GET …/render-plan</c> seam (M-02/M-04) with an admin-facing <c>POST</c> that lets a specific
/// respondent id + locale be supplied in the body — so an admin can reproduce exactly what a given
/// respondent would receive (the deterministic Random sample). Delegates to the published
/// <see cref="ISurveyRenderService"/> (AD-01 — the same in-process seam), which 404s
/// (indistinguishable-absence) when the survey is missing or not Active.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{id:guid}/render-plan")]
public sealed class SurveyRenderPlanController : ControllerBase
{
    private readonly ISurveyRenderService _render;

    public SurveyRenderPlanController(ISurveyRenderService render) => _render = render;

    [HttpPost]
    public async Task<ActionResult<RenderPlanResponse>> Post(Guid id, [FromBody] RenderPlanDiagnosticsRequest? request, CancellationToken ct)
    {
        var respondent = new RespondentContext(
            request?.RespondentId ?? Guid.NewGuid(),
            new LocaleCode(string.IsNullOrWhiteSpace(request?.Locale) ? "en" : request!.Locale!));

        var plan = await _render.GetRenderPlanAsync(new SurveyId(id), respondent, ct);
        return Ok(RenderPlanResponse.From(plan));
    }
}
