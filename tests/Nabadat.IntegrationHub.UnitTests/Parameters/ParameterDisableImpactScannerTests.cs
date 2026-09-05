using FluentAssertions;
using Nabadat.IntegrationHub.Application.Parameters;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Parameters;

/// <summary>
/// T047 [US2] — unit tests for <c>ParameterDisableImpactScanner</c>: BR-10 / FR-S5-03 / AC-S5-02. Disabling a
/// parameter that is referenced by an M-10 data-scope filter, a rule builder (CMC-07: M-14/15/16), or a service
/// channel's contract requires an explicit impact warning (Dialog D-6) that lists <b>every</b> reference — not
/// just the first found (spec.md Edge Cases).
///
/// <para>Contract these tests pin for the implementer (T055):
/// <list type="bullet">
///   <item><c>ParameterDisableImpactScanner</c> in <c>Application/Parameters/</c> with
///   <c>IReadOnlyList&lt;ParameterReference&gt; Scan(Guid parameterId, IEnumerable&lt;ParameterReferenceSource&gt;? channelContracts = null, IEnumerable&lt;ParameterReferenceSource&gt;? scopeFilters = null, IEnumerable&lt;ParameterReferenceSource&gt;? ruleBuilders = null)</c>.</item>
///   <item><b>Pure</b> by design: BR-10's reference scan spans M-13's own <c>channel_parameter_assignments</c>
///   AND two <b>external</b> consumers (M-10 scope filters, M-14/15/16 rules) that cannot be joined in SQL
///   (Article 4.1 — identifier-only references, no cross-module FKs). <c>ParameterService</c> (T057) reads each
///   source through its own port and hands the candidates in; the scanner does the filtering, ordering, and
///   kind-stamping. That keeps the multi-source assembly unit-testable with no database.</item>
///   <item>Each source carries only <c>(ParameterId, Name)</c> — the <c>Kind</c> is stamped by <b>which
///   argument</b> the candidate arrived in, so a caller cannot mislabel a reference.</item>
///   <item>Results are grouped by kind in a fixed order (channel contracts → scope filters → rule builders) so
///   D-6's copy is deterministic, and de-duplicated by (kind, name).</item>
///   <item>An empty list is the "disable proceeds with no dialog" signal (spec.md required case).</item>
/// </list></para>
/// </summary>
public sealed class ParameterDisableImpactScannerTests
{
    private static readonly ParameterDisableImpactScanner Scanner = new();

    /// <summary>Stands in for the built-in <c>service</c> parameter named in spec.md's required case.</summary>
    private static readonly Guid ServiceParameterId = Guid.Parse("0000000d-0000-0000-0000-00000000000c");

    private static readonly Guid UnusedCustomParameterId = Guid.NewGuid();

    [Fact]
    public void Scan_returns_every_reference_when_the_parameter_is_used_by_a_scope_filter_and_a_channel_contract()
    {
        // The normative spec.md required case:
        // ScanReferences(parameterId="service", scopeFilters=[…], channelContracts=[…]) → non-empty list feeding D-6.
        var references = Scanner.Scan(
            ServiceParameterId,
            channelContracts: new[] { new ParameterReferenceSource(ServiceParameterId, "Self-Service Kiosk") },
            scopeFilters: new[] { new ParameterReferenceSource(ServiceParameterId, "Eastern Region Analysts") });

        references.Should().HaveCount(2);
        references.Should().Contain(new ParameterReference(ParameterReferenceKind.ChannelContract, "Self-Service Kiosk"));
        references.Should().Contain(new ParameterReference(ParameterReferenceKind.DataScopeFilter, "Eastern Region Analysts"));
    }

    [Fact]
    public void Scan_returns_an_empty_list_for_an_unreferenced_custom_parameter()
    {
        // The normative spec.md required case:
        // ScanReferences(parameterId="unused_custom_param") → empty list → disable proceeds with no dialog.
        Scanner.Scan(UnusedCustomParameterId).Should().BeEmpty();
    }

    [Fact]
    public void Scan_returns_an_empty_list_when_every_source_references_other_parameters()
    {
        var otherParameter = Guid.NewGuid();

        Scanner.Scan(
                UnusedCustomParameterId,
                channelContracts: new[] { new ParameterReferenceSource(otherParameter, "Call Center") },
                scopeFilters: new[] { new ParameterReferenceSource(otherParameter, "Branch Managers") },
                ruleBuilders: new[] { new ParameterReferenceSource(otherParameter, "Escalation Rule") })
            .Should().BeEmpty();
    }

    [Fact]
    public void Scan_lists_all_three_consumer_kinds_when_the_parameter_is_referenced_simultaneously()
    {
        // spec.md Edge Cases: "Parameter referenced by three different consumers simultaneously (a scope filter,
        // a rule builder, and a channel contract) — the impact warning lists ALL references, not just the first
        // found (BR-10)."
        var references = Scanner.Scan(
            ServiceParameterId,
            channelContracts: new[] { new ParameterReferenceSource(ServiceParameterId, "Self-Service Kiosk") },
            scopeFilters: new[] { new ParameterReferenceSource(ServiceParameterId, "Eastern Region Analysts") },
            ruleBuilders: new[] { new ParameterReferenceSource(ServiceParameterId, "VIP Escalation Rule") });

        references.Select(r => r.Kind).Should().Equal(
            ParameterReferenceKind.ChannelContract,
            ParameterReferenceKind.DataScopeFilter,
            ParameterReferenceKind.RuleBuilder);
    }

    [Fact]
    public void Scan_keeps_every_distinct_reference_of_the_same_kind()
    {
        var references = Scanner.Scan(
            ServiceParameterId,
            channelContracts: new[]
            {
                new ParameterReferenceSource(ServiceParameterId, "Self-Service Kiosk"),
                new ParameterReferenceSource(ServiceParameterId, "Call Center"),
                new ParameterReferenceSource(Guid.NewGuid(), "Mobile App"),
            });

        references.Select(r => r.Name).Should().BeEquivalentTo("Self-Service Kiosk", "Call Center");
    }

    [Fact]
    public void Scan_deduplicates_identical_references()
    {
        // A parameter assigned to the same channel through two rows, or reported twice by an external consumer,
        // must not be listed twice in D-6's copy.
        var references = Scanner.Scan(
            ServiceParameterId,
            channelContracts: new[]
            {
                new ParameterReferenceSource(ServiceParameterId, "Self-Service Kiosk"),
                new ParameterReferenceSource(ServiceParameterId, "Self-Service Kiosk"),
            });

        references.Should().HaveCount(1);
    }

    [Fact]
    public void Scan_keeps_the_same_name_when_it_appears_under_two_different_kinds()
    {
        // "Branch" as both a channel name and a scope-filter name are two genuinely different references.
        var references = Scanner.Scan(
            ServiceParameterId,
            channelContracts: new[] { new ParameterReferenceSource(ServiceParameterId, "Branch") },
            scopeFilters: new[] { new ParameterReferenceSource(ServiceParameterId, "Branch") });

        references.Should().HaveCount(2);
    }

    [Fact]
    public void Scan_ignores_a_source_with_a_blank_name()
    {
        // A nameless reference would render as an empty bullet in D-6 — worse than omitting it.
        Scanner.Scan(
                ServiceParameterId,
                channelContracts: new[] { new ParameterReferenceSource(ServiceParameterId, "   ") })
            .Should().BeEmpty();
    }

    [Fact]
    public void Scan_returns_an_empty_list_for_an_empty_parameter_id()
    {
        Scanner.Scan(
                Guid.Empty,
                channelContracts: new[] { new ParameterReferenceSource(Guid.Empty, "Self-Service Kiosk") })
            .Should().BeEmpty();
    }
}
