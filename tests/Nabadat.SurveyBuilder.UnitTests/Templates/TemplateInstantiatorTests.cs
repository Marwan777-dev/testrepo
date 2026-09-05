using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Templates;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Templates;

/// <summary>
/// T186 [US5] — write-first unit tests for <c>TemplateInstantiator</c> (T194). "Use this template"
/// (FR-6.3) creates a fresh Draft survey pre-loaded with <b>all</b> of the template snapshot's data —
/// settings, questions, appearance <b>and</b> journey/stage/touchpoint bindings — owned by the caller,
/// with <b>no back-reference</b> to the template (Q4 / BR-7.1 snapshot-no-link). "No back-reference" is
/// asserted structurally: the new survey/section/question rows get fresh identities while the copied
/// binding values (journey/stage/touchpoint) are preserved verbatim — a copy, not a link.
/// <para>
/// Contract pinned for the implementer (T194):
/// <list type="bullet">
///   <item><c>TemplateInstantiator</c> lives in <c>Application/Templates/</c> and is pure — it maps a
///   <c>SurveySnapshot</c> (T183) into fresh domain aggregates; persistence + the wrapping
///   <c>ExecuteAsync</c> are the command service's job (T195).</item>
///   <item><c>InstantiatedSurvey CreateSurveyFrom(SurveySnapshot snapshot, Guid callerId,
///   DateTimeOffset now)</c>.</item>
///   <item><c>InstantiatedSurvey(Survey Survey, IReadOnlyList&lt;Section&gt; Sections,
///   IReadOnlyList&lt;Question&gt; Questions)</c> in <c>Application/Templates/</c> (theme/translations/
///   routing are copied too by the full instantiator — additive, not pinned by this unit).</item>
///   <item>The new <c>Survey</c> has <c>Status = Draft</c>, <c>OwnerUserId = callerId</c>,
///   <c>CreatedBy = UpdatedBy = callerId</c>, a fresh <c>Id</c>, and settings copied from the snapshot.
///   Each copied question keeps the snapshot's <c>StageId</c>/<c>TouchpointId</c> but gets a fresh
///   <c>Id</c> (copy-not-link).</item>
/// </list>
/// Neither <c>TemplateInstantiator</c>, <c>InstantiatedSurvey</c>, nor <c>SurveySnapshot</c> exists yet
/// → the project fails to COMPILE (valid red).
/// </para>
/// </summary>
public sealed class TemplateInstantiatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 9, 0, 0, TimeSpan.Zero);

    private static SurveySnapshot BuildSnapshot(Guid journeyId, Guid stageId, Guid touchpointId, out Guid sourceQuestionId)
    {
        sourceQuestionId = Guid.NewGuid();
        var question = new QuestionSnapshot(sourceQuestionId, "How satisfied were you?", QuestionType.Kpi, journeyId, stageId, touchpointId);
        var section = new SectionSnapshot(Guid.NewGuid(), "Experience", new[] { question });
        return new SurveySnapshot("Post-visit satisfaction", journeyId, LayoutMode.Section, new[] { section });
    }

    [Fact]
    public void CreateSurveyFrom_produces_a_fresh_draft_survey_owned_by_the_caller()
    {
        var caller = Guid.NewGuid();
        var journeyId = Guid.NewGuid();
        var snapshot = BuildSnapshot(journeyId, Guid.NewGuid(), Guid.NewGuid(), out _);

        var result = TemplateInstantiator.CreateSurveyFrom(snapshot, caller, Now);

        result.Survey.NameEn.Should().Be("Post-visit satisfaction");
        result.Survey.BoundJourneyId.Should().Be(journeyId);
        result.Survey.Layout.Should().Be(LayoutMode.Section);
        result.Survey.Status.Should().Be(SurveyStatus.Draft);
        result.Survey.OwnerUserId.Should().Be(caller);
        result.Survey.CreatedBy.Should().Be(caller);
        result.Survey.UpdatedBy.Should().Be(caller);
        result.Survey.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateSurveyFrom_copies_the_journey_stage_and_touchpoint_bindings_onto_the_new_questions()
    {
        var caller = Guid.NewGuid();
        var journeyId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var touchpointId = Guid.NewGuid();
        var snapshot = BuildSnapshot(journeyId, stageId, touchpointId, out _);

        var result = TemplateInstantiator.CreateSurveyFrom(snapshot, caller, Now);

        result.Questions.Should().ContainSingle()
            .Which.Should().Match<Domain.Entities.Question>(q =>
                q.StageId == stageId && q.TouchpointId == touchpointId && q.SurveyId == result.Survey.Id);
    }

    [Fact]
    public void CreateSurveyFrom_copies_translation_bundles_and_remaps_their_section_and_question_keys()
    {
        // FR-7.4 / TODO-M01-022 — instantiate re-persists the copied bundles, remapping the
        // section.{id}.* / question.{id}.* keys onto the regenerated rows while survey-level keys pass through.
        var caller = Guid.NewGuid();
        var journeyId = Guid.NewGuid();
        var oldQuestionId = Guid.NewGuid();
        var oldSectionId = Guid.NewGuid();
        var question = new QuestionSnapshot(oldQuestionId, "How satisfied were you?", QuestionType.Kpi, journeyId, Guid.NewGuid(), Guid.NewGuid());
        var section = new SectionSnapshot(oldSectionId, "Experience", new[] { question });
        var snapshot = new SurveySnapshot("Post-visit satisfaction", journeyId, LayoutMode.Section, new[] { section })
        {
            Translations = new[]
            {
                new TranslationBundleSnapshot("ar", new Dictionary<string, string>
                {
                    ["survey.name"] = "استبيان ما بعد الزيارة",
                    [$"section.{oldSectionId}.title"] = "التجربة",
                    [$"question.{oldQuestionId}.text"] = "ما مدى رضاك؟",
                }),
            },
        };

        var result = TemplateInstantiator.CreateSurveyFrom(snapshot, caller, Now);

        var newSectionId = result.Sections.Should().ContainSingle().Subject.Id;
        var newQuestionId = result.Questions.Should().ContainSingle().Subject.Id;
        var bundle = result.Translations.Should().ContainSingle().Subject;

        bundle.Locale.Should().Be("ar");
        bundle.SurveyId.Should().Be(result.Survey.Id);
        bundle.Keys["survey.name"].Should().Be("استبيان ما بعد الزيارة");            // survey-level key unchanged
        bundle.Keys[$"section.{newSectionId}.title"].Should().Be("التجربة");         // remapped to the new section id
        bundle.Keys[$"question.{newQuestionId}.text"].Should().Be("ما مدى رضاك؟");   // remapped to the new question id
        bundle.Keys.Keys.Should().NotContain($"section.{oldSectionId}.title");
        bundle.Keys.Keys.Should().NotContain($"question.{oldQuestionId}.text");
    }

    [Fact]
    public void CreateSurveyFrom_regenerates_row_identities_so_the_new_survey_does_not_link_back_to_the_template()
    {
        var caller = Guid.NewGuid();
        var snapshot = BuildSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), out var sourceQuestionId);

        var result = TemplateInstantiator.CreateSurveyFrom(snapshot, caller, Now);

        result.Questions.Should().OnlyContain(q => q.Id != sourceQuestionId);
        result.Sections.Should().OnlyContain(s => s.Id != snapshot.Sections[0].SectionId);
    }
}
