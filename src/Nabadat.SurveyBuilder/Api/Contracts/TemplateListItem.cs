using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>One F6 template-picker / Templates-tab card (contracts/templates.md GET /templates).</summary>
public sealed record TemplateListItem(
    Guid Id,
    TemplateClass Class,
    string NameEn,
    string? NameAr,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Sectors,
    string? PreviewThumbnailFileHandle,
    DateTimeOffset UpdatedAt)
{
    public static TemplateListItem From(Template t) => new(
        t.Id, t.Class, t.NameEn, t.NameAr, t.Description, t.Tags, t.Sectors, t.PreviewThumbnailFileHandle, t.UpdatedAt);
}
