namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// The kind of survey background (tenant-schema column <c>themes.background_type</c>, data-model.md
/// §2.6, F4). The accompanying <see cref="BackgroundConfig"/> jsonb carries the per-type detail;
/// <see cref="Image"/> requires a file handle and <see cref="Gradient"/> requires ≥2 stops
/// (enforced at the App layer by <c>AppearanceService</c>). Wire/DB form is the PascalCase name.
/// </summary>
public enum BackgroundType
{
    Solid,
    Gradient,
    Image,
    Pattern,
}
