using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F9 answer-routing endpoints (US4, contracts/questions.md): the survey-level routing toggle and the
/// per-question routing map get/save. Delegates to <see cref="RoutingConfigurationService"/>. Writes
/// carry an <c>If-Match</c> (Q1) — the toggle against <c>survey.row_version</c>, the map save against
/// <c>question.row_version</c> — and return the new <c>ETag</c>. Every non-2xx uses the API-05 envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{surveyId:guid}")]
public sealed class SurveyRoutingController : ControllerBase
{
    private readonly RoutingConfigurationService _routing;
    private readonly ISurveyStore _surveys;
    private readonly IQuestionStore _questions;
    private readonly ISessionContextAccessor _session;

    public SurveyRoutingController(
        RoutingConfigurationService routing,
        ISurveyStore surveys,
        IQuestionStore questions,
        ISessionContextAccessor session)
    {
        _routing = routing;
        _surveys = surveys;
        _questions = questions;
        _session = session;
    }

    private Guid ActorId => _session.Current?.UserId ?? throw new SurveyBuilderException("survey.unauthenticated", 401, "No session.");

    /// <summary>POST /api/v1/surveys/{id}/routing — F9 survey-level routing toggle (FR-9.1).</summary>
    [HttpPost("routing")]
    public async Task<ActionResult<SurveyView>> Toggle(Guid surveyId, [FromBody] EnableRoutingRequest request, CancellationToken ct)
    {
        await EnsureSurveyEtagMatchesAsync(surveyId, ct);
        var survey = await _routing.ToggleRoutingAsync(surveyId, request.Enabled, request.Confirm, ActorId, ct);
        SetEtag(survey.RowVersion);
        return Ok(SurveyView.From(survey));
    }

    /// <summary>GET /api/v1/surveys/{id}/questions/{qid}/routing — the question's sparse override map.</summary>
    [HttpGet("questions/{questionId:guid}/routing")]
    public async Task<ActionResult<RoutingMapView>> Get(Guid surveyId, Guid questionId, CancellationToken ct)
    {
        var overrides = await _routing.GetMapAsync(questionId, ct);
        return Ok(RoutingMapView.From(overrides));
    }

    /// <summary>PUT /api/v1/surveys/{id}/questions/{qid}/routing — replace the question's override map.</summary>
    [HttpPut("questions/{questionId:guid}/routing")]
    public async Task<ActionResult<RoutingMapView>> Save(Guid surveyId, Guid questionId, [FromBody] UpdateRoutingMapRequest request, CancellationToken ct)
    {
        await EnsureQuestionEtagMatchesAsync(questionId, ct);
        var question = await _routing.SaveMapAsync(surveyId, questionId, request.Map, ct);
        SetEtag(question.RowVersion);
        var overrides = await _routing.GetMapAsync(questionId, ct);
        return Ok(RoutingMapView.From(overrides));
    }

    private void SetEtag(int rowVersion) => Response.Headers.ETag = $"W/\"{rowVersion}\"";

    private async Task EnsureSurveyEtagMatchesAsync(Guid surveyId, CancellationToken ct)
    {
        var expected = ParseIfMatch("survey.etag_required");
        var survey = await _surveys.GetAsync(surveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");
        if (expected is null || expected != survey.RowVersion)
        {
            throw new SurveyBuilderException("survey.conflict", 409, "The survey was modified by another writer.");
        }
    }

    private async Task EnsureQuestionEtagMatchesAsync(Guid questionId, CancellationToken ct)
    {
        var expected = ParseIfMatch("question.etag_required");
        var question = await _questions.GetAsync(questionId, ct)
            ?? throw new SurveyBuilderException("question.not_found", 404, "Question not found.");
        if (expected is null || expected != question.RowVersion)
        {
            throw new SurveyBuilderException("question.conflict", 409, "The question was modified by another writer.");
        }
    }

    private int? ParseIfMatch(string missingCode)
    {
        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(ifMatch))
        {
            throw new SurveyBuilderException(missingCode, 400, "If-Match header is required (Q1).");
        }

        return ParseWeakEtag(ifMatch);
    }

    private static int? ParseWeakEtag(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        trimmed = trimmed.Trim('"');
        return int.TryParse(trimmed, out var version) ? version : null;
    }
}
