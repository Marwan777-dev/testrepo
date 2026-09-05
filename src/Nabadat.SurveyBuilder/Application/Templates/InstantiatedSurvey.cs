using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// The fresh (not-yet-persisted) survey aggregate produced by <see cref="TemplateInstantiator"/>
/// from a <see cref="SurveySnapshot"/>. All rows carry newly-generated identities (copy-not-link,
/// BR-7.1); the caller (<c>TemplateCommandService</c>) persists the graph inside one
/// <c>ITenantDbContext.ExecuteAsync</c>. The positional members are pinned by the US5 unit test;
/// the init-only members carry the rest of the copied graph the command service must persist.
/// </summary>
public sealed record InstantiatedSurvey(
    Survey Survey,
    IReadOnlyList<Section> Sections,
    IReadOnlyList<Question> Questions)
{
    public IReadOnlyList<QuestionsSet> QuestionsSets { get; init; } = Array.Empty<QuestionsSet>();

    public Theme? Theme { get; init; }

    public IReadOnlyList<RoutingMap> RoutingMaps { get; init; } = Array.Empty<RoutingMap>();

    /// <summary>
    /// Per-locale translation bundles copied from the snapshot, with <c>section.{id}.*</c> /
    /// <c>question.{id}.*</c> keys remapped onto the regenerated section/question ids (FR-7.4).
    /// </summary>
    public IReadOnlyList<SurveyTranslation> Translations { get; init; } = Array.Empty<SurveyTranslation>();
}
