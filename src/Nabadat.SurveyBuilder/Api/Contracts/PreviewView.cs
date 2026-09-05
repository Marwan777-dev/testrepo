using Nabadat.SurveyBuilder.Application.Preview;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// F12 preview payload view (contracts/report-and-analytics.md § GET /preview): a light-weight
/// <c>GET /surveys/{id}</c> with resolved <see cref="Theme"/> tokens and the resolved locale bundle
/// inlined. <see cref="Sections"/>/<see cref="Questions"/> are ordered; the SPA groups questions under
/// their section (by <c>SectionId</c>), renders section titles above them (FR-12.4), and paginates by
/// <c>Survey.Layout</c> (FR-12.3). <see cref="Translations"/> maps each source key to its resolved
/// value (target or English fallback); <see cref="MissingKeys"/> are the keys not yet translated.
/// </summary>
public sealed record PreviewView(
    string Channel,
    string Locale,
    SurveyView Survey,
    ThemeView Theme,
    IReadOnlyList<SectionView> Sections,
    IReadOnlyList<QuestionView> Questions,
    IReadOnlyDictionary<string, string> Translations,
    IReadOnlyList<string> MissingKeys)
{
    public static PreviewView From(PreviewPayload payload) => new(
        payload.Channel,
        payload.Locale,
        SurveyView.From(payload.Survey),
        ThemeView.From(payload.Theme),
        payload.Sections.Select(SectionView.From).ToList(),
        payload.Questions.Select(QuestionView.From).ToList(),
        payload.Translations.Keys,
        payload.Translations.MissingKeys);
}
