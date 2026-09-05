using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// PUT /api/v1/surveys/{id}/sections/{sectionId}/questions/{questionId} body (contracts/questions.md).
/// Placement (section/set/order) is changed via the move endpoint, not here.
/// </summary>
public sealed record UpdateQuestionRequest(
    string? Text,
    string? Description,
    QuestionType Type,
    QuestionSubType SubType,
    int? SliderSteps,
    bool Required,
    bool ShowComments,
    bool Sentiment,
    KpiBinding? Binding,
    QuestionTypePayload Payload)
{
    public QuestionWriteModel ToWriteModel(Guid surveyId, Guid sectionId) => new()
    {
        SurveyId = surveyId,
        SectionId = sectionId,
        Text = Text ?? string.Empty,
        Description = Description,
        Type = Type,
        SubType = SubType,
        SliderSteps = SliderSteps,
        Required = Required,
        ShowComments = ShowComments,
        Sentiment = Sentiment,
        Binding = Binding,
        Payload = Payload,
    };
}
