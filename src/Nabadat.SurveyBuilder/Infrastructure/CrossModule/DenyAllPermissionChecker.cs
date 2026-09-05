using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.CrossModule;

/// <summary>
/// Placeholder <see cref="IPermissionChecker"/> until M-01 is wired to M-10's published permission
/// service in the host (T020). It denies every grant (returns <c>false</c>) — a safe default: a P-03
/// cannot self-publish without the real <c>PublishOwnSurveys</c> grant, so surveys route through the
/// P-01 review path in dev/E2E. Production MUST replace it with the M-10 adapter. Tracked as
/// TODO-M01-014.
/// </summary>
public sealed class DenyAllPermissionChecker : IPermissionChecker
{
    public Task<bool> HasGrantAsync(Guid userId, string grant, CancellationToken ct) => Task.FromResult(false);
}
