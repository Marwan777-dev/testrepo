using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Accessors;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Api.Filters;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F1 / F3 / F5 survey endpoints (contracts/surveys.md): list, get (deep-link), create, update,
/// clone, status change (self-serve transitions), and the dispatch-time render-plan. Authentication
/// is enforced by <c>[Authorize]</c> (host PortalSession scheme); the actor (user id / persona) is
/// read from <see cref="ISessionContextAccessor"/>. Writes carry an <c>ETag</c>; every non-2xx uses
/// the API-05 envelope (via <c>ApiErrorEnvelopeMiddleware</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys")]
public sealed class SurveysController : ControllerBase
{
    private readonly SurveyCommandService _commands;
    private readonly SurveyLifecycleService _lifecycle;
    private readonly RulesCountProjection _rulesCount;
    private readonly ISurveyRenderService _render;
    private readonly ISessionContextAccessor _session;

    public SurveysController(
        SurveyCommandService commands,
        SurveyLifecycleService lifecycle,
        RulesCountProjection rulesCount,
        ISurveyRenderService render,
        ISessionContextAccessor session)
    {
        _commands = commands;
        _lifecycle = lifecycle;
        _rulesCount = rulesCount;
        _render = render;
        _session = session;
    }

    private Guid ActorId => _session.Current?.UserId ?? throw new SurveyBuilderException("survey.unauthenticated", 401, "No session.");

    private string ActorRole => _session.Current?.Persona ?? "P-00";

    [HttpGet]
    public async Task<ActionResult<SurveyListResponse>> List([FromQuery] SurveyListQuery query, CancellationToken ct)
    {
        var result = await _commands.SearchAsync(query.ToSearchQuery(), ct);
        var items = new List<SurveyListItem>(result.Items.Count);
        foreach (var survey in result.Items)
        {
            var rules = await _rulesCount.ReadAsync(survey.Id, ct);
            items.Add(SurveyListItem.From(survey, rules));
        }

        return Ok(new SurveyListResponse(items, result.NextPageToken, result.TotalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SurveyView>> Get(Guid id, CancellationToken ct)
    {
        var survey = await _commands.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");
        SetEtag(survey.RowVersion);
        return Ok(SurveyView.From(survey));
    }

    [HttpPost]
    public async Task<ActionResult<SurveyView>> Create([FromBody] CreateSurveyRequest request, CancellationToken ct)
    {
        var survey = await _commands.CreateAsync(request.ToDraft(), ActorId, request.Id, ct);
        SetEtag(survey.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = survey.Id }, SurveyView.From(survey));
    }

    [HttpPut("{id:guid}")]
    [ServiceFilter(typeof(EditLockFilter))]
    public async Task<ActionResult<SurveyView>> Update(Guid id, [FromBody] UpdateSurveyRequest request, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(id, ct);
        var survey = await _commands.UpdateAsync(id, request.ToDraft(), ActorId, ct);
        SetEtag(survey.RowVersion);
        return Ok(SurveyView.From(survey));
    }

    [HttpPost("{id:guid}/clone")]
    public async Task<ActionResult<SurveyView>> Clone(Guid id, [FromBody] CloneSurveyRequest? request, CancellationToken ct)
    {
        var survey = await _commands.CloneAsync(id, ActorId, ct);
        SetEtag(survey.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = survey.Id }, SurveyView.From(survey));
    }

    [HttpPost("{id:guid}/status")]
    [ServiceFilter(typeof(PublishGateFilter))]
    public async Task<ActionResult<SurveyView>> ChangeStatus(Guid id, [FromBody] SurveyStatusChangeRequest request, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(id, ct);
        var command = new SurveyStatusChangeCommand(id, request.To, ActorId, ActorRole, Guid.NewGuid(), request.Confirm);
        await _lifecycle.ChangeStatusAsync(command, ct);

        var survey = await _commands.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");
        SetEtag(survey.RowVersion);
        return Ok(SurveyView.From(survey));
    }

    [HttpGet("{id:guid}/render-plan")]
    public async Task<ActionResult<RenderPlanResponse>> RenderPlan(Guid id, [FromQuery] string respondent_id, CancellationToken ct)
    {
        // The dispatch-time seam (M-02/M-04) + admin diagnostics: the real FR-10.4 low-response
        // ordering, per-set sampling, and routing-map projection assembled by ISurveyRenderService
        // (AD-01). It 404s (indistinguishable-absence) when the survey is missing or not Active.
        var respondent = new RespondentContext(RespondentSeed.From(respondent_id), new LocaleCode("en"));
        var plan = await _render.GetRenderPlanAsync(new SurveyId(id), respondent, ct);
        return Ok(RenderPlanResponse.From(plan));
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id) =>
        throw new SurveyBuilderException("method_not_allowed", 405, "Surveys are archived, not deleted (Article 4.4).");

    private void SetEtag(int rowVersion) => Response.Headers.ETag = $"W/\"{rowVersion}\"";

    private async Task EnsureEtagMatchesAsync(Guid id, CancellationToken ct)
    {
        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(ifMatch))
        {
            throw new SurveyBuilderException("survey.etag_required", 400, "If-Match header is required (Q1).");
        }

        var expected = ParseWeakEtag(ifMatch);
        var survey = await _commands.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");
        if (expected is null || expected != survey.RowVersion)
        {
            throw new SurveyBuilderException("survey.conflict", 409, "The survey was modified by another writer.");
        }
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
