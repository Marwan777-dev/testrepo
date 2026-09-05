namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// An MFA-enrolled tenant user seeded by <see cref="SurveyBuilderApplicationFactory"/>. The
/// <see cref="Base32Secret"/> lets a test compute live TOTP codes to drive the real login flow;
/// <see cref="UserId"/> supports audit assertions by <c>actor_id</c>.
/// </summary>
public sealed record SeededUser(Guid UserId, string Username, string Password, string Base32Secret);
