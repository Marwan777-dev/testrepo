namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>Envelope for the locales list (<c>{ "locales": [ … ] }</c>) returned by GET /translations.</summary>
public sealed record TranslationLocalesResponse(IReadOnlyList<LocaleSummary> Locales);
