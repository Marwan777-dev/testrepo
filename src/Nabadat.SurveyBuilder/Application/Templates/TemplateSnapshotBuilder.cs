using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// Builds the immutable <see cref="SurveySnapshot"/> that a template stores — a full copy of a
/// survey's authoring state (T191, FR-7.4). Pure: the caller (<c>TemplateCommandService</c>) loads
/// the aggregate from the stores and hands the already-materialised graph in. Every question's
/// journey binding is denormalised from the survey (<see cref="Survey.BoundJourneyId"/>) so it is
/// self-contained in the snapshot and copies back verbatim on instantiate (BR-7.1).
/// </summary>
public static class TemplateSnapshotBuilder
{
    /// <summary>Current snapshot schema version emitted by <see cref="Build"/>.</summary>
    public const int CurrentSchemaVersion = 1;

    public static SurveySnapshot Build(
        Survey survey,
        IReadOnlyList<Section> sections,
        IReadOnlyList<Question> questions,
        IReadOnlyList<QuestionsSet>? sets = null,
        Theme? theme = null,
        IReadOnlyList<RoutingMap>? routingMaps = null,
        IReadOnlyList<SurveyTranslation>? translations = null)
    {
        sets ??= Array.Empty<QuestionsSet>();
        routingMaps ??= Array.Empty<RoutingMap>();
        translations ??= Array.Empty<SurveyTranslation>();

        var standaloneBySection = questions.Where(q => q.SetId is null).ToLookup(q => q.SectionId);
        var membersBySet = questions.Where(q => q.SetId is not null).ToLookup(q => q.SetId!.Value);
        var setsBySection = sets.ToLookup(s => s.SectionId);
        var journeyId = survey.BoundJourneyId;

        var sectionSnapshots = sections
            .OrderBy(s => s.Order)
            .Select(section => new SectionSnapshot(
                section.Id,
                section.Name,
                standaloneBySection[section.Id].OrderBy(q => q.Order).Select(q => ToQuestion(q, journeyId)).ToList())
            {
                Description = section.Description,
                Order = section.Order,
                Sets = setsBySection[section.Id].OrderBy(s => s.Order).Select(set => new QuestionsSetSnapshot(
                    set.Id,
                    set.Title,
                    set.SelectionMode,
                    set.Count,
                    set.Order,
                    membersBySet[set.Id].OrderBy(q => q.Order).Select(q => ToQuestion(q, journeyId)).ToList())
                {
                    Description = set.Description,
                }).ToList(),
            })
            .ToList();

        return new SurveySnapshot(survey.NameEn, survey.BoundJourneyId, survey.Layout, sectionSnapshots, CurrentSchemaVersion)
        {
            Description = survey.Description,
            SurveyType = survey.SurveyType,
            ThemeMode = survey.ThemeMode,
            WelcomeHtml = survey.WelcomeHtml,
            ThanksHtml = survey.ThanksHtml,
            SanitiserPolicyVersion = survey.SanitiserPolicyVersion,
            RedirectUrl = survey.RedirectUrl,
            RedirectAfterS = survey.RedirectAfterS,
            QuestionsPerPage = survey.QuestionsPerPage,
            ActivePeriod = survey.ActivePeriod,
            Shuffle = survey.Shuffle,
            ShuffleMode = survey.ShuffleMode,
            RoutingOn = survey.RoutingOn,
            ThemeLogoFileHandle = survey.ThemeLogoFileHandle,
            Theme = theme is null ? null : ToTheme(theme),
            RoutingMaps = routingMaps
                .Select(r => new RoutingMapSnapshot(r.SourceQuestionId, r.AnswerKey, r.TargetQuestionId))
                .ToList(),
            Translations = translations
                .Select(t => new TranslationBundleSnapshot(t.Locale, new Dictionary<string, string>(t.Keys)))
                .ToList(),
        };
    }

    private static QuestionSnapshot ToQuestion(Question q, Guid? journeyId) =>
        new(q.Id, q.Text, q.Type, journeyId, q.StageId, q.TouchpointId)
        {
            SectionId = q.SectionId,
            SetId = q.SetId,
            Subtype = q.Subtype,
            Description = q.Description,
            Required = q.Required,
            Comments = q.Comments,
            CommentLabel = q.CommentLabel,
            CommentMaxLength = q.CommentMaxLength,
            Sentiment = q.Sentiment,
            KpiCode = q.KpiCode,
            Perspective = q.Perspective,
            BoundJourneyOn = q.BoundJourneyOn,
            Order = q.Order,
            TypePayload = q.TypePayload,
        };

    private static ThemeSnapshot ToTheme(Theme t) => new(
        t.PrimaryColor,
        t.TextColor,
        t.ButtonRadiusPx,
        t.ButtonBorderColor,
        t.ButtonTextColor,
        t.HeaderShowLogo,
        t.HeaderShowTitle,
        t.HeaderAlignment,
        t.FooterText,
        t.BackgroundType,
        t.BackgroundConfig,
        t.BackgroundOpacity,
        t.AdvancedStatusColors,
        t.AdvancedSurfaces,
        t.AdvancedTypography,
        t.AdvancedLayout);
}
