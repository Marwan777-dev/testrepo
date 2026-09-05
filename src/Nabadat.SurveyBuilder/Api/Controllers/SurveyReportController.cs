using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Report;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F13 Survey Report endpoints (contracts/report-and-analytics.md): the report payload and the
/// "show more" verbatim expansion. Both are read-only and route directly to Elasticsearch (AD-04).
/// <para><b>required_permission = <c>survey.report.read</c></b>; scope <c>organisation</c> for
/// P-01/P-02, with region/branch narrowing applied server-side from the caller's
/// <c>PermissionSnapshot.ScopeAssignments</c> before the ES query is dispatched (APIs-constitution
/// Article 4.5). Authentication is enforced by <c>[Authorize]</c> (host PortalSession scheme);
/// errors use the API-05 envelope. No ETag — read-only.</para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{id:guid}/report")]
public sealed class SurveyReportController : ControllerBase
{
    private const int DefaultVerbatimLimit = 20;

    private readonly ReportService _reports;
    private readonly ISessionContextAccessor _session;

    public SurveyReportController(ReportService reports, ISessionContextAccessor session)
    {
        _reports = reports;
        _session = session;
    }

    /// <summary>
    /// <c>GET /api/v1/surveys/{id}/report</c> — metric cards, headline KPI gauges, and per-question
    /// views over the requested period. <c>period=custom</c> requires <c>from</c>/<c>to</c>
    /// (<c>400 report.period.invalid</c> otherwise).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ReportView>> GetReport(
        Guid id,
        [FromQuery] string? period,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var report = await _reports.GetReportAsync(id, period, from, to, CallerScope(), ct);
        return Ok(ReportView.From(report));
    }

    /// <summary>
    /// <c>GET /api/v1/surveys/{id}/report/verbatims</c> — the newest verbatims for one Text/Paragraph
    /// question (FR-13.7), <c>limit</c> defaulting to 20 and capped at 100.
    /// </summary>
    [HttpGet("verbatims")]
    public async Task<ActionResult<IReadOnlyList<VerbatimSampleResponse>>> GetVerbatims(
        Guid id,
        [FromQuery(Name = "question_id")] Guid questionId,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var verbatims = await _reports.GetVerbatimsAsync(id, questionId, limit ?? DefaultVerbatimLimit, CallerScope(), ct);
        return Ok(verbatims.Select(VerbatimSampleResponse.From).ToList());
    }

    // The caller's data scope (Article 4.5) from the session permission snapshot; empty ⇒ org-wide.
    private ReportScope CallerScope()
    {
        var assignments = _session.Current?.PermissionSnapshot.ScopeAssignments;
        return assignments is { Count: > 0 } ? new ReportScope(assignments) : ReportScope.Organisation;
    }
}
