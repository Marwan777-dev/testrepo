using FluentAssertions;
using Nabadat.IntegrationHub.Application.Parameters;
using Nabadat.IntegrationHub.Domain.Entities;
using Nabadat.IntegrationHub.Domain.ValueObjects;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Parameters;

/// <summary>
/// T045 [US2] — unit tests for <c>ApiFieldNameLockGuard</c>: BR-11 / FR-S6-02. A parameter's API field name is
/// editable until the first inbound request carrying it has been received, then locked permanently — renaming
/// after that would break the caller (tenet T-08). Built-ins are <b>always</b> locked (BR-09).
///
/// <para>Contract these tests pin for the implementer (T053), deliberately mirroring
/// <c>ChannelIdLockGuard</c> (T031) so the two lock rules read the same:
/// <list type="bullet">
///   <item><c>ApiFieldNameLockGuard</c> in <c>Application/Parameters/</c> with
///   <c>bool IsLocked(Parameter parameter, bool hasReceivedRequest)</c> and
///   <c>ParameterValidationResult ValidateApiFieldChange(Parameter parameter, bool hasReceivedRequest, string? requestedApiField)</c>.</item>
///   <item>The lock has <b>three</b> OR-ed sources: the persisted one-way <c>Parameter.ApiFieldLocked</c> flag,
///   the caller's live "has a request carried this field?" probe, and <c>Origin == BuiltIn</c>. The probe is
///   defence in depth for the case where traffic exists but the flag was never written; the origin check means a
///   built-in stays locked even if its seeded flag were somehow cleared.</item>
///   <item>The guard is <b>pure</b> — <c>ParameterService</c> (T057) resolves the probe and passes a boolean, so
///   enforcement is server-side and a stale client rendering the field editable cannot get around it.</item>
///   <item><c>Enabled</c> and the API-field lock are <b>independent axes</b> (spec.md Edge Cases): re-enabling a
///   disabled parameter never unlocks its field name.</item>
/// </list></para>
/// </summary>
public sealed class ApiFieldNameLockGuardTests
{
    private static readonly ApiFieldNameLockGuard Guard = new();

    private static Parameter Custom(bool apiFieldLocked = false, bool enabled = true) => new()
    {
        Id = Guid.NewGuid(),
        NameEn = "Wait Time",
        NameAr = "وقت الانتظار",
        ApiField = "wait_time",
        ApiFieldLocked = apiFieldLocked,
        DataType = DataType.Range,
        Origin = ParameterOrigin.Custom,
        Enabled = enabled,
    };

    private static Parameter BuiltIn() => new()
    {
        Id = Guid.NewGuid(),
        NameEn = "Branch",
        NameAr = "الفرع",
        ApiField = "branch",
        ApiFieldLocked = true,
        DataType = DataType.List,
        Origin = ParameterOrigin.BuiltIn,
        Enabled = true,
    };

    [Fact]
    public void IsLocked_returns_true_when_a_request_carrying_the_field_has_been_received()
    {
        // The normative spec.md required case: IsLocked(parameter, hasReceivedRequest=true) → true.
        Guard.IsLocked(Custom(), hasReceivedRequest: true).Should().BeTrue();
    }

    [Fact]
    public void IsLocked_returns_true_when_the_persisted_lock_flag_is_set()
    {
        Guard.IsLocked(Custom(apiFieldLocked: true), hasReceivedRequest: false).Should().BeTrue();
    }

    [Fact]
    public void IsLocked_returns_true_for_a_built_in_parameter_even_with_no_traffic_and_no_flag()
    {
        // BR-09: built-in field names are permanently read-only, independent of traffic.
        var seededWithoutTheFlag = BuiltIn();
        seededWithoutTheFlag.ApiFieldLocked = false;

        Guard.IsLocked(seededWithoutTheFlag, hasReceivedRequest: false).Should().BeTrue();
    }

    [Fact]
    public void IsLocked_returns_false_for_a_fresh_custom_parameter_with_no_traffic()
    {
        Guard.IsLocked(Custom(), hasReceivedRequest: false).Should().BeFalse();
    }

    [Fact]
    public void IsLocked_stays_true_for_a_disabled_parameter_whose_field_was_already_locked()
    {
        // spec.md Edge Cases: enabled-state and the field lock are independent axes — disabling (or later
        // re-enabling) never unlocks the name.
        Guard.IsLocked(Custom(apiFieldLocked: true, enabled: false), hasReceivedRequest: false).Should().BeTrue();
    }

    [Fact]
    public void ValidateApiFieldChange_returns_invalid_api_field_locked_when_a_locked_field_is_changed()
    {
        var result = Guard.ValidateApiFieldChange(
            Custom(apiFieldLocked: true), hasReceivedRequest: false, requestedApiField: "handling_time");

        result.IsValid.Should().BeFalse();
        result.HasCode(ParameterErrorCodes.ApiFieldLocked).Should().BeTrue();
        result.Errors.Should().OnlyContain(e => e.Field == ParameterFields.ApiField);
    }

    [Fact]
    public void ValidateApiFieldChange_returns_valid_when_the_requested_field_is_unchanged_on_a_locked_parameter()
    {
        // Re-submitting the same value is not a change — this is what lets a locked parameter still save a
        // display-name edit, a flag change, or an enable/disable toggle.
        Guard.ValidateApiFieldChange(Custom(apiFieldLocked: true), hasReceivedRequest: true, "wait_time")
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateApiFieldChange_returns_valid_when_the_field_was_not_submitted_at_all()
    {
        // A null means "the client did not send the field" — which a locked parameter's read-only form does.
        Guard.ValidateApiFieldChange(Custom(apiFieldLocked: true), hasReceivedRequest: true, requestedApiField: null)
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateApiFieldChange_returns_valid_when_an_unlocked_custom_field_is_renamed()
    {
        Guard.ValidateApiFieldChange(Custom(), hasReceivedRequest: false, "handling_time")
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateApiFieldChange_returns_invalid_for_a_built_in_rename_attempt()
    {
        Guard.ValidateApiFieldChange(BuiltIn(), hasReceivedRequest: false, "branch_code")
            .HasCode(ParameterErrorCodes.ApiFieldLocked).Should().BeTrue();
    }

    [Fact]
    public void ValidateApiFieldChange_treats_a_case_only_difference_as_a_real_change()
    {
        // The wire key is matched exactly by the request pipeline, so "Wait_Time" is a different key.
        Guard.ValidateApiFieldChange(Custom(apiFieldLocked: true), hasReceivedRequest: true, "WAIT_TIME")
            .HasCode(ParameterErrorCodes.ApiFieldLocked).Should().BeTrue();
    }

    [Fact]
    public void Guard_throws_when_the_parameter_is_null()
    {
        var act = () => Guard.IsLocked(null!, hasReceivedRequest: true);

        act.Should().Throw<ArgumentNullException>();
    }
}
