using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.QuestionsSets;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F10 Questions Set endpoints (contracts/sections-and-sets.md): add / edit / delete a rotating
/// Questions Set inside a section. Deletion of a non-empty set requires <c>?confirm=true</c> (FR-2.6).
/// Writes carry an <c>If-Match</c> (Q1); every non-2xx uses the API-05 envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{surveyId:guid}/sections/{sectionId:guid}/sets")]
public sealed class QuestionsSetsController : ControllerBase
{
    private readonly QuestionsSetService _sets;
    private readonly IQuestionsSetStore _store;
    private readonly ISessionContextAccessor _session;

    public QuestionsSetsController(
        QuestionsSetService sets,
        IQuestionsSetStore store,
        ISessionContextAccessor session)
    {
        _sets = sets;
        _store = store;
        _session = session;
    }

    private Guid ActorId => _session.Current?.UserId ?? throw new SurveyBuilderException("survey.unauthenticated", 401, "No session.");

    [HttpPost]
    public async Task<ActionResult<QuestionsSetView>> Create(Guid surveyId, Guid sectionId, [FromBody] CreateQuestionsSetRequest request, CancellationToken ct)
    {
        var set = await _sets.CreateAsync(request.Id, request.ToWriteModel(sectionId), ct);
        SetEtag(set.RowVersion);
        return Created($"/api/v1/surveys/{surveyId}/sections/{sectionId}/sets/{set.Id}", QuestionsSetView.From(set));
    }

    [HttpPatch("{setId:guid}")]
    public async Task<ActionResult<QuestionsSetView>> Update(Guid surveyId, Guid sectionId, Guid setId, [FromBody] UpdateQuestionsSetRequest request, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(setId, ct);
        var set = await _sets.UpdateAsync(setId, request.ToWriteModel(sectionId), ct);
        SetEtag(set.RowVersion);
        return Ok(QuestionsSetView.From(set));
    }

    [HttpDelete("{setId:guid}")]
    public async Task<IActionResult> Delete(Guid surveyId, Guid sectionId, Guid setId, [FromQuery] bool confirm, CancellationToken ct)
    {
        _ = ActorId; // enforce authentication
        await EnsureEtagMatchesAsync(setId, ct);
        var result = await _sets.DeleteAsync(setId, confirm, ct);
        if (!result.Deleted)
        {
            throw new SurveyBuilderException(
                result.ErrorCode!, 409, "The Questions Set is not empty — resend with confirm=true.", result.Details);
        }

        return Ok();
    }

    private void SetEtag(int rowVersion) => Response.Headers.ETag = $"W/\"{rowVersion}\"";

    private async Task EnsureEtagMatchesAsync(Guid setId, CancellationToken ct)
    {
        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(ifMatch))
        {
            throw new SurveyBuilderException("questionsset.etag_required", 400, "If-Match header is required (Q1).");
        }

        var set = await _store.GetAsync(setId, ct)
            ?? throw new SurveyBuilderException("questionsset.not_found", 404, "Questions Set not found.");
        if (ParseWeakEtag(ifMatch) != set.RowVersion)
        {
            throw new SurveyBuilderException("questionsset.conflict", 409, "The Questions Set was modified by another writer.");
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
