namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>
/// Result of a permission check. Returned by
/// <see cref="Interfaces.IUserManagementPermissionService"/> at every module action boundary.
/// </summary>
public sealed record PermissionDecision
{
    public required bool IsAllowed { get; init; }

    /// <summary>Machine-readable reason when <see cref="IsAllowed"/> is false (e.g. <c>module.not_assigned</c>).</summary>
    public string? DeniedReason { get; init; }

    public static PermissionDecision Allowed() => new() { IsAllowed = true };

    public static PermissionDecision Denied(string reason) => new() { IsAllowed = false, DeniedReason = reason };
}
