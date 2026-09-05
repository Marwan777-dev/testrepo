namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>
/// Output of <c>MfaEnrollmentService.InitiateEnrollmentAsync</c>: the data shown on
/// the enrollment screen (QR/URI + manual secret) and the token that gates confirm.
/// </summary>
public sealed record EnrollmentInitiation
{
    public required string OtpauthUri { get; init; }

    public required string Base32Secret { get; init; }

    public required string EnrollmentToken { get; init; }
}
