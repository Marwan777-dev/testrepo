namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// A single-use, time-limited password-reset token (tenant-schema table
/// <c>password_reset_tokens</c>). Only the SHA-256 hash of the raw token is stored.
/// </summary>
public sealed class PasswordResetToken
{
    public Guid TokenId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the raw token.</summary>
    public byte[] TokenHash { get; set; } = [];

    /// <summary>Default issued + 30 min; configurable.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>Set on redemption; non-null means consumed.</summary>
    public DateTimeOffset? UsedAtUtc { get; set; }

    /// <summary>Admin-side revocation flag.</summary>
    public bool Revoked { get; set; }

    /// <summary><c>self-service</c> | <c>admin</c>.</summary>
    public string IssuedBy { get; set; } = string.Empty;

    /// <summary><c>email</c> | <c>sms</c> | <c>admin-api</c>.</summary>
    public string IssuedVia { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
