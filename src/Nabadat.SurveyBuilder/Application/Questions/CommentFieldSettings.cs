namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// The materialised comment-field settings produced by <c>CommentFieldFlagPolicy.Apply</c> (T078,
/// FR-8.9): an enabled comment field is optional, capped at 200 chars, labelled "Comments"
/// (translatable), and travels to NLP.
/// </summary>
public sealed record CommentFieldSettings(
    bool HasCommentField,
    bool CommentRequired,
    int CommentMaxLength,
    string CommentLabel,
    bool CommentTravelsToNlp);
