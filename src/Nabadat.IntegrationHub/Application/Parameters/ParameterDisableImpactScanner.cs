namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// T055 — BR-10 / FR-S5-03 / AC-S5-02: assembles the reference list behind the parameter-disable impact warning
/// (Dialog D-6). It lists <b>every</b> reference, not just the first found (spec.md Edge Cases), across all three
/// consumer families.
///
/// <para>Pure by design, and that is the load-bearing decision here: BR-10's scan spans M-13's own
/// <c>channel_parameter_assignments</c> AND two <b>external</b> consumers — M-10 data-scope filters (CMC-06) and
/// M-14/15/16 rules (CMC-07) — which are identifier-only references with no cross-module foreign key
/// (architecture-constitution Article 4.1). There is no single query that can join them.
/// <see cref="ParameterService"/> reads each source through its own port and hands the candidates in; this type
/// does the filtering, kind-stamping, ordering and de-duplication, which keeps the multi-source assembly
/// unit-testable with no database.</para>
/// </summary>
public sealed class ParameterDisableImpactScanner
{
    /// <summary>
    /// Returns every reference to <paramref name="parameterId"/> found in the supplied sources, grouped by kind
    /// in <see cref="ParameterReferenceKind"/> order and de-duplicated by (kind, name). An empty result is the
    /// "disable proceeds with no dialog" signal.
    ///
    /// <para>A blank candidate name is dropped — it would render as an empty bullet in D-6, which is worse than
    /// omitting the reference. An empty <paramref name="parameterId"/> matches nothing, so a caller that failed
    /// to resolve the parameter cannot accidentally list every unassociated reference.</para>
    /// </summary>
    public IReadOnlyList<ParameterReference> Scan(
        Guid parameterId,
        IEnumerable<ParameterReferenceSource>? channelContracts = null,
        IEnumerable<ParameterReferenceSource>? scopeFilters = null,
        IEnumerable<ParameterReferenceSource>? ruleBuilders = null)
    {
        if (parameterId == Guid.Empty)
        {
            return Array.Empty<ParameterReference>();
        }

        var references = new List<ParameterReference>();

        Collect(references, parameterId, channelContracts, ParameterReferenceKind.ChannelContract);
        Collect(references, parameterId, scopeFilters, ParameterReferenceKind.DataScopeFilter);
        Collect(references, parameterId, ruleBuilders, ParameterReferenceKind.RuleBuilder);

        return references;
    }

    private static void Collect(
        List<ParameterReference> into,
        Guid parameterId,
        IEnumerable<ParameterReferenceSource>? candidates,
        ParameterReferenceKind kind)
    {
        if (candidates is null)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.ParameterId != parameterId || string.IsNullOrWhiteSpace(candidate.Name))
            {
                continue;
            }

            var reference = new ParameterReference(kind, candidate.Name.Trim());

            if (!into.Contains(reference))
            {
                into.Add(reference);
            }
        }
    }
}
