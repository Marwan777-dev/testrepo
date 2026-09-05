namespace Nabadat.IntegrationHub.Application.Parameters.Dtos;

/// <summary>
/// One parameter definition as M-10's data-scope endpoint expects it — the wire shape of
/// <c>M13ParameterDefinition</c> in <c>Nabadat.UserManagement</c> (research.md §4.1).
///
/// <para>Mirrored rather than referenced on purpose: this is a <b>published cross-module contract</b>, and
/// M-13 serialises it over HTTP. Binding M-13's outbound payload to M-10's internal record type would couple the
/// two modules' compilation to each other's refactors for no benefit — the JSON is the contract.</para>
/// </summary>
/// <param name="Name">M-13's <c>api_field</c> — the only identifier pushed cross-module (data-model.md §4).</param>
/// <param name="Label">The EN display name, for M-10's scope-filter UI.</param>
/// <param name="AllowedValues">
/// The known value set. M-10 <b>rejects a definition with an empty set</b>, which is why the publisher only sends
/// parameters that actually have one (List types, via their mapping table).
/// </param>
public sealed record DataScopeParameterContract(string Name, string Label, IReadOnlyList<string> AllowedValues);
