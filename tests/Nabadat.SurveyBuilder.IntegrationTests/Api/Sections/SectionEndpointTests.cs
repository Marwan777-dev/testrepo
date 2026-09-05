using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Sections;

/// <summary>
/// T155 [US3] — API tests for <c>SectionsController</c> (contracts/sections-and-sets.md), end-to-end
/// through the real host pipeline (auth + M-01 middleware) against a Dockerised Postgres. Covers
/// create / edit / delete, including the FR-2.3 "last remaining section is deletable" rule and the
/// FR-2.5 destructive-delete confirmation gate (409 with the child-count <c>details</c> breakdown the
/// client renders in the confirmation prompt).
/// <para>Writes carry an <c>If-Match</c> of the SECTION's <c>row_version</c> (a freshly seeded section
/// is at <c>W/"1"</c>); enum fields are integers on the wire (CLAUDE.md Backend Integration).</para>
/// </summary>
[Collection("survey-builder")]
public sealed class SectionEndpointTests
{
    private readonly SurveyBuilderApplicationFactory _factory;

    public SectionEndpointTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_sections_returns_201_with_location_and_etag_when_input_is_valid()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client, "Section host");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/surveys/{surveyId}/sections", new { name = "General", order = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag!.ToString().Should().Be("W/\"1\"");
        response.Headers.Location.Should().NotBeNull();
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("name").GetString().Should().Be("General");
    }

    [Fact]
    public async Task PATCH_section_returns_200_and_new_etag_when_if_match_matches()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client, "Rename host");
        var sectionId = await _factory.SeedSectionAsync(surveyId, "Before");

        var response = await IntegrationHttp.PatchJsonAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}", new { name = "After" }, ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.ToString().Should().Be("W/\"2\"");
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("name").GetString().Should().Be("After");
    }

    [Fact]
    public async Task DELETE_section_returns_200_when_it_is_the_last_remaining_empty_section()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client, "Lone section host");
        var sectionId = await _factory.SeedSectionAsync(surveyId, "Only one");

        var response = await IntegrationHttp.DeleteAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}", ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DELETE_nonempty_section_returns_409_with_child_breakdown_when_confirm_is_absent()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client, "Busy section host");
        var sectionId = await _factory.SeedSectionAsync(surveyId, "Full");
        await _factory.SeedQuestionAsync(surveyId, sectionId, text: "Standalone", order: 0);
        var setId = await _factory.SeedQuestionsSetAsync(sectionId, count: 1, order: 1);
        await _factory.SeedQuestionAsync(surveyId, sectionId, text: "In set", order: 2, setId: setId);

        var response = await IntegrationHttp.DeleteAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}", ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("section.delete.requires_confirmation");
        var details = body.GetProperty("error").GetProperty("details");
        details.GetProperty("standalone_questions").GetInt32().Should().Be(1);
        details.GetProperty("questions_sets").GetInt32().Should().Be(1);
        details.GetProperty("set_questions").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task DELETE_nonempty_section_cascades_children_when_confirm_is_true()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client, "Cascade host");
        var sectionId = await _factory.SeedSectionAsync(surveyId, "Full");
        var standaloneId = await _factory.SeedQuestionAsync(surveyId, sectionId, text: "Standalone", order: 0);
        var setId = await _factory.SeedQuestionsSetAsync(sectionId, count: 1, order: 1);
        var memberId = await _factory.SeedQuestionAsync(surveyId, sectionId, text: "In set", order: 2, setId: setId);

        var response = await IntegrationHttp.DeleteAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}?confirm=true", ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _factory.QuestionExistsAsync(standaloneId)).Should().BeFalse();
        (await _factory.QuestionExistsAsync(memberId)).Should().BeFalse();
        (await _factory.QuestionsSetExistsAsync(setId)).Should().BeFalse();
        (await _factory.SectionExistsAsync(sectionId)).Should().BeFalse();
    }

    [Fact]
    public async Task PATCH_section_returns_409_conflict_when_if_match_is_stale()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client, "Stale host");
        var sectionId = await _factory.SeedSectionAsync(surveyId, "Contested");

        var response = await IntegrationHttp.PatchJsonAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}", new { name = "Loser" }, ifMatch: "W/\"99\"");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("section.conflict");
    }

    private static async Task<Guid> CreateSurveyAsync(HttpClient client, string nameEn)
    {
        var response = await client.PostAsJsonAsync("/api/v1/surveys", new { nameEn });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
