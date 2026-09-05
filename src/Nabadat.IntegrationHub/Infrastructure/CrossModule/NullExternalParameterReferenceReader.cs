using Nabadat.IntegrationHub.Domain.Interfaces;

namespace Nabadat.IntegrationHub.Infrastructure.CrossModule;

/// <summary>
/// Default binding for <see cref="IExternalParameterReferenceReader"/> until a module can answer the reverse
/// "who references parameter P?" question (TODO-M13-005). Reports <b>no</b> external references, which makes
/// BR-10's impact warning complete for channel contracts (M-13's own data, read for real) and silent for M-10
/// scope filters and M-14/15/16 rules.
///
/// <para>Reporting nothing is the deliberate choice over throwing: BR-10's warning is <i>informational</i> — it
/// tells the user what a disable will affect, it does not gate the operation. A reader that threw would take the
/// whole parameter-disable flow down for a dependency that has no provider yet, which is a worse failure than an
/// incomplete list. The gap is recorded rather than hidden, and swapping in the real adapter is a one-line change
/// in the composition root with no consumer edits.</para>
/// </summary>
public sealed class NullExternalParameterReferenceReader : IExternalParameterReferenceReader
{
    public Task<IReadOnlyList<string>> GetDataScopeFilterNamesAsync(string apiField, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<IReadOnlyList<string>> GetRuleNamesAsync(string apiField, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
