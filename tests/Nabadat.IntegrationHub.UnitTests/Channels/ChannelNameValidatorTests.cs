using FluentAssertions;
using Nabadat.IntegrationHub.Application.Channels;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Channels;

/// <summary>
/// T026 [US1] — unit tests for <c>ChannelNameValidator</c>: the bilingual channel names (BR-06).
/// EN is required, ≤ 50 characters, and unique per tenant case-insensitively (VR-F02); AR is required
/// (VR-F03) and bounded by the same 50-character column CHECK.
///
/// <para>Contract these tests pin for the implementer (T033):
/// <list type="bullet">
///   <item><c>ChannelNameValidator</c> in <c>Application/Channels/</c> with
///   <c>ChannelValidationResult Validate(string? nameEn, string? nameAr, IEnumerable&lt;string&gt;? existingNamesEn = null)</c>
///   — pure; the caller supplies the tenant's existing EN names (already excluding the row being edited),
///   and omitting them means "nothing to collide with".</item>
///   <item><c>ChannelNameValidator.MaxNameLength</c> = 50, matching the baseline's
///   <c>ck_service_channels_name_en_length</c> / <c>..._name_ar_length</c> CHECKs so a valid input can
///   never be rejected by the database instead of by this validator.</item>
///   <item>Errors <b>accumulate</b>: a submission that is wrong in several fields reports every failure at
///   once, so SCR-04 can render all inline errors in one pass rather than one per round-trip.</item>
///   <item>Shipped console copy is asserted verbatim — the EN-required message is the normative
///   "Channel name · EN is required" from spec.md, middle dot included.</item>
/// </list>
/// Uniqueness is checked only on the EN name: VR-F02 scopes it there, and AR names are not required to be
/// unique.</para>
/// </summary>
public sealed class ChannelNameValidatorTests
{
    private static readonly ChannelNameValidator Validator = new();

    private const string ArabicName = "كشك الخدمة الذاتية";

    [Fact]
    public void MaxNameLength_is_50_characters_matching_the_baseline_check()
    {
        ChannelNameValidator.MaxNameLength.Should().Be(50);
    }

    [Fact]
    public void Validate_returns_invalid_name_en_required_when_english_name_is_empty()
    {
        // The normative spec.md required case: Validate(nameEn="", nameAr="جيد") → Invalid("Channel name · EN is required").
        var result = Validator.Validate(nameEn: string.Empty, nameAr: "جيد");

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.NameEnRequired).Should().BeTrue();
        result.Messages.Should().Contain("Channel name · EN is required");
    }

    [Fact]
    public void Validate_returns_invalid_name_en_required_when_english_name_is_only_whitespace()
    {
        Validator.Validate("   ", ArabicName).HasCode(ChannelErrorCodes.NameEnRequired).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_name_ar_required_when_arabic_name_is_empty()
    {
        var result = Validator.Validate("Self-Service Kiosk", string.Empty);

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.NameArRequired).Should().BeTrue();
        result.Messages.Should().Contain("Channel name · AR is required");
    }

    [Fact]
    public void Validate_returns_invalid_name_en_too_long_when_english_name_exceeds_50_characters()
    {
        var result = Validator.Validate(new string('A', 51), ArabicName);

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.NameEnTooLong).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_when_english_name_is_exactly_50_characters()
    {
        // The bound is inclusive — 50 passes, 51 fails.
        Validator.Validate(new string('A', 50), ArabicName).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_name_ar_too_long_when_arabic_name_exceeds_50_characters()
    {
        var result = Validator.Validate("Self-Service Kiosk", new string('ك', 51));

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.NameArTooLong).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_duplicate_name_when_english_name_matches_an_existing_one_case_insensitively()
    {
        // VR-F02: "Self-Service Kiosk" already exists, so "self-service kiosk" is blocked.
        var result = Validator.Validate("self-service kiosk", ArabicName, new[] { "Self-Service Kiosk" });

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.DuplicateName).Should().BeTrue();
        result.Messages.Should().Contain("A channel with this name already exists");
    }

    [Fact]
    public void Validate_returns_valid_when_both_names_are_present_and_the_english_name_is_unique()
    {
        Validator.Validate("Call Center", "مركز الاتصال", new[] { "Self-Service Kiosk" })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_when_existing_names_is_null()
    {
        Validator.Validate("Self-Service Kiosk", ArabicName, existingNamesEn: null).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_accumulates_every_failure_when_both_names_are_missing()
    {
        var result = Validator.Validate(null, null);

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.NameEnRequired).Should().BeTrue();
        result.HasCode(ChannelErrorCodes.NameArRequired).Should().BeTrue();
    }
}
