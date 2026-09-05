using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Permissions.Interfaces;

/// <summary>
/// Context-holding data-access service over <c>permission_module_assignments</c> (tenant
/// schema, EF / <c>TenantDbContext</c>). Replaces the tenant-side half of the old
/// raw-Npgsql <c>IPermissionRepository</c>; persona baselines (control-plane) live in a
/// separate control-plane service.
/// </summary>
public interface IPermissionModuleAssignmentService
{
    /// <summary>All module assignments for a user, ordered by module id.</summary>
    Task<IReadOnlyList<PermissionModuleAssignment>> GetAssignmentsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Replaces a user's module assignments (delete-then-insert) and saves. The delete
    /// precedes the inserts so the unique <c>(user_id, module_id)</c> constraint is never
    /// violated. Compose inside <c>ITenantDbContext.ExecuteAsync</c> to make this
    /// atomic with other writes.
    /// </summary>
    Task ReplaceAssignmentsAsync(
        Guid userId,
        IReadOnlyList<PermissionModuleAssignment> assignments,
        CancellationToken ct = default);
}
