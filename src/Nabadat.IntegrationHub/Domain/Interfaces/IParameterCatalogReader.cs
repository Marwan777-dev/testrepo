namespace Nabadat.IntegrationHub.Domain.Interfaces;

/// <summary>
/// The tenant's enabled parameter catalogue, <b>published</b> by M-13 for future rule / action / journey
/// builders (M-14 / M-15 / M-16) that may reference M-13 parameters (CMC-07).
///
/// <para>Referencing a parameter through this reader makes that reference part of the BR-10
/// impact-warning guard when the parameter is later disabled — the disable flow scans consumers, it does
/// not silently break them.</para>
///
/// <para><b>Forward contract / skeleton only</b> (research.md §4.7, mirroring M-15's
/// <c>IActionOverlayReader</c> precedent): <b>no consumer exists yet</b> and M-13 ships no implementation
/// for it in this phase. M-14/15/16's current data-scope needs are served through M-10 directly, via the
/// real CMC-06 integration. It lives here now so the contract is published and reviewable before a
/// consumer arrives; the implementing class lands with that first consumer.</para>
/// </summary>
public interface IParameterCatalogReader
{
    /// <summary>Returns every <c>enabled</c> parameter in the current tenant, built-in and custom alike.</summary>
    Task<IReadOnlyList<ParameterCatalogEntry>> GetEnabledParametersAsync(CancellationToken ct = default);
}
