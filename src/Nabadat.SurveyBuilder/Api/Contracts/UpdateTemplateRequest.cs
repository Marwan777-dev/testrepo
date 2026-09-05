using Nabadat.SurveyBuilder.Application.Templates.Dtos;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// PATCH /api/v1/templates/{id} body (contracts/templates.md) — the editable metadata subset of a
/// Customized template. Any omitted (null) field is left unchanged. Maps to a <see cref="TemplatePatch"/>.
/// </summary>
public sealed record UpdateTemplateRequest(
    string? NameEn = null,
    string? NameAr = null,
    string? Description = null,
    string[]? Tags = null,
    string? PreviewThumbnailFileHandle = null)
{
    public TemplatePatch ToPatch() => new(NameEn, NameAr, Description, Tags, PreviewThumbnailFileHandle);
}
