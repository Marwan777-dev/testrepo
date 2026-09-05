using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Result of <c>KpiBindingValidator.Validate</c> (T076): validity, error/warning codes, and the
/// <see cref="Normalised"/> binding after stripping stage/touchpoint that are ignored when the
/// journey binding is off (BR-8.2).
/// </summary>
public sealed record KpiBindingValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    KpiBinding Normalised);
