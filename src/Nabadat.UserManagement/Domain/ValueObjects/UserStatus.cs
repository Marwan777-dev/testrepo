namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>
/// Lifecycle state of a <see cref="Entities.TenantUser"/>.
/// Wire/storage form is the lowercase string in <see cref="UserStatusExtensions"/>
/// (e.g. <c>pending-enrollment</c>); the persistence layer maps to/from these names.
/// </summary>
public enum UserStatus
{
    /// <summary>Normal, authenticatable user (<c>active</c>).</summary>
    Active,

    /// <summary>Soft-deleted; row retained for audit history (<c>inactive</c>).</summary>
    Inactive,

    /// <summary>Temporarily locked after repeated failed auth (<c>locked</c>).</summary>
    Locked,

    /// <summary>Created but has not yet enrolled MFA (<c>pending-enrollment</c>).</summary>
    PendingEnrollment,
}
