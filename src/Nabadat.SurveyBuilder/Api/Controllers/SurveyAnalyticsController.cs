using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Analytics;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F14 Survey Analytics endpoint (contracts/report-and-analytics.md § GET /analytics): the
/// reach-and-drop-off funnel, per-channel breakdown and responses-trend line over a selected period
/// and granularity. Read-only and routes directly to Elasticsearch (AD-04).
/// <para><b>required_permission = <c>survey.analytics.read</c></b>; scope <c>organisation</c> (no
/// per-region/branch narrowing — F14 is organisation-wide). Authentication is enforced by
/// <c>[Authorize]</c> (host PortalSession scheme); a bad <c>period</c>/<c>granularity</c> is
/// <c>400 analytics.period.invalid</c> / <c>analytics.granularity.invalid</c> via the API-05
/// envelope. No ETag — read-only.</para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{id:guid}/analytics")]
public sealed class SurveyAnalyticsController : ControllerBase
{
    private readonly AnalyticsService _analytics;

    public SurveyAnalyticsController(AnalyticsService analytics) => _analytics = analytics;

    /// <summary>
    /// <c>GET /api/v1/surveys/{id}/analytics</c> — funnel, deltas, channel breakdown and trend over
    /// the requested period. <c>period=custom</c> requires <c>from</c>/<c>to</c>; <c>granularity</c>
    /// defaults to the period-appropriate bucket size when omitted.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AnalyticsView>> Get(
        Guid id,
        [FromQuery] string? period,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? granularity,
        CancellationToken ct)
    {
        var result = await _analytics.GetAsync(id, period, from, to, granularity, ct);
        return Ok(AnalyticsView.From(result));
    }
}
