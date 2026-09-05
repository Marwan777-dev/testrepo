namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>Result of <c>QuestionValidator.Validate</c> (T075): validity + API-05 error codes.</summary>
public sealed record QuestionValidationResult(bool IsValid, IReadOnlyList<string> Errors);
