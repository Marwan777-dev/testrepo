namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>A cursor-paginated page of audit events (API-04 cursor pagination).</summary>
public sealed record AuditLogPage
{
    public required IReadOnlyList<AuditLogEntry> Items { get; init; }

    /// <summary>Opaque cursor for the next page; null when there are no more results.</summary>
    public string? NextCursor { get; init; }
}
