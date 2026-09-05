namespace Nabadat.TenantAdmin.Theming;

/// <summary>
/// A tenant brand-theme seed — the six <c>#RRGGBB</c> colors the frontend expands into the full CSS
/// variable set (mirrors the frontend <c>TenantThemeSeed</c>). <see cref="Primary"/>/
/// <see cref="Secondary"/>/<see cref="Neutral"/> are required; <see cref="Sidebar"/>/
/// <see cref="Accent"/>/<see cref="Background"/> are optional (null → the frontend derives them).
/// Deserialized from <c>tenant-themes.json</c> (case-insensitive property names).
/// </summary>
public sealed record ThemeColors(
    string Primary,
    string Secondary,
    string Neutral,
    string? Sidebar = null,
    string? Accent = null,
    string? Background = null);
