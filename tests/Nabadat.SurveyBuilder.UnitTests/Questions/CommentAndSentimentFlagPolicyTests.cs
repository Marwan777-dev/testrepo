using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Questions;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Questions;

/// <summary>
/// T048 [US1] — unit tests for <c>CommentFieldFlagPolicy</c> (FR-8.9) and <c>SentimentFlagPolicy</c>
/// (FR-8.11). Enabling comments materialises an optional, 200-char, "Comments"-labelled field that
/// travels to NLP; sentiment analysis only applies to Input Field Text/Paragraph — requesting it on
/// any other type is ignored with a warning.
/// <para>
/// Contract pinned for the implementer (T078):
/// <list type="bullet">
///   <item>Both policies live in <c>Application/Questions/</c> and are pure.</item>
///   <item><c>CommentFieldSettings CommentFieldFlagPolicy.Apply(bool showComments)</c> →
///   <c>CommentFieldSettings(bool HasCommentField, bool CommentRequired, int CommentMaxLength,
///   string CommentLabel, bool CommentTravelsToNlp)</c>. When <c>showComments</c> is true:
///   <c>HasCommentField=true, CommentRequired=false, CommentMaxLength=200, CommentLabel="Comments",
///   CommentTravelsToNlp=true</c>.</item>
///   <item><c>SentimentFlagResult SentimentFlagPolicy.Apply(QuestionType type, QuestionSubType subType,
///   bool requested)</c> → <c>SentimentFlagResult(bool Applied, IReadOnlyList&lt;string&gt; Warnings)</c>;
///   warning code <c>sentiment.ignored_for_non_text</c> for any type other than Input Field
///   Text/Paragraph.</item>
/// </list>
/// </para>
/// </summary>
public sealed class CommentAndSentimentFlagPolicyTests
{
    private readonly CommentFieldFlagPolicy _comments = new();
    private readonly SentimentFlagPolicy _sentiment = new();

    [Fact]
    public void CommentFieldFlagPolicy_materialises_an_optional_comments_field_when_enabled()
    {
        var settings = _comments.Apply(showComments: true);

        settings.HasCommentField.Should().BeTrue();
        settings.CommentRequired.Should().BeFalse();
        settings.CommentMaxLength.Should().Be(200);
        settings.CommentLabel.Should().Be("Comments");
        settings.CommentTravelsToNlp.Should().BeTrue();
    }

    [Fact]
    public void CommentFieldFlagPolicy_produces_no_comment_field_when_disabled()
    {
        var settings = _comments.Apply(showComments: false);

        settings.HasCommentField.Should().BeFalse();
    }

    [Fact]
    public void SentimentFlagPolicy_warns_and_does_not_apply_for_a_non_text_type()
    {
        var result = _sentiment.Apply(QuestionType.SingleSelect, QuestionSubType.None, requested: true);

        result.Applied.Should().BeFalse();
        result.Warnings.Should().Contain("sentiment.ignored_for_non_text");
    }

    [Theory]
    [InlineData(QuestionSubType.Text)]
    [InlineData(QuestionSubType.Paragraph)]
    public void SentimentFlagPolicy_applies_for_input_field_text_and_paragraph(QuestionSubType subType)
    {
        var result = _sentiment.Apply(QuestionType.InputField, subType, requested: true);

        result.Applied.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void SentimentFlagPolicy_warns_for_a_non_text_input_field_subtype()
    {
        var result = _sentiment.Apply(QuestionType.InputField, QuestionSubType.Number, requested: true);

        result.Applied.Should().BeFalse();
        result.Warnings.Should().Contain("sentiment.ignored_for_non_text");
    }
}
