namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>POST /api/v1/auth/mfa/enroll</c>.</summary>
public sealed record MfaEnrollRequest
{
    public string ChallengeId { get; init; } = string.Empty;
}
