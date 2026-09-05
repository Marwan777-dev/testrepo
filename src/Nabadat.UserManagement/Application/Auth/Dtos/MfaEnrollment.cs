namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>A pending TOTP enrollment resolved from an enrollment token (holds the not-yet-confirmed secret).</summary>
public sealed record MfaEnrollment
{
    public required Guid UserId { get; init; }

    /// <summary>The Base32 TOTP secret generated for enrollment; persisted (encrypted) only on confirm.</summary>
    public required string Base32Secret { get; init; }
}
