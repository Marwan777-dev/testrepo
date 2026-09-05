using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Templates;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Templates;

/// <summary>
/// T183 [US5] — write-first unit tests for <c>TemplateSnapshotBuilder</c> (T191). Saving a survey as
/// a template takes a <b>full copy</b> of its authoring state (FR-7.4 / Q4 snapshot-no-link); the
/// binding-critical invariant this unit pins is that <b>every</b> question in the snapshot carries the
/// survey's journey binding together with its own stage/touchpoint — nothing is dropped on the way in
/// (so instantiation, T186, can copy them back out).
/// <para>
/// Contract pinned for the implementer (T191):
/// <list type="bullet">
///   <item><c>TemplateSnapshotBuilder</c> lives in <c>Application/Templates/</c> and is pure (no I/O,
///   no <c>TimeProvider</c>) — the stores hand it the already-loaded aggregate.</item>
///   <item><c>SurveySnapshot Build(Survey survey, IReadOnlyList&lt;Section&gt; sections,
///   IReadOnlyList&lt;Question&gt; questions)</c>. The full builder also folds in questions-sets, the
///   theme, the translation bundle and routing maps per data-model.md §2.9 + contracts/templates.md;
///   this unit pins only the <b>settings + question-binding</b> copy invariant (FR-7.4). Those extra
///   collections are additive to the returned record and are exercised by the US5 integration lane.</item>
///   <item>Records in <c>Application/Templates/</c> (one type per file): <c>SurveySnapshot(string
///   NameEn, Guid? BoundJourneyId, LayoutMode Layout, IReadOnlyList&lt;SectionSnapshot&gt; Sections,
///   int SchemaVersion = 1)</c>; <c>SectionSnapshot(Guid SectionId, string Name,
///   IReadOnlyList&lt;QuestionSnapshot&gt; Questions)</c>; <c>QuestionSnapshot(Guid QuestionId, string
///   Text, QuestionType Type, Guid? JourneyId, Guid? StageId, Guid? TouchpointId)</c>.</item>
///   <item>Each <c>QuestionSnapshot.JourneyId</c> is denormalised from <c>survey.BoundJourneyId</c> so
///   a question's binding is self-contained in the snapshot (copy-all).</item>
/// </list>
/// Neither <c>TemplateSnapshotBuilder</c> nor the snapshot records exist yet → the project fails to
/// COMPILE (valid red per CLAUDE.md Unit Test Policy rule 7).
/// </para>
/// </summary>
public sealed class TemplateSnapshotBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_copies_the_journey_stage_and_touchpoint_binding_onto_every_question()
    {
        var journeyId = Guid.NewGuid();
        var stageA = Guid.NewGuid();
        var touchpointA = Guid.NewGuid();
        var stageB = Guid.NewGuid();
        var touchpointB = Guid.NewGuid();

        var survey = Survey.Create(Guid.NewGuid(), "Post-visit satisfaction", Guid.NewGuid(), journeyId, Guid.NewGuid(), Now);
        var section = new Section { Id = Guid.NewGuid(), SurveyId = survey.Id, Name = "Experience", Order = 0 };
        var q1 = new Question
        {
            Id = Guid.NewGuid(),
            SurveyId = survey.Id,
            SectionId = section.Id,
            Type = QuestionType.Kpi,
            KpiCode = "CSAT",
            BoundJourneyOn = true,
            StageId = stageA,
            TouchpointId = touchpointA,
            Order = 0,
        };
        var q2 = new Question
        {
            Id = Guid.NewGuid(),
            SurveyId = survey.Id,
            SectionId = section.Id,
            Type = QuestionType.Kpi,
            KpiCode = "NPS",
            BoundJourneyOn = true,
            StageId = stageB,
            TouchpointId = touchpointB,
            Order = 1,
        };

        var snapshot = TemplateSnapshotBuilder.Build(survey, new[] { section }, new[] { q1, q2 });

        var snapshotQuestions = snapshot.Sections.SelectMany(s => s.Questions).ToList();
        snapshotQuestions.Should().HaveCount(2);
        snapshotQuestions.Should().OnlyContain(q => q.JourneyId == journeyId);
        snapshotQuestions.Should().ContainSingle(q => q.QuestionId == q1.Id)
            .Which.Should().Match<QuestionSnapshot>(q => q.StageId == stageA && q.TouchpointId == touchpointA);
        snapshotQuestions.Should().ContainSingle(q => q.QuestionId == q2.Id)
            .Which.Should().Match<QuestionSnapshot>(q => q.StageId == stageB && q.TouchpointId == touchpointB);
    }

    [Fact]
    public void Build_copies_every_translation_bundle_into_the_snapshot()
    {
        // FR-7.4 / TODO-M01-022 — save-as-template copies "all" data, including translations.
        var survey = Survey.Create(Guid.NewGuid(), "Post-visit satisfaction", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        var section = new Section { Id = Guid.NewGuid(), SurveyId = survey.Id, Name = "Experience", Order = 0 };
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SurveyId = survey.Id,
            SectionId = section.Id,
            Type = QuestionType.Kpi,
            KpiCode = "CSAT",
            Order = 0,
        };
        var ar = new SurveyTranslation
        {
            Id = Guid.NewGuid(),
            SurveyId = survey.Id,
            Locale = "ar",
            Keys = new Dictionary<string, string>
            {
                ["survey.name"] = "استبيان ما بعد الزيارة",
                [$"question.{question.Id}.text"] = "ما مدى رضاك؟",
            },
        };

        var snapshot = TemplateSnapshotBuilder.Build(
            survey, new[] { section }, new[] { question }, translations: new[] { ar });

        var bundle = snapshot.Translations.Should().ContainSingle().Subject;
        bundle.Locale.Should().Be("ar");
        bundle.Keys["survey.name"].Should().Be("استبيان ما بعد الزيارة");
        bundle.Keys[$"question.{question.Id}.text"].Should().Be("ما مدى رضاك؟");
    }

    [Fact]
    public void Build_defaults_to_no_translations_when_none_are_supplied()
    {
        var survey = Survey.Create(Guid.NewGuid(), "Onboarding pulse", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

        var snapshot = TemplateSnapshotBuilder.Build(survey, Array.Empty<Section>(), Array.Empty<Question>());

        snapshot.Translations.Should().BeEmpty();
    }

    [Fact]
    public void Build_copies_the_survey_settings_and_defaults_to_schema_version_1()
    {
        var journeyId = Guid.NewGuid();
        var survey = Survey.Create(Guid.NewGuid(), "Onboarding pulse", Guid.NewGuid(), journeyId, Guid.NewGuid(), Now);
        survey.Layout = LayoutMode.Question;

        var snapshot = TemplateSnapshotBuilder.Build(survey, Array.Empty<Section>(), Array.Empty<Question>());

        snapshot.NameEn.Should().Be("Onboarding pulse");
        snapshot.BoundJourneyId.Should().Be(journeyId);
        snapshot.Layout.Should().Be(LayoutMode.Question);
        snapshot.SchemaVersion.Should().Be(1);
    }
}
