namespace Nabadat.IntegrationHub.IntegrationTests.Infrastructure;

/// <summary>
/// A tenant user seeded by <see cref="IntegrationHubApplicationFactory.SeedEnrolledUserAsync"/>: the
/// credentials needed to drive the real login → MFA-verify flow, plus the id so a test can assert audit
/// rows by <c>actor_id</c>.
/// </summary>
/// <param name="UserId">M-10 user id — the <c>actor_id</c> on any M-17 event the user's action emits.</param>
/// <param name="Username">The seeded login (a unique e-mail).</param>
/// <param name="Password">The plaintext password, known only because the test seeded it.</param>
/// <param name="Base32Secret">The MFA secret, for computing a live TOTP code.</param>
public sealed record SeededUser(Guid UserId, string Username, string Password, string Base32Secret);
