namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;

/// <summary>
/// An M-10 tenant user inserted by the M-16 fixture so journey endpoints can be driven
/// as an authenticated actor. <see cref="Base32Secret"/> is the plaintext TOTP secret
/// (present only for MFA-enrolled users) so the test can compute live codes; it is never
/// exposed by the production API.
/// </summary>
public sealed record SeededUser(Guid UserId, string Username, string Password, string? Base32Secret);
