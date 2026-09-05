namespace Nabadat.SurveyBuilder.Application.HtmlSanitisation;

/// <summary>
/// The result of sanitising a rich-text fragment: the safe <see cref="Html"/> that gets persisted
/// (never the raw input) and <see cref="WasModified"/> — true when the sanitiser stripped at least
/// one tag/attribute/URL, which the API surfaces so the editor can show a "content was cleaned"
/// notice.
/// </summary>
public sealed record SanitisedResult(string Html, bool WasModified);
