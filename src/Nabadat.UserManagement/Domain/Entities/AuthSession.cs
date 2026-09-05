using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// An authenticated user session (tenant-schema table <c>auth_sessions</c>).
/// Append-only: never updated except to set <see cref="IsActive"/> false and bump
/// <see cref="LastActivityAtUtc"/>.
/// </summary>
public sealed class AuthSession
{
    public Guid SessionId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the raw opaque session token.</summary>
    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset IssuedAtUtc { get; set; }

    /// <summary>Hard expiry (default issued + 24h; configurable via M-11).</summary>
    public DateTimeOffset AbsoluteExpiresAtUtc { get; set; }

    /// <summary>Updated on every authenticated request (sliding window).</summary>
    public DateTimeOffset LastActivityAtUtc { get; set; }

    /// <summary>Tenant-configured sliding window in minutes (default 60).</summary>
    public short SlidingTtlMinutes { get; set; }

    /// <summary>Permission-snapshot version captured when the snapshot was last built.</summary>
    public long PermissionSnapshotVersion { get; set; }

    /// <summary>Serialized into the <c>permission_snapshot</c> jsonb column.</summary>
    public PermissionSnapshot PermissionSnapshot { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
