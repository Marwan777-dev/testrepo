using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Domain.Interfaces;

/// <summary>
/// <b>M-10-owned read port for the tenant audit trail.</b> M-10 both writes its audit
/// events to the tenant-schema <c>event_log</c> (via <c>IUserManagementEventPublisher</c>, in the
/// same transaction as the business change) and reads them back through this contract —
/// it owns the whole audit cycle for its own events (SRS §6; resolves gap-analysis
/// I-02/I-04). The previously-planned external M-17 Audit module is not part of this
/// scope.
/// </summary>
public interface IAuditLogReader
{
    /// <summary>
    /// Returns a cursor-paginated page of audit events for the tenant, filtered by
    /// <paramref name="filter"/>, ordered newest-first, starting after
    /// <paramref name="cursor"/> and capped at <paramref name="pageSize"/> rows.
    /// </summary>
    Task<AuditLogPage> QueryEventsAsync(
        AuditLogFilter filter,
        int pageSize,
        string? cursor,
        CancellationToken ct = default);
}
