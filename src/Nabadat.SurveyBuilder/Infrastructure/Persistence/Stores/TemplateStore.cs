using Microsoft.EntityFrameworkCore;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Templates.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Stores;

/// <summary>
/// EF implementation of <see cref="ITemplateStore"/> (T190) over <see cref="ITenantDbContext"/>.
/// Tracks entities on the context; the caller owns the transaction/save boundary
/// (<c>ExecuteAsync</c> for the template-row + snapshot pair).
/// </summary>
public sealed class TemplateStore : ITemplateStore
{
    private readonly ITenantDbContext _context;

    public TemplateStore(ITenantDbContext context) => _context = context;

    public Task<Template?> GetAsync(Guid id, CancellationToken ct = default) =>
        _context.Templates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<TemplateSnapshot?> GetSnapshotAsync(Guid templateId, CancellationToken ct = default) =>
        _context.TemplateSnapshots.FirstOrDefaultAsync(s => s.TemplateId == templateId, ct);

    public Task AddAsync(Template template, TemplateSnapshot snapshot, CancellationToken ct = default)
    {
        _context.Templates.Add(template);
        _context.TemplateSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Template template, CancellationToken ct = default)
    {
        _context.Templates.Update(template);
        return Task.CompletedTask;
    }

    public Task UpdateSnapshotAsync(TemplateSnapshot snapshot, CancellationToken ct = default)
    {
        _context.TemplateSnapshots.Update(snapshot);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Template template, CancellationToken ct = default)
    {
        _context.Templates.Remove(template);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Template>> ListAsync(TemplateClass? cls, string? sector, CancellationToken ct = default)
    {
        IQueryable<Template> q = _context.Templates;

        if (cls is { } templateClass)
        {
            q = q.Where(t => t.Class == templateClass);
        }

        if (!string.IsNullOrWhiteSpace(sector))
        {
            q = q.Where(t => t.Sectors.Contains(sector));
        }

        return await q.ToListAsync(ct);
    }
}
