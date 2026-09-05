using Microsoft.EntityFrameworkCore;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Stores;

/// <summary>EF implementation of <see cref="IQuestionsSetStore"/> (T136) over <see cref="ITenantDbContext"/>.</summary>
public sealed class QuestionsSetStore : IQuestionsSetStore
{
    private readonly ITenantDbContext _context;

    public QuestionsSetStore(ITenantDbContext context) => _context = context;

    public Task<QuestionsSet?> GetAsync(Guid id, CancellationToken ct = default) =>
        _context.QuestionsSets.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<QuestionsSet>> GetBySectionAsync(Guid sectionId, CancellationToken ct = default) =>
        await _context.QuestionsSets.Where(s => s.SectionId == sectionId).OrderBy(s => s.Order).ToListAsync(ct);

    public Task AddAsync(QuestionsSet set, CancellationToken ct = default)
    {
        _context.QuestionsSets.Add(set);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(QuestionsSet set, CancellationToken ct = default)
    {
        _context.QuestionsSets.Update(set);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var set = await _context.QuestionsSets.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (set is not null)
        {
            _context.QuestionsSets.Remove(set);
        }
    }
}
