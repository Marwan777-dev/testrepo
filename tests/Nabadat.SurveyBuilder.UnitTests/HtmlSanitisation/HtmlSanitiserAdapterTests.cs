using FluentAssertions;
using Nabadat.SurveyBuilder.Application.HtmlSanitisation;
using Nabadat.SurveyBuilder.Application.HtmlSanitisation.Interfaces;
using Nabadat.SurveyBuilder.Infrastructure.HtmlSanitisation;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.HtmlSanitisation;

/// <summary>
/// T051 [US1] — unit tests for the <see cref="GannsHtmlSanitiserAdapter"/> (Q3 allowlist, research.md
/// §1). Safe markup survives; <c>script</c>/<c>iframe</c>/<c>object</c>/<c>embed</c> are dropped;
/// <c>javascript:</c> URLs and <c>on*</c> handlers are stripped. The adapter is the foundational
/// sanitiser (T029) already in place — these tests pin its Q3 behaviour.
/// <para>The adapter is exercised through the <see cref="IHtmlSanitiser"/> port with the active
/// <see cref="SanitiserPolicyVersion.V1"/> allowlist.</para>
/// </summary>
public sealed class HtmlSanitiserAdapterTests
{
    private readonly IHtmlSanitiser _sanitiser = new GannsHtmlSanitiserAdapter();

    private SanitisedResult Sanitise(string input) => _sanitiser.Sanitise(input, SanitiserPolicyVersion.V1);

    [Fact]
    public void Sanitise_preserves_allowed_markup()
    {
        var result = Sanitise("<p>hi</p>");

        result.Html.Should().Be("<p>hi</p>");
        result.WasModified.Should().BeFalse();
    }

    [Fact]
    public void Sanitise_strips_a_script_tag_entirely()
    {
        var result = Sanitise("<script>alert(1)</script>");

        result.Html.Should().BeEmpty();
        result.WasModified.Should().BeTrue();
    }

    [Fact]
    public void Sanitise_strips_a_javascript_url_scheme_while_keeping_the_anchor_text()
    {
        var result = Sanitise("<a href=\"javascript:alert(1)\">x</a>");

        result.Html.Should().NotContain("javascript:");
        result.Html.Should().Contain("x");
        result.WasModified.Should().BeTrue();
    }

    [Fact]
    public void Sanitise_strips_an_iframe()
    {
        var result = Sanitise("<iframe src=\"https://evil.example\"></iframe>");

        result.Html.Should().NotContain("<iframe");
        result.WasModified.Should().BeTrue();
    }

    [Fact]
    public void Sanitise_strips_an_inline_event_handler_attribute()
    {
        var result = Sanitise("<a onclick=\"steal()\">x</a>");

        result.Html.Should().NotContain("onclick");
        result.WasModified.Should().BeTrue();
    }
}
