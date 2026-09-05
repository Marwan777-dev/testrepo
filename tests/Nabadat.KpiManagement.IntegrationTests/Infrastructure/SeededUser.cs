namespace Nabadat.KpiManagement.IntegrationTests.Infrastructure;

/// <summary>
/// A tenant user seeded by <see cref="KpiManagementApplicationFactory"/> for an authenticated test.
/// <see cref="Base32Secret"/> is the MFA TOTP secret so the test can compute live codes during the
/// login → MFA-verify flow; <see cref="UserId"/> is the audit <c>actor_id</c>.
/// </summary>
public sealed record SeededUser(Guid UserId, string Username, string Password, string? Base32Secret);
