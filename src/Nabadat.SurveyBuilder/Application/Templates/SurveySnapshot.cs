using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// The full authoring-state copy of a survey, held (serialised as jsonb) in a
/// <c>template_snapshots.snapshot</c> row and copied back into a fresh survey on instantiate
/// (data-model.md §2.9, FR-7.4 copy-all / BR-7.1 snapshot-no-link). Standalone questions hang off
/// each <see cref="SectionSnapshot.Questions"/>; rotating-set questions hang off
/// <see cref="SectionSnapshot.Sets"/>. The positional members are the settings pinned by the US5
/// unit tests; the init-only members carry the remaining copied settings, the appearance
/// (<see cref="Theme"/>), the routing overrides and the per-locale translation bundles.
/// <para><b>Translations are snapshotted (FR-7.4 copy-all, TODO-M01-022)</b>: every saved
/// <c>survey_translations</c> locale bundle is copied into <see cref="Translations"/>; on instantiate
/// the <c>section.{id}.*</c> / <c>question.{id}.*</c> keys are remapped onto the regenerated rows.</para>
/// </summary>
public sealed record SurveySnapshot(
    string NameEn,
    Guid? BoundJourneyId,
    LayoutMode Layout,
    IReadOnlyList<SectionSnapshot> Sections,
    int SchemaVersion = 1)
{
    public string? NameAr { get; init; }

    public string? Description { get; init; }

    public SurveyType SurveyType { get; init; } = SurveyType.SeasonalRelational;

    public ThemeMode ThemeMode { get; init; } = ThemeMode.Inherited;

    public string? WelcomeHtml { get; init; }

    public string? ThanksHtml { get; init; }

    public int SanitiserPolicyVersion { get; init; } = 1;

    public string? RedirectUrl { get; init; }

    public int RedirectAfterS { get; init; }

    public int? QuestionsPerPage { get; init; }

    public ActivePeriod? ActivePeriod { get; init; }

    public bool Shuffle { get; init; }

    public string ShuffleMode { get; init; } = "random";

    public bool RoutingOn { get; init; }

    public string? ThemeLogoFileHandle { get; init; }

    public ThemeSnapshot? Theme { get; init; }

    public IReadOnlyList<RoutingMapSnapshot> RoutingMaps { get; init; } = Array.Empty<RoutingMapSnapshot>();

    /// <summary>Per-locale translation bundles copied verbatim from the source survey (FR-7.4).</summary>
    public IReadOnlyList<TranslationBundleSnapshot> Translations { get; init; } = Array.Empty<TranslationBundleSnapshot>();
}
