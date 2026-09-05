namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>POST /api/v1/templates body (contracts/templates.md) — save a survey as a Customized template (FR-7.4).</summary>
public sealed record CreateTemplateRequest(
    Guid SourceSurveyId,
    string NameEn,
    string? NameAr = null,
    string? Description = null,
    string[]? Tags = null);
