namespace Nabadat.KpiManagement.Application.Catalogue.Dtos;

/// <summary>
/// Application-layer projection of one catalogue row, produced by <see cref="KpiListItemMapper"/>.
/// Carries the human-readable <see cref="CalculationMethodLabel"/> / <see cref="ScaleLabel"/> the
/// table renders, alongside the raw enum names (projected to their PascalCase member name so the
/// value is unambiguous). This is a pure app DTO — the controller maps it to the wire contract
/// (<c>Api/Contracts/KpiListItemResponse</c>); it carries no serialization attributes.
/// </summary>
public sealed record KpiListItemDto
{
    public Guid Id { get; init; }

    public string ShortName { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string KpiType { get; init; } = string.Empty;

    public bool IsComposite { get; init; }

    /// <summary>Scale member name, or <c>null</c> for the composite KPI.</summary>
    public string? Scale { get; init; }

    public string CalculationMethod { get; init; } = string.Empty;

    /// <summary>Human-readable calculation method (e.g. <c>"NPS Standard"</c>, <c>"Weighted Average"</c>).</summary>
    public string CalculationMethodLabel { get; init; } = string.Empty;

    /// <summary>Human-readable scale (e.g. <c>"0–10"</c>); <c>"—"</c> for the composite KPI.</summary>
    public string ScaleLabel { get; init; } = string.Empty;

    public decimal? Target { get; init; }

    public bool IsActive { get; init; }

    public bool ShowOnDashboard { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
