using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.QuestionsSets;

/// <summary>
/// Create / update / delete a Questions Set (T139). Create leaves the ceiling unchecked (the set is
/// empty and its <c>count</c> is validated as members are added); update enforces
/// <c>count &lt;= current member count</c> via <see cref="QuestionsSetValidator"/>. Delete of a
/// non-empty set requires confirmation (FR-2.6) and cascades its member questions with the same
/// routing-reset (FR-2.7) + translation-purge (FR-2.8) cleanup as the section cascade — atomic
/// inside <see cref="ITenantDbContext.ExecuteAsync"/>.
/// </summary>
public sealed class QuestionsSetService
{
    private readonly IQuestionsSetStore _sets;
    private readonly IQuestionStore _questions;
    private readonly QuestionsSetValidator _validator;
    private readonly IRoutingMapStore _routing;
    private readonly ITranslationStore _translations;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public QuestionsSetService(
        IQuestionsSetStore sets,
        IQuestionStore questions,
        QuestionsSetValidator validator,
        IRoutingMapStore routing,
        ITranslationStore translations,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _sets = sets;
        _questions = questions;
        _validator = validator;
        _routing = routing;
        _translations = translations;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<QuestionsSet> CreateAsync(Guid? id, QuestionsSetWriteModel model, CancellationToken ct = default)
    {
        // The set is empty on create, so the count ceiling is not enforced yet (SetSize = MaxValue).
        Validate(new QuestionsSetDraft { Title = model.Title, Count = model.Count, SetSize = int.MaxValue });

        var now = _timeProvider.GetUtcNow();
        var set = new QuestionsSet
        {
            Id = id ?? Guid.NewGuid(),
            SectionId = model.SectionId,
            Title = model.Title,
            Description = model.Description,
            SelectionMode = model.SelectionMode,
            Count = model.Count,
            Order = model.Order,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _context.ExecuteAsync(async () => await _sets.AddAsync(set, ct), ct);
        return set;
    }

    public async Task<QuestionsSet> UpdateAsync(Guid id, QuestionsSetWriteModel model, CancellationToken ct = default)
    {
        var set = await _sets.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("questionsset.not_found", 404, "Questions Set not found.");

        var memberCount = await _questions.CountBySetAsync(id, ct);
        Validate(new QuestionsSetDraft { Title = model.Title, Count = model.Count, SetSize = memberCount });

        set.Title = model.Title;
        set.Description = model.Description;
        set.SelectionMode = model.SelectionMode;
        set.Count = model.Count;
        set.Order = model.Order;
        set.UpdatedAt = _timeProvider.GetUtcNow();
        set.IncrementRowVersion();

        await _context.ExecuteAsync(async () => await _sets.UpdateAsync(set, ct), ct);
        return set;
    }

    public async Task<QuestionsSetDeletionResult> DeleteAsync(Guid id, bool confirmed, CancellationToken ct = default)
    {
        var set = await _sets.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("questionsset.not_found", 404, "Questions Set not found.");

        var sectionQuestions = await _questions.GetBySectionAsync(set.SectionId, ct);
        var members = sectionQuestions.Where(q => q.SetId == id).ToList();

        if (members.Count > 0 && !confirmed)
        {
            return QuestionsSetDeletionResult.Blocked("questionsset.delete.requires_confirmation", members.Count);
        }

        var memberIds = members.Select(q => q.Id).ToList();

        await _context.ExecuteAsync(async () =>
        {
            foreach (var questionId in memberIds)
            {
                await _routing.DeleteByTargetQuestionAsync(questionId, ct); // FR-2.7 (defensive — set members can't route, but inbound targets can exist historically)
                await _questions.DeleteAsync(questionId, ct);
            }

            if (memberIds.Count > 0)
            {
                await _translations.PurgeQuestionKeysAsync(memberIds, ct); // FR-2.8
            }

            await _sets.DeleteAsync(id, ct);
        }, ct);

        return QuestionsSetDeletionResult.Success();
    }

    private void Validate(QuestionsSetDraft draft)
    {
        var result = _validator.Validate(draft);
        if (!result.IsValid)
        {
            throw new SurveyBuilderException(result.Errors[0], 400, "The Questions Set is invalid.");
        }
    }
}
