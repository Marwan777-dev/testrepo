namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>POST /api/v1/templates/{id}/rebuild-from-survey body (contracts/templates.md).</summary>
public sealed record RebuildTemplateRequest(Guid SourceSurveyId);
