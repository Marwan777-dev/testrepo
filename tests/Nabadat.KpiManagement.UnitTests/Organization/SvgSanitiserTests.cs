using System.Text;
using FluentAssertions;
using Nabadat.KpiManagement.Application.Organization;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.Organization;

/// <summary>
/// T130 [US6] — unit tests for <c>SvgSanitiser</c> (FR-050 SVG logo sanitisation per
/// [research.md](../../../specs/003-kpi-engine-settings/research.md) R1), covering the spec.md US-6
/// Required cases.
/// <para>
/// Contract pinned for the implementer (T135):
/// <list type="bullet">
///   <item><c>SvgSanitiser</c> in <c>Application/Organization/</c> exposing
///   <c>byte[] Sanitise(byte[] svgBytes)</c>.</item>
///   <item>Returns sanitised bytes for any payload it can parse: benign markup passes through; the
///   disallowed <c>&lt;script&gt;</c> element, every <c>on*</c> event-handler attribute, the
///   <c>&lt;foreignObject&gt;</c> subtree, and external-<c>href</c> <c>&lt;use&gt;</c> are stripped while
///   allow-listed shapes (e.g. <c>&lt;circle&gt;</c>) survive.</item>
///   <item>Throws <c>SvgUnsafeContentException</c> (in <c>Application/Organization/</c>) when the input
///   cannot be parsed as SVG at all → API maps to <c>400 LOGO_SVG_UNSAFE_CONTENT</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class SvgSanitiserTests
{
    private static readonly SvgSanitiser Sanitiser = new();

    private static string Sanitise(string svg) =>
        Encoding.UTF8.GetString(Sanitiser.Sanitise(Encoding.UTF8.GetBytes(svg)));

    [Fact]
    public void Sanitise_passes_benign_svg_through_unchanged_in_substance()
    {
        var result = Sanitise("<svg xmlns='http://www.w3.org/2000/svg'><circle r='5'/></svg>");

        result.Should().Contain("circle");
        result.Should().Contain("svg");
    }

    [Fact]
    public void Sanitise_strips_script_node_but_keeps_allowed_shapes()
    {
        var result = Sanitise("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script><circle r='5'/></svg>");

        result.Should().NotContain("script");
        result.Should().Contain("circle");
    }

    [Fact]
    public void Sanitise_strips_event_handler_attributes_but_keeps_geometry_attributes()
    {
        var result = Sanitise("<svg xmlns='http://www.w3.org/2000/svg'><circle r='5' onload='alert(1)'/></svg>");

        result.Should().NotContain("onload");
        result.Should().Contain("circle");
        result.Should().Contain("r=");
    }

    [Fact]
    public void Sanitise_removes_foreign_object_subtree()
    {
        var result = Sanitise("<svg xmlns='http://www.w3.org/2000/svg'><foreignObject><iframe src='evil'/></foreignObject></svg>");

        result.Should().NotContain("foreignObject");
        result.Should().NotContain("iframe");
    }

    [Fact]
    public void Sanitise_removes_use_referencing_an_external_href()
    {
        var result = Sanitise("<svg xmlns='http://www.w3.org/2000/svg'><use href='http://evil.example/x.svg#a'/></svg>");

        result.Should().NotContain("evil.example");
    }

    [Fact]
    public void Sanitise_throws_when_payload_is_not_parseable_as_svg()
    {
        var bytes = Encoding.UTF8.GetBytes("not actually svg bytes");

        Action act = () => Sanitiser.Sanitise(bytes);

        act.Should().Throw<SvgUnsafeContentException>();
    }
}
