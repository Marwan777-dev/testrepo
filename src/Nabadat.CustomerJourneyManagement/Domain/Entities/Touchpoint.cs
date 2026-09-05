namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// Journey-local interaction point within a stage (tenant-schema table <c>touchpoints</c>).
/// A touchpoint with no <c>kpi_bindings</c> rows is "unmeasured" — excluded from score
/// computation and visually flagged in the UI.
/// </summary>
public sealed class Touchpoint
{
    public Guid TouchpointId { get; set; }

    /// <summary>Parent stage (FK → <c>stages.stage_id</c> ON DELETE CASCADE).</summary>
    public Guid StageId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Channel codes, e.g. <c>{IVR, Web, App, Email, Branch}</c>. Defaults to empty.</summary>
    public string[] Channels { get; set; } = [];

    /// <summary><c>Low</c> | <c>Medium</c> | <c>High</c> | <c>Critical</c>.</summary>
    public string Importance { get; set; } = "Medium";

    /// <summary>Moment-of-Truth flag — author-set; elevates priority in detection/reporting.</summary>
    public bool IsMot { get; set; }

    /// <summary>Mandatory touchpoints are always included in score calculation.</summary>
    public bool IsMandatory { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
