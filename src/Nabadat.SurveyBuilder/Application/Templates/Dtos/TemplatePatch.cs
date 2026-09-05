namespace Nabadat.SurveyBuilder.Application.Templates.Dtos;

/// <summary>
/// The editable metadata subset of a Customized template (contracts/templates.md PATCH /templates).
/// Any null field is left unchanged. Snapshot content is not editable via patch — use
/// rebuild-from-survey. Class + Primary sector are never authoring fields (FR-7.3).
/// </summary>
public sealed record TemplatePatch(
    string? NameEn = null,
    string? NameAr = null,
    string? Description = null,
    string[]? Tags = null,
    string? PreviewThumbnailFileHandle = null);
