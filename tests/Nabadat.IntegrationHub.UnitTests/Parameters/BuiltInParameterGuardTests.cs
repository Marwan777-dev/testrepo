using FluentAssertions;
using Nabadat.IntegrationHub.Application.Parameters;
using Nabadat.IntegrationHub.Application.Parameters.Exceptions;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Parameters;

/// <summary>
/// T048 [US2] — unit tests for <c>BuiltInParameterGuard</c>: BR-09 / <c>[PO-G27]</c>. The 23 seeded built-in
/// parameters may only be enabled or disabled. Deleting one, renaming its API field, or changing its data type is
/// rejected; custom parameters allow everything except a hard delete (which has no endpoint at all).
///
/// <para>Contract these tests pin for the implementer (T056):
/// <list type="bullet">
///   <item><c>BuiltInParameterGuard</c> in <c>Application/Parameters/</c> with
///   <c>void Guard(bool builtIn, ParameterAction action)</c> — it <b>throws</b> rather than returning a result,
///   which is deliberate and differs from the accumulating validators: these are not user-correctable field
///   errors, they are attempts at an operation that does not exist in the product. There is no inline message to
///   render, so there is nothing to accumulate.</item>
///   <item>The thrown type is <c>BuiltInParameterViolationException : InvalidOperationException</c> — it
///   satisfies spec.md's required case (<c>Guard(builtIn=true, action=Delete)</c> → throws
///   <c>InvalidOperationException</c>) while carrying the stable <c>Code</c> the controller maps to <b>409</b>
///   <c>parameter.type_locked</c> (contracts/api-endpoints.md).</item>
///   <item><c>ParameterAction</c> is a closed enum. <c>Delete</c> is a member even though no DELETE endpoint
///   exists (BR-09): the guard is the second line of defence if one is ever added by mistake, and the test below
///   is what fails if someone does.</item>
/// </list></para>
/// </summary>
public sealed class BuiltInParameterGuardTests
{
    private static readonly BuiltInParameterGuard Guard = new();

    [Fact]
    public void Guard_throws_invalid_operation_when_a_built_in_is_deleted()
    {
        // The normative spec.md required case: Guard(builtIn=true, action=Delete) → throws InvalidOperationException.
        var act = () => Guard.Guard(builtIn: true, ParameterAction.Delete);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Guard_allows_disabling_a_built_in()
    {
        // The normative spec.md required case: Guard(builtIn=true, action=Disable) → allowed.
        var act = () => Guard.Guard(builtIn: true, ParameterAction.Disable);

        act.Should().NotThrow();
    }

    [Fact]
    public void Guard_allows_enabling_a_built_in()
    {
        // BR-23 seeds all 23 enabled; BR-09 keeps the toggle available in both directions forever.
        var act = () => Guard.Guard(builtIn: true, ParameterAction.Enable);

        act.Should().NotThrow();
    }

    [Fact]
    public void Guard_throws_when_a_built_in_api_field_is_renamed()
    {
        var act = () => Guard.Guard(builtIn: true, ParameterAction.RenameApiField);

        act.Should().Throw<BuiltInParameterViolationException>()
            .Which.Code.Should().Be(ParameterErrorCodes.ApiFieldLocked);
    }

    [Fact]
    public void Guard_throws_parameter_type_locked_when_a_built_in_data_type_is_changed()
    {
        // [PO-G27] — the SCR-06 type select is read-only for built-ins, and the server rejects it regardless of
        // client state. contracts/api-endpoints.md maps this to 409 parameter.type_locked.
        var act = () => Guard.Guard(builtIn: true, ParameterAction.ChangeDataType);

        act.Should().Throw<BuiltInParameterViolationException>()
            .Which.Code.Should().Be(ParameterErrorCodes.ParameterTypeLocked);
    }

    [Theory]
    [InlineData(ParameterAction.Enable)]
    [InlineData(ParameterAction.Disable)]
    [InlineData(ParameterAction.RenameApiField)]
    [InlineData(ParameterAction.ChangeDataType)]
    [InlineData(ParameterAction.UpdateDisplayNames)]
    [InlineData(ParameterAction.UpdateUsageFlags)]
    public void Guard_allows_every_non_delete_action_on_a_custom_parameter(ParameterAction action)
    {
        var act = () => Guard.Guard(builtIn: false, action);

        act.Should().NotThrow();
    }

    [Fact]
    public void Guard_throws_when_a_custom_parameter_is_deleted()
    {
        // BR-09: customs are disabled, never hard-deleted. No DELETE endpoint exists for either origin; the
        // guard is the second line of defence if one is ever wired by mistake.
        var act = () => Guard.Guard(builtIn: false, ParameterAction.Delete);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Guard_allows_editing_a_built_in_display_name()
    {
        // BR-09's "never renamed" is about the API FIELD (VR-F06). The bilingual display names stay editable —
        // a tenant may localise "Branch" to its own vocabulary.
        var act = () => Guard.Guard(builtIn: true, ParameterAction.UpdateDisplayNames);

        act.Should().NotThrow();
    }

    [Fact]
    public void Guard_allows_editing_built_in_usage_flags()
    {
        var act = () => Guard.Guard(builtIn: true, ParameterAction.UpdateUsageFlags);

        act.Should().NotThrow();
    }

    [Fact]
    public void ParameterAction_never_carries_a_member_beyond_the_seven_governed_actions()
    {
        // A field-set guard: adding an action without deciding its built-in policy would silently default to
        // "allowed" in the switch. This test forces that decision to be made here.
        Enum.GetNames<ParameterAction>().Should().BeEquivalentTo(
            nameof(ParameterAction.Enable),
            nameof(ParameterAction.Disable),
            nameof(ParameterAction.RenameApiField),
            nameof(ParameterAction.ChangeDataType),
            nameof(ParameterAction.UpdateDisplayNames),
            nameof(ParameterAction.UpdateUsageFlags),
            nameof(ParameterAction.Delete));
    }
}
