namespace Nabadat.SurveyBuilder.Application.Sections;

/// <summary>Result of <c>SectionValidator.Validate</c> (T137): validity + API-05 error codes.</summary>
public sealed record SectionValidationResult(bool IsValid, IReadOnlyList<string> Errors);
