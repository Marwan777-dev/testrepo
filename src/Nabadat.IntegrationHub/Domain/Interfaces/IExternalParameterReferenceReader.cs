using Nabadat.IntegrationHub.Application.Parameters;

namespace Nabadat.IntegrationHub.Domain.Interfaces;

/// <summary>
/// M-13-owned consumer-side port for BR-10's <b>external</b> half: which M-10 data-scope filters (CMC-06) and
/// M-14/15/16 rules or actions (CMC-07) currently reference a given parameter, so the impact warning
/// (Dialog D-6) can list them before the user disables it.
///
/// <para>It is a port rather than a query because those references live in <b>other modules' tables</b>, reached
/// only through published contracts (architecture-constitution Article 4.1 — identifier-only, no cross-module
/// FKs). M-13 reads its own <c>channel_parameter_assignments</c> directly; these two kinds it must ask for.</para>
///
/// <para><b>No provider exists yet.</b> M-10 publishes <c>IDataScopeService</c>, but that interface can only
/// answer "what scope does user X have?", not "who references parameter P?" — the reverse index M-13 needs; and
/// M-14/15/16 do not exist under <c>src/</c> at all. The default binding is therefore
/// <c>NullExternalParameterReferenceReader</c> (always empty), which means BR-10's warning is currently complete
/// for channel contracts and silent for the two external kinds. Tracked as TODO-M13-005.</para>
/// </summary>
public interface IExternalParameterReferenceReader
{
    /// <summary>
    /// Returns the M-10 data-scope filters referencing <paramref name="apiField"/>. The lookup is by API field
    /// name, not parameter id: that is the only identifier M-13 pushes cross-module (data-model.md §4).
    /// </summary>
    Task<IReadOnlyList<string>> GetDataScopeFilterNamesAsync(string apiField, CancellationToken ct = default);

    /// <summary>Returns the M-14/15/16 rules or actions referencing <paramref name="apiField"/>.</summary>
    Task<IReadOnlyList<string>> GetRuleNamesAsync(string apiField, CancellationToken ct = default);
}
