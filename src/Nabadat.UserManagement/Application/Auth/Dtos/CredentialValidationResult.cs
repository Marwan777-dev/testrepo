namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>
/// Result of <c>TenantAuthenticationService.ValidateCredentialsAsync</c>. On success
/// a short-lived <see cref="ChallengeId"/> gates the MFA step; a locked account
/// instead throws <c>AccountLockedException</c>.
/// </summary>
public sealed record CredentialValidationResult
{
    public required CredentialOutcome Outcome { get; init; }

    /// <summary>Opaque challenge id for the MFA step (set when credentials are valid).</summary>
    public string? ChallengeId { get; init; }

    public static CredentialValidationResult InvalidCredentials() =>
        new() { Outcome = CredentialOutcome.InvalidCredentials };

    public static CredentialValidationResult ChallengeIssued(string challengeId) =>
        new() { Outcome = CredentialOutcome.ChallengeIssued, ChallengeId = challengeId };

    public static CredentialValidationResult RequiresMfaEnrollment(string challengeId) =>
        new() { Outcome = CredentialOutcome.RequiresMfaEnrollment, ChallengeId = challengeId };
}
