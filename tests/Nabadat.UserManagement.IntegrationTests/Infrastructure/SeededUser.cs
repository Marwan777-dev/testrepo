namespace Nabadat.UserManagement.IntegrationTests.Infrastructure;

/// <summary>
/// A user inserted by the test fixture. <see cref="Base32Secret"/> is the plaintext
/// TOTP secret (present only for MFA-enrolled users) so the test can compute live
/// codes; it is never exposed by the production API.
/// </summary>
public sealed record SeededUser(Guid UserId, string Username, string Password, string? Base32Secret);
