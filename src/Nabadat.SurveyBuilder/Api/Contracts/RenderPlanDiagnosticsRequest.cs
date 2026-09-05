namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// POST /api/v1/surveys/{id}/render-plan diagnostics body (T150). Lets an admin simulate a specific
/// respondent (to reproduce the deterministic Random sample) and locale. Both are optional — a random
/// respondent id and the default locale are used when omitted.
/// </summary>
public sealed record RenderPlanDiagnosticsRequest(Guid? RespondentId, string? Locale);
