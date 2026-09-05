using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// Maps a <see cref="SurveySnapshot"/> into a fresh, Draft survey aggregate owned by the caller
/// (T194, FR-6.3 "Use this template"). Pure: it only builds the graph; persistence + the wrapping
/// <c>ITenantDbContext.ExecuteAsync</c> are <c>TemplateCommandService</c>'s job (T195). Every row
/// gets a new identity while copied binding values (journey/stage/touchpoint) are preserved — a
/// copy, not a link, so the new survey has no back-reference to the template (BR-7.1). Routing
/// overrides are remapped from the snapshot's original question ids onto the new ones.
/// </summary>
public static class TemplateInstantiator
{
    public static InstantiatedSurvey CreateSurveyFrom(SurveySnapshot snapshot, Guid callerId, DateTimeOffset now)
    {
        var survey = Survey.Create(Guid.NewGuid(), snapshot.NameEn, callerId, snapshot.BoundJourneyId, callerId, now);
        survey.Description = snapshot.Description;
        // Keep the BR-3.3 journey↔type invariant rather than trusting the stored type.
        survey.SurveyType = snapshot.BoundJourneyId is null ? SurveyType.SeasonalRelational : SurveyType.Transactional;
        survey.ThemeMode = snapshot.ThemeMode;
        survey.WelcomeHtml = snapshot.WelcomeHtml;
        survey.ThanksHtml = snapshot.ThanksHtml;
        survey.SanitiserPolicyVersion = snapshot.SanitiserPolicyVersion;
        survey.RedirectUrl = snapshot.RedirectUrl;
        survey.RedirectAfterS = snapshot.RedirectAfterS;
        survey.Layout = snapshot.Layout;
        survey.QuestionsPerPage = snapshot.QuestionsPerPage;
        survey.ActivePeriod = snapshot.ActivePeriod;
        survey.RoutingOn = snapshot.RoutingOn;
        survey.Shuffle = snapshot.RoutingOn ? false : snapshot.Shuffle; // routing_on locks shuffle off (F9)
        survey.ShuffleMode = snapshot.ShuffleMode;
        survey.ThemeLogoFileHandle = snapshot.ThemeLogoFileHandle;

        var sections = new List<Section>();
        var sets = new List<QuestionsSet>();
        var questions = new List<Question>();
        var idMap = new Dictionary<Guid, Guid>(); // snapshot question id → new question id
        var sectionIdMap = new Dictionary<Guid, Guid>(); // snapshot section id → new section id

        foreach (var sec in snapshot.Sections)
        {
            var section = new Section
            {
                Id = Guid.NewGuid(),
                SurveyId = survey.Id,
                Name = sec.Name,
                Description = sec.Description,
                Order = sec.Order,
                CreatedAt = now,
                UpdatedAt = now,
            };
            sections.Add(section);
            sectionIdMap[sec.SectionId] = section.Id;

            foreach (var q in sec.Questions)
            {
                questions.Add(CopyQuestion(q, survey.Id, section.Id, setId: null, now, idMap));
            }

            foreach (var setSnapshot in sec.Sets)
            {
                var set = new QuestionsSet
                {
                    Id = Guid.NewGuid(),
                    SectionId = section.Id,
                    Title = setSnapshot.Title,
                    Description = setSnapshot.Description,
                    SelectionMode = setSnapshot.SelectionMode,
                    Count = setSnapshot.Count,
                    Order = setSnapshot.Order,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                sets.Add(set);

                foreach (var q in setSnapshot.Questions)
                {
                    questions.Add(CopyQuestion(q, survey.Id, section.Id, set.Id, now, idMap));
                }
            }
        }

        var routingMaps = new List<RoutingMap>();
        foreach (var route in snapshot.RoutingMaps)
        {
            if (!idMap.TryGetValue(route.SourceQuestionId, out var newSource))
            {
                continue; // source no longer present — drop the override (default reapplies)
            }

            Guid? newTarget = null;
            if (route.TargetQuestionId is { } target)
            {
                if (!idMap.TryGetValue(target, out var mapped))
                {
                    continue; // target no longer present — drop the override
                }

                newTarget = mapped;
            }

            routingMaps.Add(new RoutingMap
            {
                Id = Guid.NewGuid(),
                SurveyId = survey.Id,
                SourceQuestionId = newSource,
                AnswerKey = route.AnswerKey,
                TargetQuestionId = newTarget,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        Theme? theme = null;
        if (snapshot.Theme is { } t)
        {
            theme = new Theme
            {
                Id = Guid.NewGuid(),
                SurveyId = survey.Id,
                PrimaryColor = t.PrimaryColor,
                TextColor = t.TextColor,
                ButtonRadiusPx = t.ButtonRadiusPx,
                ButtonBorderColor = t.ButtonBorderColor,
                ButtonTextColor = t.ButtonTextColor,
                HeaderShowLogo = t.HeaderShowLogo,
                HeaderShowTitle = t.HeaderShowTitle,
                HeaderAlignment = t.HeaderAlignment,
                FooterText = t.FooterText,
                BackgroundType = t.BackgroundType,
                BackgroundConfig = t.BackgroundConfig,
                BackgroundOpacity = t.BackgroundOpacity,
                AdvancedStatusColors = t.AdvancedStatusColors,
                AdvancedSurfaces = t.AdvancedSurfaces,
                AdvancedTypography = t.AdvancedTypography,
                AdvancedLayout = t.AdvancedLayout,
                CreatedAt = now,
                UpdatedAt = now,
            };
        }

        var translations = new List<SurveyTranslation>();
        foreach (var bundle in snapshot.Translations)
        {
            var remapped = new Dictionary<string, string>(bundle.Keys.Count);
            foreach (var (key, value) in bundle.Keys)
            {
                remapped[RemapKey(key, sectionIdMap, idMap)] = value;
            }

            translations.Add(new SurveyTranslation
            {
                Id = Guid.NewGuid(),
                SurveyId = survey.Id,
                Locale = bundle.Locale,
                Keys = remapped,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = 1,
            });
        }

        return new InstantiatedSurvey(survey, sections, questions)
        {
            QuestionsSets = sets,
            Theme = theme,
            RoutingMaps = routingMaps,
            Translations = translations,
        };
    }

    /// <summary>
    /// Rewrites the entity id embedded in a translation key onto the regenerated row. Keys are
    /// <c>section.{id}.title</c> and <c>question.{id}.{text|description|options.i.label|…}</c>
    /// (survey-level keys like <c>survey.name</c> have no id and pass through). If the embedded id is
    /// not in the map (a stale key) the original key is kept — harmless, it just resolves to nothing.
    /// </summary>
    private static string RemapKey(
        string key, IReadOnlyDictionary<Guid, Guid> sectionIdMap, IReadOnlyDictionary<Guid, Guid> questionIdMap)
    {
        var firstDot = key.IndexOf('.');
        if (firstDot < 0)
        {
            return key;
        }

        var prefix = key[..firstDot];
        var map = prefix switch
        {
            "section" => sectionIdMap,
            "question" => questionIdMap,
            _ => null,
        };
        if (map is null)
        {
            return key;
        }

        var secondDot = key.IndexOf('.', firstDot + 1);
        if (secondDot < 0)
        {
            return key;
        }

        var idSegment = key[(firstDot + 1)..secondDot];
        if (!Guid.TryParse(idSegment, out var oldId) || !map.TryGetValue(oldId, out var newId))
        {
            return key;
        }

        return $"{prefix}.{newId}{key[secondDot..]}";
    }

    private static Question CopyQuestion(
        QuestionSnapshot q, Guid surveyId, Guid sectionId, Guid? setId, DateTimeOffset now, Dictionary<Guid, Guid> idMap)
    {
        var newId = Guid.NewGuid();
        idMap[q.QuestionId] = newId;
        return new Question
        {
            Id = newId,
            SurveyId = surveyId,
            SectionId = sectionId,
            SetId = setId,
            Type = q.Type,
            Subtype = q.Subtype,
            Text = q.Text,
            Description = q.Description,
            Required = q.Required,
            Comments = q.Comments,
            CommentLabel = q.CommentLabel,
            CommentMaxLength = q.CommentMaxLength,
            Sentiment = q.Sentiment,
            KpiCode = q.KpiCode,
            Perspective = q.Perspective,
            BoundJourneyOn = q.BoundJourneyOn,
            StageId = q.StageId,
            TouchpointId = q.TouchpointId,
            TypePayload = q.TypePayload!,
            Order = q.Order,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
