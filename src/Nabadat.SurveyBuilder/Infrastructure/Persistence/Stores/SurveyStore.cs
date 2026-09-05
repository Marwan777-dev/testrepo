using Microsoft.EntityFrameworkCore;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Stores;

/// <summary>
/// EF implementation of <see cref="ISurveyStore"/> (T063) over <see cref="ITenantDbContext"/>.
/// Tracks entities on the context; the caller owns the transaction/save boundary
/// (<c>SaveChangesAsync</c> for a single write, <c>ExecuteAsync</c> for a compound one).
/// </summary>
public sealed class SurveyStore : ISurveyStore
{
    private readonly ITenantDbContext _context;

    public SurveyStore(ITenantDbContext context) => _context = context;

    public Task<Survey?> GetAsync(Guid id, CancellationToken ct = default) =>
        _context.Surveys.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<SurveyContentCounts> GetContentCountsAsync(Guid id, CancellationToken ct = default)
    {
        var sections = await _context.Sections.CountAsync(s => s.SurveyId == id, ct);
        var questions = await _context.Questions.CountAsync(q => q.SurveyId == id, ct);
        return new SurveyContentCounts(sections, questions);
    }

    public Task AddAsync(Survey survey, CancellationToken ct = default)
    {
        _context.Surveys.Add(survey);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Survey survey, CancellationToken ct = default)
    {
        _context.Surveys.Update(survey);
        return Task.CompletedTask;
    }

    public async Task<SurveySearchResult> SearchAsync(SurveySearchQuery query, CancellationToken ct = default)
    {
        IQueryable<Survey> q = _context.Surveys;

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToLower();
            q = q.Where(s => s.NameEn.ToLower().Contains(term));
        }

        if (query.Types is { Count: > 0 })
        {
            q = q.Where(s => query.Types.Contains(s.SurveyType));
        }

        if (query.Statuses is { Count: > 0 })
        {
            q = q.Where(s => query.Statuses.Contains(s.Status));
        }

        if (query.JourneyId is { } journeyId)
        {
            q = q.Where(s => s.BoundJourneyId == journeyId);
        }

        var descending = !string.Equals(query.Order, "asc", StringComparison.OrdinalIgnoreCase);
        q = (query.Sort, descending) switch
        {
            ("name_en", false) => q.OrderBy(s => s.NameEn),
            ("name_en", true) => q.OrderByDescending(s => s.NameEn),
            ("status", false) => q.OrderBy(s => s.Status),
            ("status", true) => q.OrderByDescending(s => s.Status),
            (_, false) => q.OrderBy(s => s.UpdatedAt),
            (_, true) => q.OrderByDescending(s => s.UpdatedAt),
        };

        var total = await q.CountAsync(ct);
        var offset = DecodeOffset(query.PageToken);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.Skip(offset).Take(pageSize).ToListAsync(ct);
        var nextOffset = offset + items.Count;
        var nextToken = nextOffset < total ? EncodeOffset(nextOffset) : null;

        return new SurveySearchResult(items, nextToken, total);
    }

    private static int DecodeOffset(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return 0;
        }

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
            return int.TryParse(raw, out var offset) && offset >= 0 ? offset : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    private static string EncodeOffset(int offset) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(offset.ToString()));
}
