using System.Collections.Concurrent;
using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// Integration-test <see cref="IPermissionChecker"/> with per-user grant control (the production
/// wiring is M-10's host adapter — the module default is <c>DenyAllPermissionChecker</c>, see
/// TODO-M01-014). Denies every grant by default (so a P-03 must route through review); a test calls
/// <see cref="AllowGrant"/> to hand a specific user the <c>PublishOwnSurveys</c> grant and exercise
/// the FR-15.5 self-publish path. Registered as a singleton by <see cref="SurveyBuilderApplicationFactory"/>.
/// </summary>
public sealed class StubPermissionChecker : IPermissionChecker
{
    private readonly ConcurrentDictionary<string, bool> _grants = new();

    /// <summary>Grants <paramref name="grant"/> to <paramref name="userId"/> for subsequent checks.</summary>
    public void AllowGrant(Guid userId, string grant) => _grants[Key(userId, grant)] = true;

    public Task<bool> HasGrantAsync(Guid userId, string grant, CancellationToken ct) =>
        Task.FromResult(_grants.TryGetValue(Key(userId, grant), out var allowed) && allowed);

    private static string Key(Guid userId, string grant) => $"{userId:N}:{grant}";
}
