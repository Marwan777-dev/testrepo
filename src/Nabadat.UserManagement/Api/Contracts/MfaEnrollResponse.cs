namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Successful <c>POST /api/v1/auth/mfa/enroll</c> response.</summary>
public sealed record MfaEnrollResponse
{
    public required string OtpauthUri { get; init; }

    public required string Base32Secret { get; init; }

    public required string EnrollmentToken { get; init; }
}
