using FluentAssertions;
using Nabadat.IntegrationHub.Application.Channels;
using Nabadat.IntegrationHub.Application.Channels.Dtos;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Channels;

/// <summary>
/// T025 [US1] — unit tests for <c>ParameterContractDependencyRule</c> (FR-S4-04 / AC-S4-03): in a channel's
/// parameter contract, <c>Required</c> may only be <c>true</c> while <c>Supported</c> is <c>true</c>.
/// Clearing <c>Supported</c> force-clears <c>Required</c> in the <b>same</b> write, so the persisted row can
/// never violate the baseline's <c>ck_channel_parameter_assignments_required_needs_supported</c> CHECK.
///
/// <para>Contract these tests pin for the implementer (T032):
/// <list type="bullet">
///   <item><c>ParameterContractDependencyRule</c> in <c>Application/Channels/</c>, with
///   <c>ContractFlags Apply(bool supported, bool required)</c> for a single row and
///   <c>IReadOnlyList&lt;ChannelParameterAssignmentInput&gt; ApplyAll(IEnumerable&lt;ChannelParameterAssignmentInput&gt;? rows)</c>
///   for a whole submitted contract.</item>
///   <item><c>ContractFlags(bool Supported, bool Required)</c> — the normalised pair, one type per file in
///   <c>Application/Channels/</c>.</item>
///   <item>This is a <b>normaliser, not a rejecter</b>: an inconsistent (supported=false, required=true)
///   input is silently corrected rather than returning a validation error, because the UI's dependency
///   already prevents it and a stale client's contradiction has one obvious safe resolution.</item>
/// </list>
/// The live contract-summary counts (FR-S4-03) are derived from the normalised rows, which is why
/// normalisation must happen before persistence and before counting.</para>
/// </summary>
public sealed class ParameterContractDependencyRuleTests
{
    private static readonly ParameterContractDependencyRule Rule = new();

    [Fact]
    public void Apply_clears_required_when_supported_is_false()
    {
        // The normative spec.md required case: ApplyDependency(supported=false, required=true) → (false, false).
        Rule.Apply(supported: false, required: true).Should().Be(new ContractFlags(false, false));
    }

    [Fact]
    public void Apply_keeps_required_when_supported_is_true()
    {
        // The normative spec.md required case: Required may be set only while Supported is on.
        Rule.Apply(supported: true, required: true).Should().Be(new ContractFlags(true, true));
    }

    [Fact]
    public void Apply_returns_supported_without_required_when_only_supported_is_on()
    {
        Rule.Apply(supported: true, required: false).Should().Be(new ContractFlags(true, false));
    }

    [Fact]
    public void Apply_returns_both_false_when_neither_flag_is_set()
    {
        Rule.Apply(supported: false, required: false).Should().Be(new ContractFlags(false, false));
    }

    [Fact]
    public void ApplyAll_force_clears_required_on_every_unsupported_row_and_leaves_supported_rows_intact()
    {
        var mobile = Guid.NewGuid();
        var email = Guid.NewGuid();
        var vip = Guid.NewGuid();

        var normalised = Rule.ApplyAll(new[]
        {
            new ChannelParameterAssignmentInput(mobile, Supported: true, Required: true),
            new ChannelParameterAssignmentInput(email, Supported: false, Required: true),
            new ChannelParameterAssignmentInput(vip, Supported: true, Required: false),
        });

        normalised.Should().HaveCount(3);
        normalised.Single(r => r.ParameterId == mobile).Should()
            .BeEquivalentTo(new ChannelParameterAssignmentInput(mobile, true, true));
        normalised.Single(r => r.ParameterId == email).Should()
            .BeEquivalentTo(new ChannelParameterAssignmentInput(email, false, false));
        normalised.Single(r => r.ParameterId == vip).Should()
            .BeEquivalentTo(new ChannelParameterAssignmentInput(vip, true, false));
    }

    [Fact]
    public void ApplyAll_preserves_the_submitted_row_order()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var normalised = Rule.ApplyAll(new[]
        {
            new ChannelParameterAssignmentInput(first, true, false),
            new ChannelParameterAssignmentInput(second, true, true),
        });

        normalised.Select(r => r.ParameterId).Should().ContainInOrder(first, second);
    }

    [Fact]
    public void ApplyAll_returns_empty_when_rows_is_null()
    {
        // A channel created with no contract rows is legal — it simply supports nothing yet.
        Rule.ApplyAll(null).Should().BeEmpty();
    }

    [Fact]
    public void ApplyAll_returns_empty_when_rows_is_empty()
    {
        Rule.ApplyAll(Array.Empty<ChannelParameterAssignmentInput>()).Should().BeEmpty();
    }
}
