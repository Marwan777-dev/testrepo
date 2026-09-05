using System.Text;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// Sanitises an uploaded SVG logo (FR-050 / research.md R1) by running it through
/// <see cref="HtmlSanitizer"/> (Ganss.Xss) configured with a strict element + attribute allow-list.
/// Everything not on the allow-list is dropped — failure-secure — so <c>script</c>,
/// <c>foreignObject</c>, <c>iframe</c>, <c>use</c> (any href), and every <c>on*</c> event-handler
/// attribute are stripped, while allow-listed shapes/gradients survive. The PERSISTED bytes are the
/// sanitiser output, never the upload bytes.
/// <para>
/// <see cref="Sanitise(byte[])"/> returns the sanitised bytes; <see cref="SanitiseDetailed(byte[])"/>
/// also reports whether anything was stripped (surfaced as <c>was_sanitised</c>). Both throw
/// <see cref="SvgUnsafeContentException"/> when the payload cannot be parsed as SVG at all (no
/// <c>&lt;svg&gt;</c> root) — there is nothing safe to keep.
/// </para>
/// </summary>
public sealed partial class SvgSanitiser
{
    // R1 allow-list — elements.
    private static readonly string[] AllowedElements =
    [
        "svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "text", "tspan", "defs", "linearGradient", "radialGradient", "stop", "symbol",
        "title", "desc",
    ];

    // R1 allow-list — attributes (presentation + geometry only; no href/xlink:href, no style, no on*).
    private static readonly string[] AllowedAttributes =
    [
        "xmlns", "viewBox", "width", "height", "x", "y", "x1", "y1", "x2", "y2",
        "cx", "cy", "r", "rx", "ry", "d", "points", "fill", "stroke", "stroke-width",
        "transform", "id", "class", "opacity", "fill-opacity", "stroke-opacity",
        "gradientUnits", "offset", "stop-color", "stop-opacity",
    ];

    /// <summary>Sanitises the SVG and returns the safe byte stream (discards the modified flag).</summary>
    public byte[] Sanitise(byte[] svgBytes) => SanitiseDetailed(svgBytes).Bytes;

    /// <summary>Sanitises the SVG and reports whether any node/attribute was stripped.</summary>
    public SvgSanitisationResult SanitiseDetailed(byte[] svgBytes)
    {
        ArgumentNullException.ThrowIfNull(svgBytes);

        var markup = Encoding.UTF8.GetString(svgBytes);
        if (!SvgRootRegex().IsMatch(markup))
        {
            throw new SvgUnsafeContentException();
        }

        var sanitiser = BuildSanitiser();
        var modified = false;
        sanitiser.RemovingTag += (_, _) => modified = true;
        sanitiser.RemovingAttribute += (_, _) => modified = true;
        sanitiser.RemovingStyle += (_, _) => modified = true;
        sanitiser.RemovingAtRule += (_, _) => modified = true;

        var clean = sanitiser.Sanitize(markup);

        // A payload that parsed as SVG but whose root did not survive sanitisation is not safe to keep.
        if (!SvgRootRegex().IsMatch(clean))
        {
            throw new SvgUnsafeContentException();
        }

        return new SvgSanitisationResult(Encoding.UTF8.GetBytes(clean), modified);
    }

    private static HtmlSanitizer BuildSanitiser()
    {
        var sanitiser = new HtmlSanitizer();

        sanitiser.AllowedTags.Clear();
        foreach (var tag in AllowedElements)
        {
            sanitiser.AllowedTags.Add(tag);
        }

        sanitiser.AllowedAttributes.Clear();
        foreach (var attribute in AllowedAttributes)
        {
            sanitiser.AllowedAttributes.Add(attribute);
        }

        // No data: / external schemes, no inline CSS — href/xlink:href and style are not allow-listed,
        // so any URL-bearing or style-injected vector is dropped regardless.
        sanitiser.AllowedSchemes.Clear();
        sanitiser.AllowedCssProperties.Clear();
        sanitiser.AllowDataAttributes = false;

        return sanitiser;
    }

    [GeneratedRegex("<svg\\b", RegexOptions.IgnoreCase)]
    private static partial Regex SvgRootRegex();
}
