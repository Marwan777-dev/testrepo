using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;

namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Deletes a single question (T140). FR-2.7: every inbound routing override pointing at the question
/// is removed so the next-in-order default reapplies (its own outbound overrides cascade at the DB
/// level via the <c>source_question_id</c> FK). FR-2.8: its translation keys are purged across all
/// locales. Atomic inside <see cref="ITenantDbContext.ExecuteAsync"/>.
/// </summary>
public sealed class QuestionDeletionService
{
    private readonly IQuestionStore _questions;
    private readonly IRoutingMapStore _routing;
    private readonly ITranslationStore _translations;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public QuestionDeletionService(
        IQuestionStore questions,
        IRoutingMapStore routing,
        ITranslationStore translations,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _questions = questions;
        _routing = routing;
        _translations = translations;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task DeleteAsync(QuestionDeletionCommand command, CancellationToken ct = default)
    {
        _ = await _questions.GetAsync(command.QuestionId, ct)
            ?? throw new SurveyBuilderException("question.not_found", 404, "Question not found.");

        await _context.ExecuteAsync(async () =>
        {
            await _routing.DeleteByTargetQuestionAsync(command.QuestionId, ct); // FR-2.7 reset inbound → default
            await _translations.PurgeQuestionKeysAsync(new[] { command.QuestionId }, ct); // FR-2.8
            await _questions.DeleteAsync(command.QuestionId, ct);
        }, ct);
    }
}
