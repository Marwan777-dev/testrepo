namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Per-type background detail (tenant-schema column <c>themes.background_config</c>, data-model.md
/// §2.6). Serialised to jsonb by <c>BackgroundConfigConverter</c> (T062). The populated fields
/// depend on the sibling <see cref="BackgroundType"/>:
/// <list type="bullet">
///   <item><see cref="BackgroundType.Solid"/> → <see cref="Color"/>.</item>
///   <item><see cref="BackgroundType.Gradient"/> → <see cref="Stops"/> (≥2) + <see cref="Angle"/>.</item>
///   <item><see cref="BackgroundType.Image"/> → <see cref="FileHandle"/> + <see cref="Opacity"/>.</item>
///   <item><see cref="BackgroundType.Pattern"/> → <see cref="PatternId"/> + <see cref="Color"/>.</item>
/// </list>
/// The shape is validated at the App layer (<c>AppearanceService</c>, T080); the full per-type
/// schema is finalised there — see data-model.md §2.6.
/// </summary>
/// <param name="Color">Solid / pattern fill colour (hex <c>#RRGGBB</c>).</param>
/// <param name="Stops">Gradient colour stops (hex), ≥2 for a valid gradient.</param>
/// <param name="Angle">Gradient angle in degrees.</param>
/// <param name="FileHandle">Opaque file-storage handle for an image background.</param>
/// <param name="Opacity">Image opacity 0–100.</param>
/// <param name="PatternId">Identifier of a built-in pattern.</param>
public sealed record BackgroundConfig(
    string? Color = null,
    IReadOnlyList<string>? Stops = null,
    int? Angle = null,
    string? FileHandle = null,
    int? Opacity = null,
    string? PatternId = null);
