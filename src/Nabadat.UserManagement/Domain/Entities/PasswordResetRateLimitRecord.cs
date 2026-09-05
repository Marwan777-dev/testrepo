namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// Application-layer rate-limit state for self-service password reset (tenant-schema
/// table <c>password_reset_rate_limit_records</c>). Keyed by a hash of the
/// normalized email so the raw address is never stored.
/// </summary>
public sealed class PasswordResetRateLimitRecord
{
    /// <summary>SHA-256(normalize(email) ‖ tenantId).</summary>
    public byte[] EmailHash { get; set; } = [];

    public DateTimeOffset WindowStartUtc { get; set; }

    public short RequestCount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
