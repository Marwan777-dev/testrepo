using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Domain.Interfaces;

/// <summary>
/// <b>Published interface (AD-01).</b> The contract every module's action boundary
/// uses to authorize an operation. Backed by the in-process permission snapshot
/// (AD-03 — no cache layer), so a snapshot-hit check is a memory read.
/// </summary>
public interface IUserManagementPermissionService
{
    /// <summary>
    /// Decides whether <paramref name="userId"/> may perform <paramref name="action"/>,
    /// optionally on a specific <paramref name="entityId"/> (for entity/scope checks).
    /// </summary>
    Task<PermissionDecision> CheckPermissionAsync(
        Guid userId,
        string action,
        Guid? entityId = null,
        CancellationToken ct = default);

    /// <summary>Returns the current effective permission snapshot for a user.</summary>
    Task<PermissionSnapshot> GetPermissionSnapshotAsync(Guid userId, CancellationToken ct = default);
}
