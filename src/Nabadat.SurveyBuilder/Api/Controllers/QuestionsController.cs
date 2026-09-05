using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Questions;
using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F8 question endpoints (contracts/questions.md, routing endpoints excluded — US4): add / edit /
/// delete a question, and move it across sections/sets. Delete resets inbound routing (FR-2.7) and
/// purges translations (FR-2.8); a move into a set strips its routing (FR-9.5). Writes carry an
/// <c>If-Match</c> (Q1); every non-2xx uses the API-05 envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{surveyId:guid}/sections/{sectionId:guid}/questions")]
public sealed class QuestionsController : ControllerBase
{
    private readonly QuestionCommandService _commands;
    private readonly QuestionDeletionService _deletion;
    private readonly QuestionMoveService _move;
    private readonly IQuestionStore _questions;
    private readonly ISessionContextAccessor _session;

    public QuestionsController(
        QuestionCommandService commands,
        QuestionDeletionService deletion,
        QuestionMoveService move,
        IQuestionStore questions,
        ISessionContextAccessor session)
    {
        _commands = commands;
        _deletion = deletion;
        _move = move;
        _questions = questions;
        _session = session;
    }

    private Guid ActorId => _session.Current?.UserId ?? throw new SurveyBuilderException("survey.unauthenticated", 401, "No session.");

    [HttpPost]
    public async Task<ActionResult<QuestionView>> Create(Guid surveyId, Guid sectionId, [FromBody] CreateQuestionRequest request, CancellationToken ct)
    {
        var question = await _commands.CreateAsync(request.ToWriteModel(surveyId, sectionId), ct);
        SetEtag(question.RowVersion);
        return Created($"/api/v1/surveys/{surveyId}/sections/{sectionId}/questions/{question.Id}", QuestionView.From(question));
    }

    [HttpPut("{questionId:guid}")]
    public async Task<ActionResult<QuestionView>> Update(Guid surveyId, Guid sectionId, Guid questionId, [FromBody] UpdateQuestionRequest request, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(questionId, ct);
        var question = await _commands.UpdateAsync(questionId, request.ToWriteModel(surveyId, sectionId), ct);
        SetEtag(question.RowVersion);
        return Ok(QuestionView.From(question));
    }

    [HttpDelete("{questionId:guid}")]
    public async Task<IActionResult> Delete(Guid surveyId, Guid sectionId, Guid questionId, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(questionId, ct);
        await _deletion.DeleteAsync(new QuestionDeletionCommand(questionId, ActorId, Guid.NewGuid()), ct);
        return Ok();
    }

    [HttpPost("{questionId:guid}/move")]
    public async Task<IActionResult> Move(Guid surveyId, Guid sectionId, Guid questionId, [FromBody] MoveQuestionRequest request, CancellationToken ct)
    {
        await _move.MoveAsync(request.ToCommand(questionId, ActorId, Guid.NewGuid()), ct);
        return Ok();
    }

    private void SetEtag(int rowVersion) => Response.Headers.ETag = $"W/\"{rowVersion}\"";

    private async Task EnsureEtagMatchesAsync(Guid questionId, CancellationToken ct)
    {
        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(ifMatch))
        {
            throw new SurveyBuilderException("question.etag_required", 400, "If-Match header is required (Q1).");
        }

        var question = await _questions.GetAsync(questionId, ct)
            ?? throw new SurveyBuilderException("question.not_found", 404, "Question not found.");
        if (ParseWeakEtag(ifMatch) != question.RowVersion)
        {
            throw new SurveyBuilderException("question.conflict", 409, "The question was modified by another writer.");
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
