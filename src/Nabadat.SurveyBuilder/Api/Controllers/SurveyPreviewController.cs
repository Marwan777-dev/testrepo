using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Preview;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F12 multi-channel preview endpoint (contracts/report-and-analytics.md § GET /preview). Returns a
/// light-weight survey view with resolved theme tokens + the resolved locale bundle inlined; the SPA
/// re-renders channel chrome around this same payload client-side (FR-12.1). An unknown
/// <c>channel</c> is <c>400 preview.channel.invalid</c>. Authentication via <c>[Authorize]</c>; errors
/// via the API-05 envelope. Read-only — no ETag/If-Match.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{id:guid}/preview")]
public sealed class SurveyPreviewController : ControllerBase
{
    private readonly PreviewPayloadBuilder _preview;

    public SurveyPreviewController(PreviewPayloadBuilder preview) => _preview = preview;

    [HttpGet]
    public async Task<ActionResult<PreviewView>> Get(
        Guid id,
        [FromQuery] string? channel,
        [FromQuery] string? locale,
        CancellationToken ct)
    {
        var payload = await _preview.BuildAsync(id, channel, locale, ct);
        return Ok(PreviewView.From(payload));
    }
}
