namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// An immutable published snapshot of a journey configuration (tenant-schema table
/// <c>journey_versions</c>). Written once at publish time; never updated. The parent FK
/// uses ON DELETE RESTRICT so journeys with versions cannot be hard-deleted.
/// </summary>
public sealed class JourneyVersion
{
    public Guid VersionId { get; set; }

    /// <summary>Owning journey (FK → <c>journeys.journey_id</c> ON DELETE RESTRICT).</summary>
    public Guid JourneyId { get; set; }

    /// <summary>Sequential integer per journey, starting at 1; unique within the journey.</summary>
    public int VersionNumber { get; set; }

    /// <summary>M-10 <c>user_id</c> of the P-01 user who published (no FK across modules).</summary>
    public Guid PublishedBy { get; set; }

    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>
    /// Full journey tree captured at publish time, stored as opaque JSON (<c>jsonb</c>
    /// column). Written once; treated as immutable.
    /// </summary>
    public string SnapshotPayload { get; set; } = string.Empty;
}
