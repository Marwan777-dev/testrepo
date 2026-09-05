namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>POST /api/v1/auth/mfa/enroll/confirm</c>.</summary>
public sealed record MfaEnrollConfirmRequest
{
    public string EnrollmentToken { get; init; } = string.Empty;

    public string TotpCode { get; init; } = string.Empty;
}
