using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections.Dtos;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;

namespace Nabadat.SurveyBuilder.Application.Sections;

/// <summary>
/// Deletes a section and cascades its children (T138, FR-2.5/2.6/2.7/2.8). A non-empty section
/// requires client confirmation (else <c>section.delete.requires_confirmation</c>); an empty section
/// deletes unconditionally. On a confirmed cascade every child standalone question and Questions Set
/// (and its child questions) is deleted, every inbound routing override pointing at a deleted
/// question is removed so the next-in-order default reapplies (FR-2.7), and the deleted questions'
/// translation keys are purged (FR-2.8) — all atomically inside <see cref="ITenantDbContext.ExecuteAsync"/>.
/// </summary>
public sealed class SectionCascadeService
{
    private readonly ISectionStore _sections;
    private readonly IQuestionStore _questions;
    private readonly IQuestionsSetStore _sets;
    private readonly IRoutingMapStore _routing;
    private readonly ITranslationStore _translations;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SectionCascadeService(
        ISectionStore sections,
        IQuestionStore questions,
        IQuestionsSetStore sets,
        IRoutingMapStore routing,
        ITranslationStore translations,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _sections = sections;
        _questions = questions;
        _sets = sets;
        _routing = routing;
        _translations = translations;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<SectionCascadeResult> DeleteAsync(SectionCascadeCommand command, CancellationToken ct = default)
    {
        _ = await _sections.GetAsync(command.SectionId, ct)
            ?? throw new SurveyBuilderException("section.not_found", 404, "Section not found.");

        var questions = await _questions.GetBySectionAsync(command.SectionId, ct);
        var sets = await _sets.GetBySectionAsync(command.SectionId, ct);

        var isEmpty = questions.Count == 0 && sets.Count == 0;
        if (!isEmpty && !command.Confirmed)
        {
            // Surface the child breakdown so the client can render the destructive-delete prompt
            // (FR-2.5) — contracts/sections-and-sets.md DELETE 409 details. A question is a set
            // member when it carries a set_id; the remainder are standalone (both share section_id).
            return SectionCascadeResult.Blocked(
                "section.delete.requires_confirmation",
                standaloneQuestions: questions.Count(q => q.SetId is null),
                questionsSets: sets.Count,
                setQuestions: questions.Count(q => q.SetId is not null));
        }

        var questionIds = questions.Select(q => q.Id).ToList();

        await _context.ExecuteAsync(async () =>
        {
            foreach (var questionId in questionIds)
            {
                await _routing.DeleteByTargetQuestionAsync(questionId, ct); // FR-2.7 reset inbound → default
                await _questions.DeleteAsync(questionId, ct);
            }

            if (questionIds.Count > 0)
            {
                await _translations.PurgeQuestionKeysAsync(questionIds, ct); // FR-2.8
            }

            foreach (var set in sets)
            {
                await _sets.DeleteAsync(set.Id, ct);
            }

            await _sections.DeleteAsync(command.SectionId, ct);
        }, ct);

        return SectionCascadeResult.Success();
    }
}
