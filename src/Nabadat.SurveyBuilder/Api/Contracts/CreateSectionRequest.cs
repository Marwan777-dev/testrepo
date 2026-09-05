using Nabadat.SurveyBuilder.Application.Sections.Dtos;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>POST /api/v1/surveys/{id}/sections body (contracts/sections-and-sets.md).</summary>
public sealed record CreateSectionRequest(Guid? Id, string? Name, string? Description, int? Order)
{
    public SectionWriteModel ToWriteModel(Guid surveyId) => new()
    {
        SurveyId = surveyId,
        Name = Name ?? string.Empty,
        Description = Description,
        Order = Order,
    };
}
