namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Materialises the comment-field settings from the "Show comments" toggle (T078, FR-8.9). An
/// enabled field is optional, 200-char capped, labelled "Comments" (translatable), and travels to
/// NLP. Pure.
/// </summary>
public sealed class CommentFieldFlagPolicy
{
    private const int DefaultCommentMaxLength = 200;
    private const string DefaultCommentLabel = "Comments";

    public CommentFieldSettings Apply(bool showComments) =>
        showComments
            ? new CommentFieldSettings(
                HasCommentField: true,
                CommentRequired: false,
                CommentMaxLength: DefaultCommentMaxLength,
                CommentLabel: DefaultCommentLabel,
                CommentTravelsToNlp: true)
            : new CommentFieldSettings(
                HasCommentField: false,
                CommentRequired: false,
                CommentMaxLength: DefaultCommentMaxLength,
                CommentLabel: DefaultCommentLabel,
                CommentTravelsToNlp: false);
}
