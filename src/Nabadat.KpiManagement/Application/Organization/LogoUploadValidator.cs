namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// Validates a logo upload's content type and size (FR-050). Not a FluentValidation validator: the
/// soft-size case is a non-blocking <see cref="LogoValidationOutcome.Warning"/>, a third outcome
/// FluentValidation's binary <c>IsValid</c> can't express. Accepted content types are PNG / JPEG /
/// SVG; anything else blocks. A 0-byte payload blocks. A payload over the recommended size (2 MB)
/// warns but proceeds; the hard 10 MB cap is enforced at the API layer (413).
/// </summary>
public sealed class LogoUploadValidator
{
    public const string ContentTypeUnsupportedCode = "logo.content_type.unsupported";
    public const string SizeZeroCode = "logo.size.zero";
    public const string SizeOverRecommendedCode = "logo.size.over_recommended";

    /// <summary>Recommended soft ceiling (2 MB) — over this warns but the upload proceeds.</summary>
    public const long RecommendedMaxBytes = 2L * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/svg+xml",
    };

    public LogoValidationResult Validate(string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            return new LogoValidationResult(LogoValidationOutcome.Invalid, ContentTypeUnsupportedCode);
        }

        if (sizeBytes <= 0)
        {
            return new LogoValidationResult(LogoValidationOutcome.Invalid, SizeZeroCode);
        }

        if (sizeBytes > RecommendedMaxBytes)
        {
            return new LogoValidationResult(LogoValidationOutcome.Warning, SizeOverRecommendedCode);
        }

        return new LogoValidationResult(LogoValidationOutcome.Valid, null);
    }
}
