using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.KpiManagement.Domain.Entities;

/// <summary>
/// Root KPI definition entity (tenant-schema table <c>kpi_definitions</c>, data-model.md §1.1).
/// One row per KPI — the eight platform-seeded standard KPIs plus any tenant-authored custom
/// KPIs. Enum-constrained columns are modelled as <see langword="string"/> (per the M-16
/// reference); the type-safe twins live in <c>Domain/ValueObjects/</c> and are applied at the
/// service / published-interface boundary. No <c>tenant_id</c> column — isolation is schema-level
/// (DB-02 / AD-02).
/// </summary>
public sealed class KpiDefinition
{
    public Guid Id { get; set; }

    /// <summary>Short code, ≤ 20 chars; case-insensitively unique per tenant; immutable after first save (FR-004).</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Display name, ≤ 100 chars.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary><c>Standard</c> | <c>Custom</c> (see <see cref="ValueObjects.KpiType"/>).</summary>
    public KpiType KpiType { get; set; }

    /// <summary>True only for the CXI composite KPI; drives the NULL-scale / NULL-representation invariants.</summary>
    public bool IsComposite { get; set; }

    /// <summary><c>WeightedAverage</c> | <c>TopNBox</c> | <c>NPSStandard</c> | <c>WeightedComposite</c> (see <see cref="ValueObjects.CalculationMethod"/>).</summary>
    public CalculationMethod CalculationMethod { get; set; } 

    /// <summary>Top-N boxes count; required when <see cref="CalculationMethod"/> is <c>TopNBox</c>, NULL otherwise.</summary>
    public short? TopNValue { get; set; }

    /// <summary>Response scale (see <see cref="ValueObjects.Scale"/>); NULL for the composite KPI.</summary>
    public Scale? Scale { get; set; }

    /// <summary>Min-scale anchor description (English), ≤ 60 chars; optional.</summary>
    public string? MinScaleDescriptionEn { get; set; }

    /// <summary>Min-scale anchor description (Arabic), ≤ 60 chars; optional.</summary>
    public string? MinScaleDescriptionAr { get; set; }

    /// <summary>Max-scale anchor description (English), ≤ 60 chars; optional.</summary>
    public string? MaxScaleDescriptionEn { get; set; }

    /// <summary>Max-scale anchor description (Arabic), ≤ 60 chars; optional.</summary>
    public string? MaxScaleDescriptionAr { get; set; }

    /// <summary>Question rendering style (see <see cref="ValueObjects.RepresentationStyle"/>); NULL for the composite KPI.</summary>
    public RepresentationStyle? RepresentationStyle { get; set; }

    /// <summary>Emoji glyph family (see <see cref="ValueObjects.EmojiSet"/>); set only when <see cref="RepresentationStyle"/> is <c>Emoji</c>.</summary>
    public EmojiSet? EmojiSet { get; set; }

    /// <summary>Target value; required when <see cref="IsActive"/> is true. Range per type (0..100; −100..+100 for NPS).</summary>
    public decimal? Target { get; set; }

    /// <summary>Active KPIs participate in scoring and may be shown on the dashboard.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Surfaces the KPI on the dashboard; forced false whenever <see cref="IsActive"/> is false.</summary>
    public bool ShowOnDashboard { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>M-10 user id of the author (logical ref; not an enforced FK in the tenant schema).</summary>
    public Guid CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>M-10 user id of the last editor (logical ref; not an enforced FK).</summary>
    public Guid UpdatedBy { get; set; }
}
