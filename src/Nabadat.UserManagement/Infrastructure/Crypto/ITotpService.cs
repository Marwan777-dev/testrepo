namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>
/// TOTP (RFC 6238) operations for mandatory MFA. The Base32 secret produced by
/// <see cref="GenerateSecret"/> is envelope-encrypted (<see cref="IMfaSecretEncryptionService"/>)
/// before any persistence.
/// </summary>
public interface ITotpService
{
    /// <summary>Generates a new random Base32-encoded TOTP secret.</summary>
    string GenerateSecret();

    /// <summary>Builds the <c>otpauth://totp/...</c> provisioning URI for authenticator apps.</summary>
    string GetOtpUri(string username, string base32Secret);

    /// <summary>
    /// Verifies a code against the secret with ±1 step tolerance. Anti-replay: a
    /// match whose step is ≤ <paramref name="lastUsedStep"/> is rejected as a replay.
    /// </summary>
    TotpVerificationResult VerifyCode(string base32Secret, string code, long? lastUsedStep);
}
