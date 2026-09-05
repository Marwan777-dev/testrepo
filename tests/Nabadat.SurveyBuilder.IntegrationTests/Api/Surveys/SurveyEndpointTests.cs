using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Surveys;

/// <summary>
/// T100 [US1] — API tests for <c>SurveysController</c> (contracts/surveys.md), end-to-end through the
/// real host pipeline (auth + M-01 middleware) against a Dockerised Postgres. Covers create/read/update
/// with ETag optimistic locking, the BR-1.7 publish gate, and the Archived → only-unarchive rule.
/// <para>Enum fields are integers on the wire (no <c>JsonStringEnumConverter</c> — CLAUDE.md Backend
/// Integration): SurveyStatus Draft=0, Active=2, Archived=4.</para>
/// <para>Two contract bullets are intentionally NOT covered here and are marked Skipped below with the
/// reason: the Pause rules-confirmation path (the <c>IChannelSurveyRulesReader</c> dev stub returns 0
/// — TODO-M01-012) and the <c>POST /questions</c> KPI-binding validation (the QuestionsController is
/// T149/US3 — not built yet; the rule itself is covered by <c>KpiBindingValidatorTests</c>, T046).</para>
/// </summary>
[Collection("survey-builder")]
public sealed class SurveyEndpointTests
{
    private const int StatusActive = 2;
    private const int StatusArchived = 4;
    private const int StatusDraft = 0;

    private readonly SurveyBuilderApplicationFactory _factory;

    public SurveyEndpointTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_surveys_returns_201_with_etag_and_location_when_input_is_valid()
    {
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync("/api/v1/surveys", new { nameEn = "Post-visit satisfaction" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag!.ToString().Should().Be("W/\"1\"");
        response.Headers.Location.Should().NotBeNull();
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("nameEn").GetString().Should().Be("Post-visit satisfaction");
        body.GetProperty("status").GetInt32().Should().Be(StatusDraft);
    }

    [Fact]
    public async Task GET_survey_returns_settings_payload_and_etag_when_it_exists()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await CreateSurveyAsync(client, "Deep-link target");

        var response = await client.GetAsync($"/api/v1/surveys/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.ToString().Should().Be("W/\"1\"");
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("id").GetGuid().Should().Be(id);
        body.GetProperty("nameEn").GetString().Should().Be("Deep-link target");
    }

    [Fact]
    public async Task PUT_survey_returns_200_and_new_etag_when_if_match_matches()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await CreateSurveyAsync(client, "Before");

        var response = await IntegrationHttp.PutJsonAsync(
            client, $"/api/v1/surveys/{id}", new { nameEn = "After" }, ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.ToString().Should().Be("W/\"2\"");
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("nameEn").GetString().Should().Be("After");
    }

    [Fact]
    public async Task PUT_survey_returns_409_conflict_when_if_match_is_stale()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await CreateSurveyAsync(client, "Race");

        var response = await IntegrationHttp.PutJsonAsync(
            client, $"/api/v1/surveys/{id}", new { nameEn = "Loser" }, ifMatch: "W/\"99\"");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("survey.conflict");
    }

    [Fact]
    public async Task PUT_survey_returns_400_when_if_match_is_missing()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await CreateSurveyAsync(client, "No precondition");

        var response = await IntegrationHttp.PutJsonAsync(client, $"/api/v1/surveys/{id}", new { nameEn = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("survey.etag_required");
    }

    [Fact]
    public async Task POST_status_returns_409_publish_requires_content_when_survey_is_empty()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await CreateSurveyAsync(client, "Empty");

        var response = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{id}/status", new { to = StatusActive }, ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("publish.requires_content");
        body.GetProperty("error").GetProperty("details").GetProperty("missing_sections").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task POST_status_returns_200_and_activates_when_content_is_present()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await CreateSurveyAsync(client, "Publishable");
        var sectionId = await _factory.SeedSectionAsync(id);
        await _factory.SeedQuestionAsync(id, sectionId);

        var response = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{id}/status", new { to = StatusActive }, ifMatch: "W/\"1\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("status").GetInt32().Should().Be(StatusActive);
    }

    [Fact]
    public async Task POST_status_returns_409_archived_only_unarchive_when_activating_an_archived_survey()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await _factory.SeedActiveSurveyAsync("Archived one");

        var archive = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{id}/status", new { to = StatusArchived }, ifMatch: "W/\"1\"");
        archive.StatusCode.Should().Be(HttpStatusCode.OK);
        var archivedEtag = archive.Headers.ETag!.ToString();

        var response = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{id}/status", new { to = StatusActive }, ifMatch: archivedEtag);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("survey.archived.only_unarchive_allowed");
    }

    [Fact]
    public async Task POST_status_returns_200_and_unarchives_when_returning_an_archived_survey_to_draft()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await _factory.SeedActiveSurveyAsync("To unarchive");

        var archive = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{id}/status", new { to = StatusArchived }, ifMatch: "W/\"1\"");
        archive.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{id}/status", new { to = StatusDraft }, ifMatch: archive.Headers.ETag!.ToString());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("status").GetInt32().Should().Be(StatusDraft);
    }

    [Fact]
    public async Task GET_surveys_filters_by_status()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var activeId = await _factory.SeedActiveSurveyAsync("Active filter target");
        await _factory.SeedDraftSurveyAsync("Draft filter target");

        var response = await client.GetAsync($"/api/v1/surveys?status={StatusActive}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(i => i.GetProperty("status").GetInt32() == StatusActive);
        items.Should().Contain(i => i.GetProperty("id").GetGuid() == activeId);
    }

    [Fact]
    public async Task GET_surveys_returns_empty_list_when_the_search_matches_nothing()
    {
        // FR-1.3 — when the combined filters/search match nothing, the list is an explicit empty result
        // (the UI renders its "no results" state), NOT an error. Seed a real survey so the empty result
        // is the search filter's doing, not an empty tenant.
        var client = await _factory.SignedInClientAsync("P-01");
        await _factory.SeedActiveSurveyAsync("A survey that really exists");

        var response = await client.GetAsync("/api/v1/surveys?q=zzz-no-such-survey-name-9f3c1a");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("items").EnumerateArray().Should().BeEmpty();
    }

    [Fact(Skip = "Pause rules-confirmation needs rules_count > 0; the IChannelSurveyRulesReader dev stub returns 0 until M-02 (TODO-M01-012).")]
    public Task POST_status_paused_returns_409_requires_rules_confirmation_when_rules_bound() => Task.CompletedTask;

    [Fact(Skip = "POST /questions is the QuestionsController (T149, US3) — not built yet. The kpi.touchpoint.requires_stage rule is covered by KpiBindingValidatorTests (T046).")]
    public Task POST_questions_kpi_returns_400_touchpoint_requires_stage() => Task.CompletedTask;

    private static async Task<Guid> CreateSurveyAsync(HttpClient client, string nameEn)
    {
        var response = await client.PostAsJsonAsync("/api/v1/surveys", new { nameEn });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
