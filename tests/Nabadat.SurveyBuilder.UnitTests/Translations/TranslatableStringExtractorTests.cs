using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Translations;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Translations;

/// <summary>
/// T205 [US6] — unit tests for <c>TranslatableStringExtractor</c> (F11, FR-11.1). The extractor walks
/// a survey and its section/question graph and produces the flat English <b>source</b> bundle whose
/// keys the Translate workspace mirrors per locale (contracts/translations.md).
/// <para>
/// Contract pinned for the implementer (T211 — "matching T205"):
/// <list type="bullet">
///   <item><c>TranslatableStringExtractor</c> lives in <c>Application/Translations/</c>; it is
///   stateless (no ctor dependencies).</item>
///   <item><c>TranslationBundle Extract(Survey survey, IReadOnlyList&lt;Section&gt; sections,
///   IReadOnlyList&lt;Question&gt; questions)</c> returns the source bundle. The survey graph is
///   passed in explicitly because <see cref="Survey"/> holds no section/question navigation
///   collections (the App layer loads them from the stores).</item>
///   <item><c>TranslationBundle</c> (record in <c>Application/Translations/</c>):
///   <c>(string Locale, IReadOnlyDictionary&lt;string,string&gt; Keys)</c>. The extractor stamps
///   <c>Locale = TranslatableStringExtractor.SourceLocale</c> (<c>"en"</c>).</item>
///   <item>Key namespace (contracts/translations.md): <c>survey.name</c>, <c>survey.welcome</c>,
///   <c>survey.thanks</c>, <c>section.{sectionId}.title</c> (from <see cref="Section.Name"/>),
///   <c>question.{questionId}.text</c>, <c>question.{questionId}.description</c>,
///   <c>question.{questionId}.options.{i}.label</c> (Single/Multi-select), <c>question.{questionId}.scale_labels.{i}</c>
///   (Scale), <c>question.{questionId}.comment_label</c> (only when <see cref="Question.Comments"/> is on).</item>
///   <item>Optional strings that are null/empty are NOT emitted as keys (welcome/thanks/description);
///   <c>survey.name</c> and each <c>question.text</c>/<c>section.title</c> are always emitted.</item>
///   <item><b>reason_items are intentionally NOT extracted</b> — the domain has no reason-follow-up
///   field yet (tracked by TODO-M01-004). Add <c>question.{id}.reason_items.{i}</c> here when that
///   field lands.</item>
/// </list>
/// </para>
/// </summary>
public sealed class TranslatableStringExtractorTests
{
    private static readonly Guid SurveyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SectionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ScaleQuestionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SelectQuestionId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static Survey SurveyWithMessages() => new()
    {
        Id = SurveyId,
        NameEn = "Post-visit survey",
        WelcomeHtml = "<p>Welcome</p>",
        ThanksHtml = "<p>Thank you</p>",
    };

    private static Section OnlySection() => new()
    {
        Id = SectionId,
        SurveyId = SurveyId,
        Name = "Overall experience",
    };

    private static Question ScaleQuestion() => new()
    {
        Id = ScaleQuestionId,
        SurveyId = SurveyId,
        SectionId = SectionId,
        Type = QuestionType.Scale,
        Subtype = QuestionSubType.Labels,
        Text = "How satisfied were you?",
        Description = "Rate your overall satisfaction",
        Comments = true,
        CommentLabel = "Additional comments",
        TypePayload = new ScalePayload(PointCount: 3, Labels: new[] { "Poor", "Okay", "Great" }),
    };

    private static Question SelectQuestion() => new()
    {
        Id = SelectQuestionId,
        SurveyId = SurveyId,
        SectionId = SectionId,
        Type = QuestionType.SingleSelect,
        Subtype = QuestionSubType.List,
        Text = "Which branch did you visit?",
        Comments = false,
        TypePayload = new SingleSelectPayload(new[] { "Downtown", "Airport" }),
    };

    private static TranslationBundle Extract(Survey survey, IReadOnlyList<Section> sections, IReadOnlyList<Question> questions) =>
        new TranslatableStringExtractor().Extract(survey, sections, questions);

    [Fact]
    public void Extract_stamps_the_bundle_with_the_english_source_locale()
    {
        var bundle = Extract(SurveyWithMessages(), new[] { OnlySection() }, new[] { ScaleQuestion() });

        bundle.Locale.Should().Be(TranslatableStringExtractor.SourceLocale);
        bundle.Locale.Should().Be("en");
    }

    [Fact]
    public void Extract_emits_survey_name_welcome_and_thanks_keys()
    {
        var bundle = Extract(SurveyWithMessages(), Array.Empty<Section>(), Array.Empty<Question>());

        bundle.Keys.Should().Contain("survey.name", "Post-visit survey");
        bundle.Keys.Should().Contain("survey.welcome", "<p>Welcome</p>");
        bundle.Keys.Should().Contain("survey.thanks", "<p>Thank you</p>");
    }

    [Fact]
    public void Extract_omits_welcome_and_thanks_keys_when_they_are_empty()
    {
        var survey = SurveyWithMessages();
        survey.WelcomeHtml = null;
        survey.ThanksHtml = "";

        var bundle = Extract(survey, Array.Empty<Section>(), Array.Empty<Question>());

        bundle.Keys.Should().ContainKey("survey.name");
        bundle.Keys.Should().NotContainKey("survey.welcome");
        bundle.Keys.Should().NotContainKey("survey.thanks");
    }

    [Fact]
    public void Extract_emits_section_title_keys_from_the_section_name()
    {
        var bundle = Extract(SurveyWithMessages(), new[] { OnlySection() }, Array.Empty<Question>());

        bundle.Keys.Should().Contain($"section.{SectionId}.title", "Overall experience");
    }

    [Fact]
    public void Extract_emits_question_text_and_description_keys()
    {
        var bundle = Extract(SurveyWithMessages(), new[] { OnlySection() }, new[] { ScaleQuestion() });

        bundle.Keys.Should().Contain($"question.{ScaleQuestionId}.text", "How satisfied were you?");
        bundle.Keys.Should().Contain($"question.{ScaleQuestionId}.description", "Rate your overall satisfaction");
    }

    [Fact]
    public void Extract_emits_indexed_scale_label_keys()
    {
        var bundle = Extract(SurveyWithMessages(), new[] { OnlySection() }, new[] { ScaleQuestion() });

        bundle.Keys.Should().Contain($"question.{ScaleQuestionId}.scale_labels.0", "Poor");
        bundle.Keys.Should().Contain($"question.{ScaleQuestionId}.scale_labels.1", "Okay");
        bundle.Keys.Should().Contain($"question.{ScaleQuestionId}.scale_labels.2", "Great");
    }

    [Fact]
    public void Extract_emits_indexed_option_label_keys_for_select_questions()
    {
        var bundle = Extract(SurveyWithMessages(), new[] { OnlySection() }, new[] { SelectQuestion() });

        bundle.Keys.Should().Contain($"question.{SelectQuestionId}.options.0.label", "Downtown");
        bundle.Keys.Should().Contain($"question.{SelectQuestionId}.options.1.label", "Airport");
    }

    [Fact]
    public void Extract_emits_the_comment_label_key_only_when_comments_are_enabled()
    {
        var bundle = Extract(
            SurveyWithMessages(),
            new[] { OnlySection() },
            new[] { ScaleQuestion(), SelectQuestion() });

        // ScaleQuestion has Comments = true → its comment label is translatable.
        bundle.Keys.Should().Contain($"question.{ScaleQuestionId}.comment_label", "Additional comments");
        // SelectQuestion has Comments = false → no comment-label key.
        bundle.Keys.Should().NotContainKey($"question.{SelectQuestionId}.comment_label");
    }

    [Fact]
    public void Extract_covers_every_localisable_string_across_the_full_graph()
    {
        var bundle = Extract(
            SurveyWithMessages(),
            new[] { OnlySection() },
            new[] { ScaleQuestion(), SelectQuestion() });

        // FR-11.1 — the workspace must expose every localisable string. Assert the full key set.
        bundle.Keys.Keys.Should().BeEquivalentTo(new[]
        {
            "survey.name",
            "survey.welcome",
            "survey.thanks",
            $"section.{SectionId}.title",
            $"question.{ScaleQuestionId}.text",
            $"question.{ScaleQuestionId}.description",
            $"question.{ScaleQuestionId}.scale_labels.0",
            $"question.{ScaleQuestionId}.scale_labels.1",
            $"question.{ScaleQuestionId}.scale_labels.2",
            $"question.{ScaleQuestionId}.comment_label",
            $"question.{SelectQuestionId}.text",
            $"question.{SelectQuestionId}.options.0.label",
            $"question.{SelectQuestionId}.options.1.label",
        });
    }
}
