using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;

namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Moves a question across sections/sets (T142, US3 drag-and-drop). Persists all three placement
/// fields (<c>section_id</c>, <c>set_id</c>, <c>order</c>) via <see cref="IQuestionStore.MoveAsync"/>
/// inside <see cref="ITenantDbContext.ExecuteAsync"/>; the store compacts sibling <c>order</c> values in
/// both the source and destination containers so each stays contiguous and unique (FR-8.2). When the
/// question lands inside a set, any pre-existing routing for it — as source OR target — is removed,
/// because set questions cannot be routing sources or targets (FR-9.5).
/// </summary>
public sealed class QuestionMoveService
{
    private readonly IQuestionStore _questions;
    private readonly IRoutingMapStore _routing;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public QuestionMoveService(
        IQuestionStore questions,
        IRoutingMapStore routing,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _questions = questions;
        _routing = routing;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task MoveAsync(MoveQuestionCommand command, CancellationToken ct = default)
    {
        _ = await _questions.GetAsync(command.QuestionId, ct)
            ?? throw new SurveyBuilderException("question.not_found", 404, "Question not found.");

        await _context.ExecuteAsync(async () =>
        {
            await _questions.MoveAsync(
                command.QuestionId, command.TargetSectionId, command.TargetSetId, command.TargetOrder, ct);

            if (command.TargetSetId is not null)
            {
                // FR-9.5 — a set question may be neither a routing source nor target; drop any existing routing.
                await _routing.DeleteBySourceQuestionAsync(command.QuestionId, ct);
                await _routing.DeleteByTargetQuestionAsync(command.QuestionId, ct);
            }
        }, ct);
    }
}
