using FluentAssertions;
using Nabadat.KpiManagement.Application.Organization;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.Organization;

/// <summary>
/// T129 [US6] — unit tests for <c>LogoUploadValidator</c> (FR-050 logo content-type + size rules),
/// covering the spec.md US-6 Required cases.
/// <para>
/// Contract pinned for the implementer (T134):
/// <list type="bullet">
///   <item><c>LogoUploadValidator</c> in <c>Application/Organization/</c> exposing
///   <c>LogoValidationResult Validate(string contentType, long sizeBytes)</c>. (NOT a FluentValidation
///   validator — the soft-size case is a non-blocking <c>Warning</c>, a third outcome FluentValidation's
///   binary IsValid cannot express.)</item>
///   <item><c>LogoValidationResult(LogoValidationOutcome Outcome, string? Code)</c> with
///   <c>enum LogoValidationOutcome { Valid, Warning, Invalid }</c>.</item>
///   <item>PNG/JPG/SVG ≤ 2 MB → <c>(Valid, null)</c>; PNG &gt; 2 MB → <c>(Warning, "logo.size.over_recommended")</c>
///   (non-blocking); unsupported content type → <c>(Invalid, "logo.content_type.unsupported")</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class LogoUploadValidatorTests
{
    private static readonly LogoUploadValidator Validator = new();

    [Fact]
    public void Validate_returns_valid_when_png_is_within_recommended_size()
    {
        var result = Validator.Validate(contentType: "image/png", sizeBytes: 500_000);

        result.Outcome.Should().Be(LogoValidationOutcome.Valid);
        result.Code.Should().BeNull();
    }

    [Fact]
    public void Validate_returns_warning_when_png_exceeds_recommended_size()
    {
        var result = Validator.Validate(contentType: "image/png", sizeBytes: 3_000_000);

        result.Outcome.Should().Be(LogoValidationOutcome.Warning);
        result.Code.Should().Be("logo.size.over_recommended");
    }

    [Fact]
    public void Validate_returns_invalid_when_content_type_is_unsupported()
    {
        var result = Validator.Validate(contentType: "application/pdf", sizeBytes: 100_000);

        result.Outcome.Should().Be(LogoValidationOutcome.Invalid);
        result.Code.Should().Be("logo.content_type.unsupported");
    }
}
