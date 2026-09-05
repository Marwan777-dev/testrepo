using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// The single source of truth for Templates-tab search semantics (T193/T185, FR-6.2): a term
/// matches a template when it is a case-insensitive substring of the template's <b>name</b> or of
/// <b>any of its tags</b>. An empty/whitespace term matches everything (an empty search box lists
/// all templates). <see cref="TemplateSearchService"/> applies this over the candidate set so the
/// running query matches the unit-tested contract exactly.
/// </summary>
public static class TemplateSearchIndexer
{
    public static bool Match(string term, Template template)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        var needle = term.Trim();
        if (template.NameEn.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return template.Tags.Any(tag => tag.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
