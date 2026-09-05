using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.QuestionsSets;

/// <summary>
/// T156 [US3] — API tests for <c>QuestionsSetsController</c> (contracts/sections-and-sets.md), end-to-end
/// through the real host pipeline against a Dockerised Postgres. Covers create / edit / delete of a
/// rotating Questions Set, the cross-row <c>count &lt;= member-count</c> ceiling (FR-10.3), and the
/// FR-2.6 destructive-delete confirmation gate (409 with the <c>questions_count</c> detail).
/// <para>Writes carry an <c>If-Match</c> of the SET's <c>row_version</c> (Q1 / contract note); enum
/// <c>selection_mode</c> is an integer on the wire — Random=0, LowResponse=1 (CLAUDE.md Backend
/// Integration).</para>
/// </summary>
[Collection("survey-builder")]
public sealed class QuestionsSetEndpointTests
{
    private const int SelectionRandom = 0;
    private const int SelectionLowResponse = 1;

    private readonly SurveyBuilderApplicationFactory _factory;

    public QuestionsSetEndpointTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_set_returns_201_with_location_and_etag_when_input_is_valid()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, sectionId) = await CreateSurveyAndSectionAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/surveys/{surveyId}/sections/{sectionId}/sets",
            new { title = "Rotating pool", selectionMode = SelectionRandom, count = 0, order = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag!.ToString().Should().Be("W/\"1\"");
        response.Headers.Location.Should().NotBeNull();
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("title").GetString().Should().Be("Rotating pool");
        body.GetProperty("selectionMode").GetInt32().Should().Be(SelectionRandom);
    }

    [Fact]
    public async Task PATCH_set_returns_200_and_new_etag_when_count_is_within_member_count()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, sectionId) = await CreateSurveyAndSectionAsync(client);
        var setId = await _factory.SeedQuestionsSetAsync(sectionId, count: 0);
        await _factory.SeedQuestionAsync(surveyId, sectionId, order: 0, setId: setId);
        await _factory.SeedQuestionAsync(surveyId, sectionId, order: 1, setId: setId);

        var response = await IntegrationHttp.PatchJsonAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}/sets/{setId}",
            new { title = "Low-response pool", selectionMode = SelectionLowResponse, count = 2, order = 0 },
            ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.ToString().Should().Be("W/\"2\"");
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("count").GetInt32().Should().Be(2);
        body.GetProperty("selectionMode").GetInt32().Should().Be(SelectionLowResponse);
    }

    [Fact]
    public async Task PATCH_set_returns_400_when_count_exceeds_member_count()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, sectionId) = await CreateSurveyAndSectionAsync(client);
        var setId = await _factory.SeedQuestionsSetAsync(sectionId, count: 0);
        await _factory.SeedQuestionAsync(surveyId, sectionId, order: 0, setId: setId); // one member

        var response = await IntegrationHttp.PatchJsonAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}/sets/{setId}",
            new { title = "Too greedy", count = 5, order = 0 }, ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("questionsset.count.exceeds_size");
    }

    [Fact]
    public async Task DELETE_empty_set_returns_200_without_confirmation()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, sectionId) = await CreateSurveyAndSectionAsync(client);
        var setId = await _factory.SeedQuestionsSetAsync(sectionId, count: 0);

        var response = await IntegrationHttp.DeleteAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}/sets/{setId}", ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _factory.QuestionsSetExistsAsync(setId)).Should().BeFalse();
    }

    [Fact]
    public async Task DELETE_nonempty_set_returns_409_with_questions_count_when_confirm_is_absent()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, sectionId) = await CreateSurveyAndSectionAsync(client);
        var setId = await _factory.SeedQuestionsSetAsync(sectionId, count: 0);
        await _factory.SeedQuestionAsync(surveyId, sectionId, order: 0, setId: setId);
        await _factory.SeedQuestionAsync(surveyId, sectionId, order: 1, setId: setId);

        var response = await IntegrationHttp.DeleteAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}/sets/{setId}", ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("questionsset.delete.requires_confirmation");
        body.GetProperty("error").GetProperty("details").GetProperty("questions_count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task DELETE_nonempty_set_cascades_member_questions_when_confirm_is_true()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, sectionId) = await CreateSurveyAndSectionAsync(client);
        var setId = await _factory.SeedQuestionsSetAsync(sectionId, count: 0);
        var memberId = await _factory.SeedQuestionAsync(surveyId, sectionId, order: 0, setId: setId);

        var response = await IntegrationHttp.DeleteAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}/sets/{setId}?confirm=true", ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _factory.QuestionsSetExistsAsync(setId)).Should().BeFalse();
        (await _factory.QuestionExistsAsync(memberId)).Should().BeFalse();
    }

    private async Task<(Guid SurveyId, Guid SectionId)> CreateSurveyAndSectionAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/surveys", new { nameEn = "Set host" });
        response.EnsureSuccessStatusCode();
        var surveyId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var sectionId = await _factory.SeedSectionAsync(surveyId, "Section");
        return (surveyId, sectionId);
    }
}
