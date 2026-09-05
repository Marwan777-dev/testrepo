using Microsoft.EntityFrameworkCore;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Stores;

/// <summary>EF implementation of <see cref="IQuestionStore"/> (T065) over <see cref="ITenantDbContext"/>.</summary>
public sealed class QuestionStore : IQuestionStore
{
    private readonly ITenantDbContext _context;

    public QuestionStore(ITenantDbContext context) => _context = context;

    public Task<Question?> GetAsync(Guid id, CancellationToken ct = default) =>
        _context.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyList<Question>> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        await _context.Questions.Where(q => q.SurveyId == surveyId).OrderBy(q => q.Order).ToListAsync(ct);

    public async Task<IReadOnlyList<Question>> GetBySectionAsync(Guid sectionId, CancellationToken ct = default) =>
        await _context.Questions.Where(q => q.SectionId == sectionId).OrderBy(q => q.Order).ToListAsync(ct);

    public Task<int> CountBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        _context.Questions.CountAsync(q => q.SurveyId == surveyId, ct);

    public Task<int> CountBySetAsync(Guid setId, CancellationToken ct = default) =>
        _context.Questions.CountAsync(q => q.SetId == setId, ct);

    public Task AddAsync(Question question, CancellationToken ct = default)
    {
        _context.Questions.Add(question);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Question question, CancellationToken ct = default)
    {
        _context.Questions.Update(question);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (question is not null)
        {
            _context.Questions.Remove(question);
        }
    }

    public async Task MoveAsync(Guid questionId, Guid targetSectionId, Guid? targetSetId, int targetOrder, CancellationToken ct = default)
    {
        var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId, ct);
        if (question is null)
        {
            return;
        }

        var sourceSectionId = question.SectionId;
        var sourceSetId = question.SetId;
        var sameContainer = sourceSectionId == targetSectionId && sourceSetId == targetSetId;

        // Reparent, then compact so every container keeps a contiguous, unique `order` per
        // (section_id, set_id) — contracts/questions.md, data-model.md §2.4 (FR-8.2).
        question.SectionId = targetSectionId;
        question.SetId = targetSetId;
        question.IncrementRowVersion();

        // Destination: insert the moved question at the requested index and renumber 0..n-1.
        await ReindexContainerAsync(targetSectionId, targetSetId, excludeQuestionId: questionId, insert: question, insertAt: targetOrder, ct);

        // Source (only when it differs): close the slot the moved question vacated.
        if (!sameContainer)
        {
            await ReindexContainerAsync(sourceSectionId, sourceSetId, excludeQuestionId: questionId, insert: null, insertAt: null, ct);
        }
    }

    /// <summary>
    /// Renumbers a <c>(section_id, set_id)</c> container to a contiguous 0..n-1 <c>order</c> sequence.
    /// The moved question is excluded from the DB scan (its row still holds its pre-move placement) and,
    /// when <paramref name="insert"/> is supplied, re-inserted at the clamped <paramref name="insertAt"/>
    /// index so the result stays gap-free even if the client sent an out-of-range order. All touched rows
    /// are change-tracked, so the surrounding <c>ExecuteAsync</c> persists them in one transaction.
    /// </summary>
    private async Task ReindexContainerAsync(
        Guid sectionId, Guid? setId, Guid excludeQuestionId, Question? insert, int? insertAt, CancellationToken ct)
    {
        var ordered = await _context.Questions
            .Where(q => q.SectionId == sectionId && q.SetId == setId && q.Id != excludeQuestionId)
            .OrderBy(q => q.Order)
            .ThenBy(q => q.Id)
            .ToListAsync(ct);

        if (insert is not null)
        {
            var index = Math.Clamp(insertAt ?? ordered.Count, 0, ordered.Count);
            ordered.Insert(index, insert);
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Order != i)
            {
                ordered[i].Order = i;
                ordered[i].IncrementRowVersion();
            }
        }
    }
}
