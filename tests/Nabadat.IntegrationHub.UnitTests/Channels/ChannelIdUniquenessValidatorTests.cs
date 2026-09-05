using FluentAssertions;
using Nabadat.IntegrationHub.Application.Channels;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Channels;

/// <summary>
/// T023 [US1] — unit tests for <c>ChannelIdUniquenessValidator</c> (VR-F04): the service-channel ID is
/// unique per tenant <b>case-insensitively</b>, matching the VR-F01 convention used for integration
/// names. <c>KIOSK-01</c> and <c>kiosk-01</c> are the same ID.
///
/// <para>Contract these tests pin for the implementer (T030):
/// <list type="bullet">
///   <item><c>ChannelIdUniquenessValidator</c> in <c>Application/Channels/</c>, with
///   <c>ChannelValidationResult Validate(IEnumerable&lt;string&gt;? existingIds, string? channelId)</c>
///   — pure: the caller supplies the tenant's existing IDs (already excluding the row being edited), so
///   this type does no I/O and is reusable on both the create and update paths.</item>
///   <item><c>ChannelValidationResult</c> — <c>IsValid</c> + an <c>Errors</c> list of
///   <c>ChannelValidationError(Code, Message)</c>. The <c>Code</c> is the stable contract the API layer
///   maps to a status (duplicates → 409 per contracts/api-endpoints.md); the <c>Message</c> is the
///   shipped inline console copy.</item>
///   <item><c>ChannelErrorCodes</c> — the code constants, so controller and validator can never drift.</item>
/// </list>
/// Format enforcement is NOT this type's job — <c>ChannelIdSanitizer</c> (T029) owns it; this validator
/// only answers "is this ID free?" plus the required-field check.</para>
/// </summary>
public sealed class ChannelIdUniquenessValidatorTests
{
    private static readonly ChannelIdUniquenessValidator Validator = new();

    [Fact]
    public void Validate_returns_invalid_duplicate_when_existing_id_differs_only_by_case()
    {
        // The normative spec.md required case.
        var result = Validator.Validate(new[] { "KIOSK-01" }, "kiosk-01");

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.DuplicateChannelId).Should().BeTrue();
        result.Messages.Should().Contain("A channel with this ID already exists");
    }

    [Fact]
    public void Validate_returns_invalid_duplicate_when_id_matches_an_existing_one_exactly()
    {
        var result = Validator.Validate(new[] { "CALLCENTER", "KIOSK-01" }, "KIOSK-01");

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.DuplicateChannelId).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_when_id_is_unused()
    {
        Validator.Validate(new[] { "KIOSK-01", "PORTAL" }, "BRANCH-02").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_when_the_tenant_has_no_channels_yet()
    {
        Validator.Validate(Array.Empty<string>(), "KIOSK-01").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_valid_when_existing_ids_is_null()
    {
        // A null collection means "nothing to collide with" — never a NullReferenceException.
        Validator.Validate(null, "KIOSK-01").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_required_when_id_is_empty()
    {
        var result = Validator.Validate(Array.Empty<string>(), string.Empty);

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.ChannelIdRequired).Should().BeTrue();
        result.Messages.Should().Contain("Service channel ID is required");
    }

    [Fact]
    public void Validate_returns_invalid_required_when_id_is_null()
    {
        Validator.Validate(Array.Empty<string>(), null)
            .HasCode(ChannelErrorCodes.ChannelIdRequired).Should().BeTrue();
    }

    [Fact]
    public void Validate_returns_invalid_required_when_id_is_only_whitespace()
    {
        // Whitespace cannot survive sanitisation, so it is an empty ID by the time it reaches here.
        Validator.Validate(Array.Empty<string>(), "   ")
            .HasCode(ChannelErrorCodes.ChannelIdRequired).Should().BeTrue();
    }
}
