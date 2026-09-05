using FluentAssertions;
using Nabadat.IntegrationHub.Application.Integrations;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Integrations;

/// <summary>
/// T068 [US3] — unit tests for <c>IntegrationNameValidator</c> (VR-F01 / FR-S2-02): the integration name is
/// required, at most <see cref="IntegrationNameValidator.MaxNameLength"/> characters, and unique per tenant
/// <b>case-insensitively</b> (spec.md's required case: <c>existingNames=["Core Bus"], name="core bus"</c> →
/// Invalid, a <c>[Formalized default]</c> in SRS v1.2 matching VR-F04/F06/F08's convention).
///
/// <para>Contract these tests pin for the implementer (T076):
/// <list type="bullet">
///   <item><c>IntegrationNameValidator</c> in <c>Application/Integrations/</c> with one pure method
///   <c>IntegrationValidationResult Validate(string? name, IEnumerable&lt;string&gt;? existingNames = null)</c>.</item>
///   <item>Failures <b>accumulate</b> (the Channels/Parameters convention) so SCR-02's step 1 renders every
///   inline error in one pass instead of one per save round-trip.</item>
///   <item><c>existingNames</c> is the tenant's <i>other</i> integrations' names — the caller excludes the row
///   being edited, so a rename that keeps the same name is not a self-collision. The validator never queries.</item>
///   <item>The 100-character cap is VR-F01's, deliberately <b>below</b> the baseline's
///   <c>ck_integrations_name_length</c> ceiling of 120, so a name this validator accepts can never be
///   rejected by the database instead.</item>
/// </list></para>
/// </summary>
public sealed class IntegrationNameValidatorTests
{
    private static readonly IntegrationNameValidator Validator = new();

    [Fact]
    public void Validate_returns_invalid_name_required_when_the_name_is_empty()
    {
        // spec.md required case: Validate(name="", channel=X, scenario=SCN-01) → Invalid("Integration name is required").
        var result = Validator.Validate(string.Empty);

        result.IsValid.Should().BeFalse();
        result.HasCode(IntegrationErrorCodes.NameRequired).Should().BeTrue();
        result.Errors.Single().Field.Should().Be(IntegrationFields.Name);
        result.Messages.Single().Should().Be("Integration name is required");
    }

    [Fact]
    public void Validate_returns_invalid_name_required_when_the_name_is_whitespace_only()
    {
        // A form that submits "   " is as nameless as one that submits nothing; trimming happens on save.
        Validator.Validate("   ").HasCode(IntegrationErrorCodes.NameRequired).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_duplicate_name_when_an_existing_name_differs_only_by_case()
    {
        // spec.md required case: Validate(existingNames=["Core Bus"], name="core bus") → Invalid (VR-F01,
        // case-insensitive per the [Formalized default]).
        var result = Validator.Validate("core bus", new[] { "Core Bus" });

        result.IsValid.Should().BeFalse();
        result.HasCode(IntegrationErrorCodes.DuplicateName).Should().BeTrue();
        result.Errors.Single().Field.Should().Be(IntegrationFields.Name);
    }

    [Fact]
    public void Validate_returns_invalid_duplicate_name_when_an_existing_name_matches_exactly()
    {
        Validator.Validate("Core Bus", new[] { "Branch Kiosk", "Core Bus" })
            .HasCode(IntegrationErrorCodes.DuplicateName).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_name_too_long_when_the_name_exceeds_100_characters()
    {
        var result = Validator.Validate(new string('x', IntegrationNameValidator.MaxNameLength + 1));

        result.IsValid.Should().BeFalse();
        result.HasCode(IntegrationErrorCodes.NameTooLong).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_when_the_name_is_exactly_at_the_length_cap()
    {
        // The cap is inclusive — 100 characters is legal, 101 is not.
        Validator.Validate(new string('x', IntegrationNameValidator.MaxNameLength)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void MaxNameLength_stays_below_the_database_column_ceiling()
    {
        // VR-F01 caps at 100 while ck_integrations_name_length allows 120: the validator must be the stricter
        // of the two, or a name it accepts would be rejected by the database with no inline message.
        IntegrationNameValidator.MaxNameLength.Should().BeLessThan(120);
    }

    [Fact]
    public void Validate_accumulates_the_length_and_duplicate_failures_in_one_pass()
    {
        var tooLong = new string('a', IntegrationNameValidator.MaxNameLength + 5);

        var result = Validator.Validate(tooLong, new[] { tooLong.ToUpperInvariant() });

        result.Errors.Should().HaveCount(2);
        result.HasCode(IntegrationErrorCodes.NameTooLong).Should().BeTrue();
        result.HasCode(IntegrationErrorCodes.DuplicateName).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_when_the_name_is_unique_and_within_the_cap()
    {
        Validator.Validate("Core Services Bus — Survey Dispatch", new[] { "Branch Kiosk" })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_when_there_are_no_existing_names_to_collide_with()
    {
        Validator.Validate("First integration").IsValid.Should().BeTrue();
    }
}
