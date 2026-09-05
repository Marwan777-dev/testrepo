namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>POST /api/v1/auth/mfa/verify</c>.</summary>
public sealed record MfaVerifyRequest
{
    public string ChallengeId { get; init; } = string.Empty;

    public string TotpCode { get; init; } = string.Empty;
}
