namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>POST /api/v1/auth/mfa/skip</c>.</summary>
public sealed record MfaSkipRequest
{
    public string ChallengeId { get; init; } = string.Empty;
}
