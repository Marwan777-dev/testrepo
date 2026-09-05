using Nabadat.SurveyBuilder.Application.Templates.Dtos;
using Nabadat.SurveyBuilder.Application.Templates.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// F6 template-picker query service (T193). Loads class/sector-filtered candidates from
/// <see cref="ITemplateStore"/>, applies the name/tag filter through <see cref="TemplateSearchIndexer"/>
/// (so the running query matches the unit-tested semantics exactly, FR-6.2), orders
/// <b>customized-first then built-in</b> (FR-6.1) with each group sorted by the requested key, and
/// paginates (offset cursor, API-04). Template volume is low (a tenant's own set + the built-ins),
/// so in-memory filtering/paging over the candidate set is acceptable.
/// </summary>
public sealed class TemplateSearchService
{
    private readonly ITemplateStore _templates;

    public TemplateSearchService(ITemplateStore templates) => _templates = templates;

    public async Task<TemplateSearchResult> SearchAsync(TemplateSearchQuery query, CancellationToken ct = default)
    {
        var candidates = await _templates.ListAsync(query.Class, query.Sector, ct);

        var matched = candidates.Where(t => TemplateSearchIndexer.Match(query.Q ?? string.Empty, t));

        var descending = !string.Equals(query.Order, "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = Order(matched, query.Sort, descending)
            // Customized first (FR-6.1) — stable over the per-group sort above.
            .OrderBy(t => t.Class == TemplateClass.BuiltIn ? 1 : 0)
            .ToList();

        var total = ordered.Count;
        var offset = DecodeOffset(query.PageToken);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = ordered.Skip(offset).Take(pageSize).ToList();
        var nextOffset = offset + items.Count;
        var nextToken = nextOffset < total ? EncodeOffset(nextOffset) : null;

        return new TemplateSearchResult(items, nextToken, total);
    }

    private static IEnumerable<Template> Order(IEnumerable<Template> source, string sort, bool descending) =>
        (sort, descending) switch
        {
            ("name_en", false) => source.OrderBy(t => t.NameEn, StringComparer.OrdinalIgnoreCase),
            ("name_en", true) => source.OrderByDescending(t => t.NameEn, StringComparer.OrdinalIgnoreCase),
            (_, false) => source.OrderBy(t => t.UpdatedAt),
            (_, true) => source.OrderByDescending(t => t.UpdatedAt),
        };

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
