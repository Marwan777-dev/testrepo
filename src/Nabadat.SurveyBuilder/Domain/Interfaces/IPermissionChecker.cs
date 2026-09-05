namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// Cross-module port M-01 consumes from <b>M-10 (User &amp; Role Management)</b> to test tenant-scoped
/// permission grants (per architecture Article 3, published-interface only — M-01 never references
/// M-10's concrete types). Used by <c>PublishAuthorizationService</c> (T115) to check the
/// <c>PublishOwnSurveys</c> grant (FR-15.5, BR-15.2).
/// <para><b>Declared here per T020;</b> the concrete implementation is supplied by M-10 and wired in
/// the host composition root. Until then no runtime path in US2 resolves it — the unit-tested
/// services take it as a mockable dependency.</para>
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// Returns <c>true</c> when the user identified by <paramref name="userId"/> holds the named
    /// <paramref name="grant"/> in the current tenant.
    /// </summary>
    Task<bool> HasGrantAsync(Guid userId, string grant, CancellationToken ct);
}
