namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>Result discriminator for <see cref="CredentialValidationResult"/>.</summary>
public enum CredentialOutcome
{
    /// <summary>Username/password did not match. The caller returns 401 without revealing which field failed.</summary>
    InvalidCredentials,

    /// <summary>Credentials valid, MFA already enrolled — an MFA challenge was issued.</summary>
    ChallengeIssued,

    /// <summary>Credentials valid but MFA is not yet enrolled — the user must enroll before a session is created.</summary>
    RequiresMfaEnrollment,
}
