using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>Section view (contracts/sections-and-sets.md). Exposes <see cref="RowVersion"/> for the ETag.</summary>
public sealed record SectionView(
    Guid Id,
    Guid SurveyId,
    string Name,
    string? Description,
    int Order,
    int RowVersion)
{
    public static SectionView From(Section s) =>
        new(s.Id, s.SurveyId, s.Name, s.Description, s.Order, s.RowVersion);
}
