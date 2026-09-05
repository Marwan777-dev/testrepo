using Microsoft.EntityFrameworkCore;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Stores;

/// <summary>EF implementation of <see cref="ISectionStore"/> (T064) over <see cref="ITenantDbContext"/>.</summary>
public sealed class SectionStore : ISectionStore
{
    private readonly ITenantDbContext _context;

    public SectionStore(ITenantDbContext context) => _context = context;

    public Task<Section?> GetAsync(Guid id, CancellationToken ct = default) =>
        _context.Sections.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Section>> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        await _context.Sections.Where(s => s.SurveyId == surveyId).OrderBy(s => s.Order).ToListAsync(ct);

    public Task<int> CountBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        _context.Sections.CountAsync(s => s.SurveyId == surveyId, ct);

    public Task AddAsync(Section section, CancellationToken ct = default)
    {
        _context.Sections.Add(section);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Section section, CancellationToken ct = default)
    {
        _context.Sections.Update(section);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var section = await _context.Sections.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section is not null)
        {
            _context.Sections.Remove(section);
        }
    }
}
