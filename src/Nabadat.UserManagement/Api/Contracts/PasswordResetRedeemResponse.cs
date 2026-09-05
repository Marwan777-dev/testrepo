namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Successful <c>POST /api/v1/auth/password-reset/redeem</c> response.</summary>
public sealed record PasswordResetRedeemResponse
{
    public required bool RequiresMfaReenrollment { get; init; }
}
