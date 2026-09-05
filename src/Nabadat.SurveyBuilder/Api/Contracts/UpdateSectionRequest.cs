using Nabadat.SurveyBuilder.Application.Sections.Dtos;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>PATCH /api/v1/surveys/{id}/sections/{sectionId} body (contracts/sections-and-sets.md).</summary>
public sealed record UpdateSectionRequest(string? Name, string? Description, int? Order)
{
    public SectionWriteModel ToWriteModel(Guid surveyId) => new()
    {
        SurveyId = surveyId,
        Name = Name ?? string.Empty,
        Description = Description,
        Order = Order,
    };
}
