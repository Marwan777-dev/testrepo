using FluentAssertions;
using Nabadat.IntegrationHub.Application.Integrations;
using Nabadat.IntegrationHub.Domain.ValueObjects;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Integrations;

/// <summary>
/// T069 [US3] — unit tests for <c>ScenarioSelectionRule</c> (BR-02 / FR-S2-03): an integration carries
/// <b>exactly one</b> of SCN-01…05, chosen at create and <b>immutable</b> afterwards. A caller that needs a
/// second scenario creates a second integration.
///
/// <para>Contract these tests pin for the implementer (T077):
/// <list type="bullet">
///   <item><c>ScenarioSelectionRule</c> in <c>Application/Integrations/</c> with two pure methods:
///   <c>IntegrationValidationResult ValidateSelection(Scenario? selected)</c> and
///   <c>IntegrationValidationResult ValidateChange(Scenario current, Scenario? requested)</c>.</item>
///   <item>BR-02 is enforced <b>structurally</b> — <see cref="Scenario"/> is a single-valued field, never a
///   set — so "attempt a second scenario" can only reach the server as a <i>change</i> of the existing one.
///   That is what <c>ValidateChange</c> rejects, with <c>integration.scenario_immutable</c> → 409.</item>
///   <item>A <c>null</c> <c>requested</c> means the client did not submit the field (edit mode legitimately
///   renders it read-only), which is not a change and stays valid. Re-submitting the same scenario is
///   likewise valid, so an unrelated rename still saves.</item>
/// </list></para>
/// </summary>
public sealed class ScenarioSelectionRuleTests
{
    private static readonly ScenarioSelectionRule Rule = new();

    [Fact]
    public void ValidateChange_returns_invalid_scenario_immutable_when_a_second_scenario_is_attempted()
    {
        // spec.md required case: SelectScenario(current=SCN-01, attemptSecond=SCN-03) → rejected. Only one
        // scenario field exists per integration; a second scenario requires a second integration (BR-02).
        var result = Rule.ValidateChange(Scenario.Dispatch, Scenario.JsonRender);

        result.IsValid.Should().BeFalse();
        result.HasCode(IntegrationErrorCodes.ScenarioImmutable).Should().BeTrue();
        result.Errors.Single().Field.Should().Be(IntegrationFields.Scenario);
    }

    [Fact]
    public void ValidateChange_returns_valid_when_the_same_scenario_is_resubmitted()
    {
        // Edit mode round-trips the field; an unchanged value must not block a rename or a channel change.
        Rule.ValidateChange(Scenario.RedirectLink, Scenario.RedirectLink).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateChange_returns_valid_when_the_client_omits_the_scenario()
    {
        Rule.ValidateChange(Scenario.IframeEmbed, requested: null).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(Scenario.Dispatch, Scenario.RedirectLink)]
    [InlineData(Scenario.RedirectLink, Scenario.JsonRender)]
    [InlineData(Scenario.JsonRender, Scenario.IframeEmbed)]
    [InlineData(Scenario.IframeEmbed, Scenario.ResponseIngestion)]
    [InlineData(Scenario.ResponseIngestion, Scenario.Dispatch)]
    public void ValidateChange_rejects_every_cross_scenario_transition(Scenario current, Scenario requested)
    {
        // No pair of distinct scenarios is ever a legal transition — the rule has no "compatible" exceptions.
        Rule.ValidateChange(current, requested)
            .HasCode(IntegrationErrorCodes.ScenarioImmutable).Should().BeTrue();
    }

    [Fact]
    public void ValidateSelection_returns_invalid_scenario_required_when_no_card_was_chosen()
    {
        // SCR-02 step 1 has NO default selection in create mode, so "Continue" with nothing picked must be a
        // named inline error rather than a silent default to Dispatch.
        var result = Rule.ValidateSelection(null);

        result.IsValid.Should().BeFalse();
        result.HasCode(IntegrationErrorCodes.ScenarioRequired).Should().BeTrue();
        result.Errors.Single().Field.Should().Be(IntegrationFields.Scenario);
    }

    [Theory]
    [InlineData(Scenario.Dispatch)]
    [InlineData(Scenario.RedirectLink)]
    [InlineData(Scenario.JsonRender)]
    [InlineData(Scenario.IframeEmbed)]
    [InlineData(Scenario.ResponseIngestion)]
    public void ValidateSelection_returns_valid_for_each_of_the_five_ratified_scenarios(Scenario selected)
    {
        Rule.ValidateSelection(selected).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSelection_returns_invalid_scenario_required_for_a_value_outside_the_five()
    {
        // A cast integer that matches no member is not a sixth scenario — the catalogue is closed (BR-02).
        Rule.ValidateSelection((Scenario)99)
            .HasCode(IntegrationErrorCodes.ScenarioRequired).Should().BeTrue();
    }
}
