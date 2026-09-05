namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Successful <c>POST /api/v1/auth/login</c> response — an MFA challenge is pending.</summary>
public sealed record LoginResponse
{
    public required string ChallengeId { get; init; }

    public required bool RequiresMfaEnrollment { get; init; }
}
