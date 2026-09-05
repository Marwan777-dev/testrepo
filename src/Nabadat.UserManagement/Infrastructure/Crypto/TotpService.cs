using System.Net;
using OtpNet;

namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>
/// <see cref="ITotpService"/> backed by OTP.NET. Verification allows ±1 time step
/// (≈30 s either side of clock skew) and enforces anti-replay via the matched step.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const string Issuer = "Nabadat";

    /// <summary>20 bytes = 160-bit secret, the RFC 4226 recommended length.</summary>
    private const int SecretByteLength = 20;

    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    public string GenerateSecret() =>
        Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(SecretByteLength));

    public string GetOtpUri(string username, string base32Secret)
    {
        var label = WebUtility.UrlEncode($"{Issuer}:{username}");
        var issuer = WebUtility.UrlEncode(Issuer);
        return $"otpauth://totp/{label}?secret={base32Secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
    }

    public TotpVerificationResult VerifyCode(string base32Secret, string code, long? lastUsedStep)
    {
        var secretBytes = Base32Encoding.ToBytes(base32Secret);
        var totp = new Totp(secretBytes);

        var isValid = totp.VerifyTotp(code, out var matchedStep, Window);
        if (!isValid)
        {
            return TotpVerificationResult.Invalid();
        }

        // Anti-replay: the same (or an earlier) step must not be accepted twice.
        if (lastUsedStep.HasValue && matchedStep <= lastUsedStep.Value)
        {
            return TotpVerificationResult.Invalid();
        }

        return new TotpVerificationResult { IsValid = true, MatchedStep = matchedStep };
    }
}
