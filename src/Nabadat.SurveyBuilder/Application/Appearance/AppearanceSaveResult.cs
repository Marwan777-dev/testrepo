namespace Nabadat.SurveyBuilder.Application.Appearance;

/// <summary>Result of <c>AppearanceService.SaveAsync</c> (T080): validity + API-05 error codes.</summary>
public sealed record AppearanceSaveResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static AppearanceSaveResult Valid() => new(true, Array.Empty<string>());

    public static AppearanceSaveResult Invalid(params string[] errors) => new(false, errors);
}
