namespace Nabadat.SurveyBuilder.Application.QuestionsSets;

/// <summary>Result of <c>QuestionsSetValidator.Validate</c> (T139): validity + API-05 error codes.</summary>
public sealed record QuestionsSetValidationResult(bool IsValid, IReadOnlyList<string> Errors);
