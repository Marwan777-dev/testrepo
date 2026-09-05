namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Appearance source for a survey (tenant-schema column <c>surveys.theme_mode</c>, data-model.md
/// §2.1, F4). <see cref="Inherited"/> resolves every token from the tenant design guidelines
/// (M-11) and locks the controls; <see cref="Customized"/> unlocks them and persists a
/// per-survey <c>themes</c> row. Wire/DB form is the PascalCase member name.
/// </summary>
public enum ThemeMode
{
    /// <summary>Use the tenant design guidelines — controls locked (default).</summary>
    Inherited,

    /// <summary>Per-survey customisation — a <c>themes</c> row is present.</summary>
    Customized,
}
