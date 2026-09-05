using Nabadat.SurveyBuilder.Application.Questions.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// POST /api/v1/surveys/{id}/sections/{sectionId}/questions body (contracts/questions.md). The
/// per-type <see cref="Payload"/> deserialises polymorphically via its <c>$type</c> discriminator.
/// </summary>
public sealed record CreateQuestionRequest(
    Guid? SetId,
    string? Text,
    string? Description,
    QuestionType Type,
    QuestionSubType SubType,
    int? SliderSteps,
    bool Required,
    bool ShowComments,
    bool Sentiment,
    int Order,
    KpiBinding? Binding,
    QuestionTypePayload Payload)
{
    public QuestionWriteModel ToWriteModel(Guid surveyId, Guid sectionId) => new()
    {
        SurveyId = surveyId,
        SectionId = sectionId,
        SetId = SetId,
        Text = Text ?? string.Empty,
        Description = Description,
        Type = Type,
        SubType = SubType,
        SliderSteps = SliderSteps,
        Required = Required,
        ShowComments = ShowComments,
        Sentiment = Sentiment,
        Order = Order,
        Binding = Binding,
        Payload = Payload,
    };
}
