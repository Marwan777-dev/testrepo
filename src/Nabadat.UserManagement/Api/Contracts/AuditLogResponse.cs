namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Cursor-paginated response for <c>GET /api/v1/audit-log</c> (API-04 cursor pagination).</summary>
public sealed record AuditLogResponse
{
    public required IReadOnlyList<AuditLogEntryResponse> Items { get; init; }

    /// <summary>Opaque cursor for the next page; <c>null</c> when there are no more results.</summary>
    public string? NextPageToken { get; init; }

    /// <summary>
    /// Total matching events; <c>null</c> under cursor pagination — the reader returns a
    /// page + next-cursor and does not compute a full count.
    /// </summary>
    public long? TotalCount { get; init; }
}
