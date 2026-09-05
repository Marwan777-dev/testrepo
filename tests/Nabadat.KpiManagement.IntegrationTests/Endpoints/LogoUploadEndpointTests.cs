using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Nabadat.KpiManagement.Application.Organization;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Endpoints;

/// <summary>
/// T145 [US6] — HTTP tests for <c>POST /api/v1/tenant/organization/logo</c> (contracts/settings-api.md).
/// Covers the happy PNG path, the unsupported content-type rejection, and the SVG sanitisation
/// surface: a benign SVG's PERSISTED bytes equal the sanitiser output (verified by re-fetching the
/// served logo and byte-comparing), a <c>&lt;script&gt;</c>-bearing SVG is stored stripped, and a
/// non-parseable payload is rejected with <c>LOGO_SVG_UNSAFE_CONTENT</c>.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class LogoUploadEndpointTests
{
    private const string LogoRoute = "/api/v1/tenant/organization/logo";

    private readonly KpiManagementApplicationFactory _factory;

    public LogoUploadEndpointTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_logo_returns_200_and_a_url_when_png_is_valid()
    {
        await _factory.ResetOrganizationAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03 };

        var response = await UploadAsync(client, png, "image/png", "logo.png");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("url").GetString().Should().Be(LogoRoute);
        body.GetProperty("was_sanitised").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task POST_logo_returns_400_logo_content_type_unsupported_when_file_is_pdf()
    {
        await _factory.ResetOrganizationAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var pdf = Encoding.UTF8.GetBytes("%PDF-1.7 fake");

        var response = await UploadAsync(client, pdf, "application/pdf", "logo.pdf");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("LOGO_CONTENT_TYPE_UNSUPPORTED");
    }

    [Fact]
    public async Task POST_logo_persists_sanitiser_output_when_svg_is_benign()
    {
        await _factory.ResetOrganizationAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var svg = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><circle r='5'/></svg>");

        var upload = await UploadAsync(client, svg, "image/svg+xml", "logo.svg");
        upload.StatusCode.Should().Be(HttpStatusCode.OK);

        // The persisted bytes must equal the sanitiser's output for the same input.
        var expected = new SvgSanitiser().Sanitise(svg);
        var fetched = await (await client.GetAsync(LogoRoute)).Content.ReadAsByteArrayAsync();
        fetched.Should().Equal(expected);
    }

    [Fact]
    public async Task POST_logo_strips_script_when_svg_contains_script()
    {
        await _factory.ResetOrganizationAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var svg = Encoding.UTF8.GetBytes(
            "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script><circle r='5'/></svg>");

        var upload = await UploadAsync(client, svg, "image/svg+xml", "logo.svg");
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        (await upload.ReadJsonAsync()).GetProperty("was_sanitised").GetBoolean().Should().BeTrue();

        var fetched = await (await client.GetAsync(LogoRoute)).Content.ReadAsByteArrayAsync();
        Encoding.UTF8.GetString(fetched).Should().NotContain("<script");
    }

    [Fact]
    public async Task POST_logo_returns_400_logo_svg_unsafe_content_when_payload_is_not_parseable()
    {
        await _factory.ResetOrganizationAsync();
        var client = await _factory.SignedInClientAsync(PersonaContextHelper.CxProgramManager);
        var garbage = Encoding.UTF8.GetBytes("not actually svg bytes");

        var response = await UploadAsync(client, garbage, "image/svg+xml", "logo.svg");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("LOGO_SVG_UNSAFE_CONTENT");
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, byte[] bytes, string contentType, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "logo", fileName);
        return await client.PostAsync(LogoRoute, content);
    }
}
