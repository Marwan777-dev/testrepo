using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;
using static Nabadat.SurveyBuilder.IntegrationTests.Infrastructure.ReportApplicationFactory;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Report;

/// <summary>
/// T248 [US8] — API integration tests for <c>GET /api/v1/surveys/{id}/report</c> (F13). Drives the
/// real <c>ReportAggregator</c> against a Testcontainers Elasticsearch cluster seeded with response
/// documents (M-04's AD-04 projection shape), through the authenticated HTTP endpoint. Verifies the
/// metric cards, headline CSAT gauge, and per-question views (FR-13.2/13.3), and that responses
/// submitted after the survey's active period are excluded from the live report (FR-13.6).
/// </summary>
[Collection("report")]
public sealed class ReportEndpointTests
{
    private readonly ReportApplicationFactory _factory;

    public ReportEndpointTests(ReportApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_report_returns_metric_cards_and_headline_csat_for_in_period_responses()
    {
        var client = await _factory.SignedInClientAsync();
        var surveyId = await _factory.SeedActiveSurveyAsync(); // no active period ⇒ every in-window response counts
        var sectionId = await _factory.SeedSectionAsync(surveyId);
        var csat1 = await _factory.SeedQuestionAsync(surveyId, sectionId, "KPI", "None", 0, """{"$type":"kpi"}""", kpiCode: "csat_a");
        var csat2 = await _factory.SeedQuestionAsync(surveyId, sectionId, "KPI", "None", 1, """{"$type":"kpi"}""", kpiCode: "csat_b");

        var now = DateTimeOffset.UtcNow;
        var submitted = now.AddDays(-1);
        var sent = now.AddDays(-2);
        var times = new[] { 60, 90, 120, 150 };            // median = (90+120)/2 = 105
        var touchpoints = new[] { "tp-1", "tp-1", "tp-2", "tp-2" }; // 2 distinct touchpoints
        for (var i = 0; i < 4; i++)
        {
            await _factory.SeedResponseAsync(
                surveyId, submitted, sent, completed: i < 3, completionTimeSeconds: times[i],
                channel: "email", touchpointId: touchpoints[i],
                answers: new[]
                {
                    Answer(csat1, kpiFamily: "csat", numericValue: 81m),
                    Answer(csat2, kpiFamily: "csat", numericValue: 76m),
                });
        }

        var response = await client.GetAsync($"/api/v1/surveys/{surveyId}/report?period=last_7_days");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var cards = body.GetProperty("metric_cards");
        cards.GetProperty("responses").GetInt32().Should().Be(4);
        cards.GetProperty("completion_rate").GetDecimal().Should().Be(0.75m);
        cards.GetProperty("median_time_seconds").GetInt32().Should().Be(105);
        cards.GetProperty("touchpoints").GetInt32().Should().Be(2);

        // Headline CSAT is the average of the two CSAT questions' averages: (81 + 76) / 2 = 78.5 (FR-13.2).
        body.GetProperty("headline_kpis").GetProperty("csat").GetProperty("value").GetDecimal().Should().Be(78.5m);
    }

    [Fact]
    public async Task GET_report_maps_multi_select_and_text_questions_to_their_FR_13_3_views()
    {
        var client = await _factory.SignedInClientAsync();
        var surveyId = await _factory.SeedActiveSurveyAsync();
        var sectionId = await _factory.SeedSectionAsync(surveyId);
        var multi = await _factory.SeedQuestionAsync(
            surveyId, sectionId, "MultiSelect", "None", 0, """{"$type":"multi_select","options":["Email","Phone","Chat"]}""");
        var text = await _factory.SeedQuestionAsync(
            surveyId, sectionId, "InputField", "Text", 1, """{"$type":"input_field"}""");

        var now = DateTimeOffset.UtcNow;
        var sent = now.AddDays(-2);
        // Two respondents: both pick Email; one adds Phone, the other Chat (multi-select totals may exceed 100%).
        await _factory.SeedResponseAsync(surveyId, now.AddDays(-1), sent, true, 100, "email", "tp-1",
            new[] { Answer(multi, optionLabels: new[] { "Email", "Phone" }), Answer(text, text: "Great service") });
        await _factory.SeedResponseAsync(surveyId, now.AddDays(-1), sent, true, 100, "whatsapp", "tp-1",
            new[] { Answer(multi, optionLabels: new[] { "Email", "Chat" }), Answer(text, text: "Fast and helpful") });

        var body = await ReadReportAsync(client, surveyId);
        var perQuestion = body.GetProperty("per_question").EnumerateArray().ToList();

        var multiCard = perQuestion.Single(q => q.GetProperty("question_id").GetGuid() == multi);
        multiCard.GetProperty("type").GetString().Should().Be("MultiSelect");
        var multiView = multiCard.GetProperty("view");
        multiView.GetProperty("kind").GetString().Should().Be("bar_with_counts_and_pct");
        multiView.GetProperty("respondents_base").GetInt32().Should().Be(2);
        var emailBucket = multiView.GetProperty("distribution").EnumerateArray()
            .Single(b => b.GetProperty("label").GetString() == "Email");
        emailBucket.GetProperty("count").GetInt32().Should().Be(2);
        emailBucket.GetProperty("pct_of_respondents").GetDecimal().Should().Be(100m);

        var textCard = perQuestion.Single(q => q.GetProperty("question_id").GetGuid() == text);
        var textView = textCard.GetProperty("view");
        textView.GetProperty("kind").GetString().Should().Be("verbatim_sample");
        textView.GetProperty("sample_size_max").GetInt32().Should().Be(100);
        textView.GetProperty("sample").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GET_report_excludes_responses_submitted_after_the_active_period_elapsed()
    {
        var client = await _factory.SignedInClientAsync();
        var surveyId = await _factory.SeedActiveSurveyAsync(activePeriodDays: 3);
        var sectionId = await _factory.SeedSectionAsync(surveyId);
        var q = await _factory.SeedQuestionAsync(surveyId, sectionId, "Scale", "Stars", 0, """{"$type":"scale","pointCount":5}""");

        var now = DateTimeOffset.UtcNow;
        var sent = now.AddDays(-5);
        // In-window: submitted 1 day after it was sent (≤ the 3-day active period).
        await _factory.SeedResponseAsync(surveyId, sent.AddDays(1), sent, true, 90, "email", "tp-1",
            new[] { Answer(q, numericValue: 5m) });
        // Late: submitted 4 days after it was sent (> the 3-day active period) yet still within last_7_days.
        await _factory.SeedResponseAsync(surveyId, sent.AddDays(4), sent, true, 90, "email", "tp-1",
            new[] { Answer(q, numericValue: 1m) });

        var body = await ReadReportAsync(client, surveyId);

        // FR-13.6: only the in-window response is counted; the late arrival is excluded.
        body.GetProperty("metric_cards").GetProperty("responses").GetInt32().Should().Be(1);
    }

    private static async Task<JsonElement> ReadReportAsync(HttpClient client, Guid surveyId)
    {
        var response = await client.GetAsync($"/api/v1/surveys/{surveyId}/report?period=last_7_days");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
