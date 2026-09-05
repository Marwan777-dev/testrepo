namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// POST /api/v1/templates/{id}/instantiate body (contracts/templates.md). All fields optional —
/// defaults are resolved from the snapshot; <see cref="NameEn"/> overrides the new survey's name.
/// </summary>
public sealed record InstantiateTemplateRequest(string? NameEn = null);
