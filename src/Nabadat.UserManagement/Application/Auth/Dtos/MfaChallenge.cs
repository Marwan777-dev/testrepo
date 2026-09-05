namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>A pending post-password MFA challenge resolved from a challenge id.</summary>
public sealed record MfaChallenge
{
    public required Guid UserId { get; init; }

    /// <summary>True when this challenge leads to enrollment rather than verification.</summary>
    public required bool RequiresEnrollment { get; init; }
}
