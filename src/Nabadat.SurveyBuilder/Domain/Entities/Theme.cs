using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Domain.Entities;

/// <summary>
/// Per-survey appearance customisation (tenant-schema table <c>themes</c>, data-model.md §2.6, F4).
/// 1:1 with a survey, present only when <c>surveys.theme_mode = Customized</c>. Colour/typography
/// tokens are nullable — a null token inherits from the tenant design guidelines (M-11) at that
/// level. The advanced token groups are opaque jsonb the App layer shapes.
/// </summary>
public sealed class Theme
{
    public Guid Id { get; set; }

    /// <summary>Owning survey — unique (1:1), ON DELETE CASCADE.</summary>
    public Guid SurveyId { get; set; }

    /// <summary>Hex <c>#RRGGBB</c> or null (inherit at token level).</summary>
    public string? PrimaryColor { get; set; }

    public string? TextColor { get; set; }

    public int? ButtonRadiusPx { get; set; }

    public string? ButtonBorderColor { get; set; }

    public string? ButtonTextColor { get; set; }

    public bool HeaderShowLogo { get; set; } = true;

    public bool HeaderShowTitle { get; set; } = true;

    /// <summary><c>start</c> | <c>center</c> | <c>end</c>.</summary>
    public string HeaderAlignment { get; set; } = "start";

    public string? FooterText { get; set; }

    public BackgroundType BackgroundType { get; set; } = BackgroundType.Solid;

    /// <summary>Per-type background detail; <see cref="BackgroundType.Image"/> requires a file handle.</summary>
    public BackgroundConfig? BackgroundConfig { get; set; }

    /// <summary>0–100.</summary>
    public int BackgroundOpacity { get; set; } = 100;

    /// <summary>Per-D-level colour overrides (opaque jsonb).</summary>
    public string? AdvancedStatusColors { get; set; }

    /// <summary>Background/card/border surfaces (opaque jsonb).</summary>
    public string? AdvancedSurfaces { get; set; }

    /// <summary>Heading/body fonts (opaque jsonb).</summary>
    public string? AdvancedTypography { get; set; }

    /// <summary>Card radius, progress-bar style (opaque jsonb).</summary>
    public string? AdvancedLayout { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Monotonic ETag counter (research.md §2).</summary>
    public int RowVersion { get; set; } = 1;

    /// <summary>Bumps the ETag counter — call inside the write transaction on every mutation.</summary>
    public void IncrementRowVersion() => RowVersion++;
}
