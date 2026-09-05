namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// Thrown by <see cref="SvgSanitiser"/> when the upload bytes cannot be parsed as SVG at all (empty
/// file, binary garbage, JSON/HTML masquerading as <c>image/svg+xml</c>) — i.e. there is nothing the
/// allow-list sanitiser can make safe. The API layer maps this to <c>400 LOGO_SVG_UNSAFE_CONTENT</c>
/// (FR-050 / research.md R1). A payload that merely contains disallowed nodes/attributes does NOT
/// throw — those are stripped and the sanitised bytes returned.
/// </summary>
public sealed class SvgUnsafeContentException : Exception
{
    public SvgUnsafeContentException()
        : base("The uploaded file could not be parsed as a safe SVG.")
    {
    }

    public SvgUnsafeContentException(string message) : base(message)
    {
    }

    public SvgUnsafeContentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
