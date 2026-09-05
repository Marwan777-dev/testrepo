namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>
/// Outcome of a TOTP verification. <see cref="MatchedStep"/> is the UNIX epoch
/// time-step the code matched (used for anti-replay: callers persist it as
/// <c>tenant_users.last_used_totp_step</c> and reject any step ≤ the last accepted).
/// </summary>
public sealed record TotpVerificationResult
{
    public required bool IsValid { get; init; }

    public required long MatchedStep { get; init; }

    public static TotpVerificationResult Invalid() => new() { IsValid = false, MatchedStep = 0 };
}
