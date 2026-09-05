using FluentAssertions;
using Nabadat.IntegrationHub.Application.Channels;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Channels;

/// <summary>
/// T022 [US1] — unit tests for <c>ChannelIdSanitizer</c> (VR-F04 / BR-04): the service-channel ID is
/// letters, digits and hyphens only, capped at 19 characters ("under 20"), and stored/matched in the URL
/// <b>exactly as entered</b> — so casing is preserved, never normalised.
///
/// <para>Contract these tests pin for the implementer (T029):
/// <list type="bullet">
///   <item><c>ChannelIdSanitizer</c> in <c>Application/Channels/</c>, with a single
///   <c>string Sanitize(string? raw)</c> method — pure, no I/O, no dependencies.</item>
///   <item><c>ChannelIdSanitizer.MaxLength</c> — the public 19-character cap the SCR-04 field's
///   <c>maxlength</c> mirrors, so the client and server never disagree.</item>
///   <item>Strip-then-truncate order: invalid characters are removed first, and the 19-char cap is
///   applied to what survives (otherwise "ab cd …" would lose real characters to stripped spaces).</item>
///   <item><c>null</c> / all-invalid input returns <see cref="string.Empty"/> — never <c>null</c>, so the
///   caller's required-field check is a plain emptiness test.</item>
/// </list>
/// The live-typing behaviour itself (AC-S4-01) is a client concern; this type is the server-side
/// enforcement of the same rule, which is why the sanitiser also runs on the write path.</para>
/// </summary>
public sealed class ChannelIdSanitizerTests
{
    private static readonly ChannelIdSanitizer Sanitizer = new();

    [Fact]
    public void MaxLength_is_19_characters_per_VR_F04()
    {
        ChannelIdSanitizer.MaxLength.Should().Be(19);
    }

    [Fact]
    public void Sanitize_strips_spaces_and_special_characters_when_input_has_them()
    {
        // The normative spec.md example: "My kiosk #1" → "Mykiosk1" (spaces and '#' stripped, case kept).
        Sanitizer.Sanitize("My kiosk #1").Should().Be("Mykiosk1");
    }

    [Fact]
    public void Sanitize_preserves_case_and_hyphens_when_input_is_already_valid()
    {
        // VR-F04: the ID is matched in the URL exactly as entered, so casing must survive untouched.
        Sanitizer.Sanitize("KIOSK-01").Should().Be("KIOSK-01");
    }

    [Fact]
    public void Sanitize_truncates_to_19_characters_when_input_is_longer()
    {
        var twentyValidChars = new string('A', 20);

        var sanitized = Sanitizer.Sanitize(twentyValidChars);

        sanitized.Should().HaveLength(19);
        sanitized.Should().Be(new string('A', 19));
    }

    [Fact]
    public void Sanitize_truncates_after_stripping_when_input_mixes_invalid_characters_and_overflow()
    {
        // 10 space-separated pairs = 20 valid characters once the 9 spaces are stripped, then capped at 19.
        Sanitizer.Sanitize("ab cd ef gh ij kl mn op qr st").Should().Be("abcdefghijklmnopqrs");
    }

    [Fact]
    public void Sanitize_returns_empty_when_input_is_null()
    {
        Sanitizer.Sanitize(null).Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_returns_empty_when_every_character_is_invalid()
    {
        Sanitizer.Sanitize("!!! ### $$$").Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_strips_underscores_and_dots_which_are_outside_the_allowed_set()
    {
        // Only [A-Za-z0-9-] survives — '_' and '.' are NOT allowed even though other identifiers use them
        // (the parameter API field is snake_case; the channel ID is not).
        Sanitizer.Sanitize("call_center.01").Should().Be("callcenter01");
    }
}
