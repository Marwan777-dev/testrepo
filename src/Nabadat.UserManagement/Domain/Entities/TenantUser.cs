using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// A user within a tenant boundary (tenant-schema table <c>tenant_users</c>).
/// Per DB-02/AD-02 there is intentionally NO <c>TenantId</c> property — isolation
/// is at the PostgreSQL schema level.
/// </summary>
public sealed class TenantUser
{
    public Guid UserId { get; set; }

    /// <summary>Email address; unique within the schema.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>bcrypt hash (cost ≥ 12). Never plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsMfaEnrolled { get; set; }

    /// <summary>Envelope-encrypted (AES-256-GCM) TOTP secret; null until enrolled.</summary>
    public byte[]? MfaSecretEncrypted { get; set; }

    /// <summary>KMS key id (SaaS) or config key name (on-prem); null until enrolled.</summary>
    public string? MfaSecretKeyRef { get; set; }

    /// <summary>UNIX epoch step number of the last accepted TOTP code (anti-replay).</summary>
    public long? LastUsedTotpStep { get; set; }

    /// <summary>Persona <c>P-01</c>..<c>P-08</c>.</summary>
    public string Persona { get; set; } = string.Empty;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public short FailedAttemptCount { get; set; }

    public DateTimeOffset? LockedUntilUtc { get; set; }

    /// <summary>Assigned organization hierarchy node (scope); null when unscoped.</summary>
    public Guid? OrganizationNodeId { get; set; }

    /// <summary>Incremented on every permission change; drives snapshot invalidation.</summary>
    public long LastPermissionSnapshotVersion { get; set; }

    /// <summary>Set true by an admin-triggered password reset.</summary>
    public bool RequiresPasswordChange { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
