using System.Net;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Routing;

/// <summary>
/// T181 [US4] — API tests for <c>SurveyRoutingController</c> (F9 answer routing, contracts/questions.md),
/// end-to-end through the real host pipeline against a Dockerised Postgres. Exercises the survey-level
/// toggle (<c>POST …/routing</c>) and the per-question map (<c>PUT</c>/<c>GET …/questions/{qid}/routing</c>):
/// the layout gate (FR-9.1), the shuffle lock on enable, map persistence + verbatim read-back, and the
/// two eligibility rejections (set target, slider source — FR-9.5). Mirrors the unit contracts pinned by
/// the Routing unit suite (T163–T166) at the HTTP layer. Writes carry <c>If-Match</c> — the toggle
/// against <c>survey.row_version</c>, the map save against <c>question.row_version</c> (both <c>W/"1"</c>
/// for a freshly seeded row).
/// </summary>
[Collection("survey-builder")]
public sealed class RoutingEndpointTests
{
    private const string SeedEtag = "W/\"1\"";

    private readonly SurveyBuilderApplicationFactory _factory;

    public RoutingEndpointTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_routing_returns_409_layout_required_when_layout_is_not_one_question_per_page()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await _factory.SeedDraftSurveyAsync();
        // Layout stays 'section' (the seed default) — routing is only available one-question-per-page.

        var response = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{surveyId}/routing", new { enabled = true, confirm = true }, ifMatch: SeedEtag);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("routing.layout_required");
    }

    [Fact]
    public async Task POST_routing_returns_200_and_locks_shuffle_when_enabled_on_a_question_layout()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await _factory.SeedDraftSurveyAsync();
        await _factory.SetSurveyLayoutAsync(surveyId, "question", shuffle: true);

        var response = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{surveyId}/routing", new { enabled = true, confirm = true }, ifMatch: SeedEtag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("routingOn").GetBoolean().Should().BeTrue();
        body.GetProperty("shuffleLocked").GetBoolean().Should().BeTrue();
        body.GetProperty("shuffle").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task POST_routing_returns_409_confirmation_required_when_enabling_without_confirm()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await _factory.SeedDraftSurveyAsync();
        await _factory.SetSurveyLayoutAsync(surveyId, "question");

        var response = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{surveyId}/routing", new { enabled = true, confirm = false }, ifMatch: SeedEtag);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("routing.confirmation_required");
    }

    [Fact]
    public async Task PUT_routing_returns_200_and_persists_the_per_answer_map()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, source, target) = await SeedRoutableSurveyAsync();

        var response = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/questions/{source}/routing",
            new { map = new Dictionary<string, string> { ["1"] = target.ToString() } },
            ifMatch: SeedEtag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("hasRouting").GetBoolean().Should().BeTrue();
        body.GetProperty("map").GetProperty("1").GetString().Should().Be(target.ToString());
        // Persisted as exactly one override row for the source.
        (await _factory.CountRoutingForQuestionAsync(source)).Should().Be(1);
    }

    [Fact]
    public async Task GET_routing_returns_the_saved_map_verbatim()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, source, target) = await SeedRoutableSurveyAsync();
        var saved = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/questions/{source}/routing",
            new { map = new Dictionary<string, string> { ["1"] = target.ToString() } },
            ifMatch: SeedEtag);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync($"/api/v1/surveys/{surveyId}/questions/{source}/routing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("hasRouting").GetBoolean().Should().BeTrue();
        body.GetProperty("map").GetProperty("1").GetString().Should().Be(target.ToString());
    }

    [Fact]
    public async Task PUT_routing_returns_400_target_ineligible_when_the_target_is_inside_a_set()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await _factory.SeedDraftSurveyAsync();
        await _factory.SetSurveyLayoutAsync(surveyId, "question");
        var section = await _factory.SeedSectionAsync(surveyId, "S", order: 0);
        var source = await _factory.SeedQuestionAsync(surveyId, section, type: "Scale", subtype: "Stars", order: 0);
        var set = await _factory.SeedQuestionsSetAsync(section, count: 0, order: 1);
        var setMember = await _factory.SeedQuestionAsync(surveyId, section, type: "Scale", subtype: "Stars", order: 2, setId: set);

        var response = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/questions/{source}/routing",
            new { map = new Dictionary<string, string> { ["1"] = setMember.ToString() } },
            ifMatch: SeedEtag);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("routing.target_ineligible");
    }

    [Fact]
    public async Task PUT_routing_returns_409_source_ineligible_when_the_source_is_a_slider_scale()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await _factory.SeedDraftSurveyAsync();
        await _factory.SetSurveyLayoutAsync(surveyId, "question");
        var section = await _factory.SeedSectionAsync(surveyId, "S", order: 0);
        var slider = await _factory.SeedQuestionAsync(surveyId, section, type: "Scale", subtype: "Slider", order: 0);
        var target = await _factory.SeedQuestionAsync(surveyId, section, type: "Scale", subtype: "Stars", order: 1);

        var response = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/questions/{slider}/routing",
            new { map = new Dictionary<string, string> { ["1"] = target.ToString() } },
            ifMatch: SeedEtag);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("routing.source_ineligible");
    }

    /// <summary>Seeds a one-question-per-page survey with two standalone, routing-eligible Scale questions in order.</summary>
    private async Task<(Guid SurveyId, Guid Source, Guid Target)> SeedRoutableSurveyAsync()
    {
        var surveyId = await _factory.SeedDraftSurveyAsync();
        await _factory.SetSurveyLayoutAsync(surveyId, "question");
        var section = await _factory.SeedSectionAsync(surveyId, "S", order: 0);
        var source = await _factory.SeedQuestionAsync(surveyId, section, type: "Scale", subtype: "Stars", order: 0);
        var target = await _factory.SeedQuestionAsync(surveyId, section, type: "Scale", subtype: "Stars", order: 1);
        return (surveyId, source, target);
    }
}
