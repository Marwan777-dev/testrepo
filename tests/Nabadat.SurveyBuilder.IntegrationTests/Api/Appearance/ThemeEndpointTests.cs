using System.Net;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Appearance;

/// <summary>
/// T101 [US1] — API tests for <c>SurveyThemesController</c> (F4, contracts/surveys.md theme routes),
/// end-to-end against a Dockerised Postgres. Covers Inherited-mode resolution (from the tenant design
/// guidelines) and the Customize-save validation (an Image background requires a file handle).
/// <para>Enum fields are integers on the wire: ThemeMode Inherited=0/Customized=1, BackgroundType
/// Solid=0/Image=2. The POST <c>/theme/logo</c> multipart upload is NOT covered — it is not
/// implemented yet (IFileStorageService unwired, TODO-M01-006); T083 shipped GET/PUT only.</para>
/// </summary>
[Collection("survey-builder")]
public sealed class ThemeEndpointTests
{
    private const int ThemeModeCustomized = 1;
    private const int BackgroundSolid = 0;
    private const int BackgroundImage = 2;

    private readonly SurveyBuilderApplicationFactory _factory;

    public ThemeEndpointTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_theme_resolves_inherited_tokens_from_the_tenant_guidelines()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await _factory.SeedDraftSurveyAsync("Inherited appearance");

        var response = await client.GetAsync($"/api/v1/surveys/{surveyId}/theme");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        // Default Nabadat palette served by the M-11 placeholder (DevTenantDesignGuidelinesReader).
        body.GetProperty("primaryColour").GetString().Should().Be("#0D8BBC");
    }

    [Fact]
    public async Task PUT_theme_returns_400_when_an_image_background_has_no_file_handle()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await _factory.SeedDraftSurveyAsync("Bad image theme");

        var response = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/theme",
            new { mode = ThemeModeCustomized, backgroundType = BackgroundImage, backgroundImageHandle = (string?)null, primaryColour = "#123456" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("theme.background_image.required");
    }

    [Fact]
    public async Task PUT_theme_returns_200_when_saving_a_valid_solid_customize_theme()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await _factory.SeedDraftSurveyAsync("Solid theme");

        var response = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/theme",
            new { mode = ThemeModeCustomized, backgroundType = BackgroundSolid, backgroundImageHandle = (string?)null, primaryColour = "#0D8BBC" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Skip = "POST /theme/logo multipart upload is not implemented (IFileStorageService unwired, TODO-M01-006); T083 shipped GET/PUT theme only.")]
    public Task POST_theme_logo_uploads_via_file_storage() => Task.CompletedTask;
}
