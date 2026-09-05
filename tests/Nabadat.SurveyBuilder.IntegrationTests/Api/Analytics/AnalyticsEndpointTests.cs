using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.SurveyBuilder.Application.Analytics.Interfaces;
using Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;
using ModuleCurrentTenant = Nabadat.SurveyBuilder.Application.Interfaces.ICurrentTenant;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Analytics;

/// <summary>
/// T267 [US9] — API tests for <c>SurveyAnalyticsController</c> (contracts/report-and-analytics.md
/// § GET /analytics), end-to-end through the real host pipeline (auth + M-01 middleware) against a
/// Dockerised Postgres and a Dockerised Elasticsearch (<see cref="EsTestcontainer"/>). Verifies that
/// the funnel counts, per-stage % of Sent, stage-to-stage conversions, headline deltas, per-channel
/// breakdown and responses trend are computed from the seeded <c>tenant_{id}_analytics</c> funnel
/// documents (FR-14.1–14.4), and that deltas are suppressed (null) when the survey has no
/// previous-period data (FR-14.5).
/// <para>The shared <see cref="SurveyBuilderApplicationFactory"/> boots the app with NO Elasticsearch
/// configured (so the module binds <c>UnavailableAnalyticsAggregator</c>). Each test therefore spins a
/// customised host via <see cref="WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/> that injects
/// the container's <see cref="ElasticsearchClient"/> and the real <c>AnalyticsAggregator</c>, so the
/// endpoint reads live ES data. The host's resolved <c>ICurrentTenant.TenantId</c> (read from the app,
/// not assumed) picks the tenant-scoped index the documents are seeded into.</para>
/// </summary>
[Collection("survey-builder")]
public sealed class AnalyticsEndpointTests : IClassFixture<EsTestcontainer>
{
    private readonly SurveyBuilderApplicationFactory _factory;
    private readonly EsTestcontainer _es;

    public AnalyticsEndpointTests(SurveyBuilderApplicationFactory factory, EsTestcontainer es)
    {
        _factory = factory;
        _es = es;
    }

    [Fact]
    public async Task GET_analytics_returns_funnel_deltas_channels_and_trend_over_the_period()
    {
        var factory = WithEs();
        var tenantId = ResolveTenantId(factory);
        await EnsureAnalyticsIndexAsync(tenantId);

        var surveyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Current window (last 7 days) — two channels summing to the SC-007 funnel 200→160→130→120.
        await SeedFunnelAsync(tenantId, surveyId, "email", now.AddDays(-1), sent: 120, opened: 100, started: 80, finished: 75);
        await SeedFunnelAsync(tenantId, surveyId, "whatsapp", now.AddDays(-2), sent: 80, opened: 60, started: 50, finished: 45);
        // Previous window (7–14 days ago) — one channel: 100 sent, 70/55/50, so every rate delta is +10 pp
        // and the Sent count delta is +100 %.
        await SeedFunnelAsync(tenantId, surveyId, "email", now.AddDays(-8), sent: 100, opened: 70, started: 55, finished: 50);

        var client = await SignedInAsync(factory);
        var response = await client.GetAsync($"/api/v1/surveys/{surveyId}/analytics?period=last_7_days&granularity=daily");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("period").GetProperty("granularity").GetString().Should().Be("daily");

        var funnel = body.GetProperty("funnel");
        var sent = funnel.GetProperty("sent");
        sent.GetProperty("count").GetInt64().Should().Be(200);
        sent.GetProperty("delta_pct").GetDecimal().Should().Be(100m); // (200 − 100) / 100 × 100

        var opened = funnel.GetProperty("opened");
        opened.GetProperty("count").GetInt64().Should().Be(160);
        opened.GetProperty("pct_of_sent").GetDecimal().Should().Be(80m);
        opened.GetProperty("delta_pp").GetDecimal().Should().Be(10m);                       // 80 − 70
        opened.GetProperty("conversion_from_prev_stage_pct").GetDecimal().Should().Be(80m); // 160 / 200

        var started = funnel.GetProperty("started");
        started.GetProperty("pct_of_sent").GetDecimal().Should().Be(65m);
        started.GetProperty("conversion_from_prev_stage_pct").GetDecimal().Should().Be(81.25m); // 130 / 160

        var finished = funnel.GetProperty("finished");
        finished.GetProperty("pct_of_sent").GetDecimal().Should().Be(60m);
        finished.GetProperty("conversion_from_prev_stage_pct").GetDecimal().Should().Be(92.31m); // 120 / 130

        var overall = body.GetProperty("overall_completion_rate");
        overall.GetProperty("value_pct").GetDecimal().Should().Be(60m);
        overall.GetProperty("delta_pp").GetDecimal().Should().Be(10m); // 60 − 50 pp (the Independent Test)

        // Channels — order is not guaranteed (ES result order), so look up by name.
        var channels = body.GetProperty("channels").EnumerateArray().ToList();
        channels.Should().HaveCount(2);
        var email = channels.Single(c => c.GetProperty("channel").GetString() == "email");
        email.GetProperty("sent").GetInt64().Should().Be(120);
        email.GetProperty("completion_rate").GetDecimal().Should().Be(0.625m); // 75 / 120
        email.GetProperty("delta_pp").GetDecimal().Should().Be(12.5m);          // 62.5 − 50
        var whatsapp = channels.Single(c => c.GetProperty("channel").GetString() == "whatsapp");
        whatsapp.GetProperty("delta_pp").ValueKind.Should().Be(JsonValueKind.Null); // no prior whatsapp ⇒ suppressed

        // Trend — daily buckets over the two current-window days; totals reconcile to the funnel.
        var trend = body.GetProperty("trend").EnumerateArray().ToList();
        trend.Should().NotBeEmpty();
        trend.Sum(t => t.GetProperty("sent").GetInt64()).Should().Be(200);
        trend.Sum(t => t.GetProperty("finished").GetInt64()).Should().Be(120);
    }

    [Fact]
    public async Task GET_analytics_suppresses_deltas_when_the_survey_has_no_previous_period()
    {
        var factory = WithEs();
        var tenantId = ResolveTenantId(factory);
        await EnsureAnalyticsIndexAsync(tenantId);

        var surveyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Current-window data only — a brand-new survey with no prior period.
        await SeedFunnelAsync(tenantId, surveyId, "email", now.AddHours(-12), sent: 10, opened: 8, started: 6, finished: 5);

        var client = await SignedInAsync(factory);
        var response = await client.GetAsync($"/api/v1/surveys/{surveyId}/analytics?period=last_1_day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var funnel = body.GetProperty("funnel");
        funnel.GetProperty("sent").GetProperty("count").GetInt64().Should().Be(10);
        // FR-14.5: no previous period ⇒ every deviation is null, never a misleading 0.
        funnel.GetProperty("sent").GetProperty("delta_pct").ValueKind.Should().Be(JsonValueKind.Null);
        funnel.GetProperty("opened").GetProperty("delta_pp").ValueKind.Should().Be(JsonValueKind.Null);
        funnel.GetProperty("finished").GetProperty("delta_pp").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("overall_completion_rate").GetProperty("delta_pp").ValueKind.Should().Be(JsonValueKind.Null);

        var channels = body.GetProperty("channels").EnumerateArray().ToList();
        channels.Should().ContainSingle();
        channels[0].GetProperty("delta_pp").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>A customised host that binds the container's ES client + the real analytics aggregator.</summary>
    private WebApplicationFactory<Program> WithEs() =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ElasticsearchClient>();
            services.AddSingleton(_es.Client);
            services.RemoveAll<IAnalyticsAggregator>();
            services.AddScoped<IAnalyticsAggregator, AnalyticsAggregator>();
        }));

    /// <summary>The tenant id the host resolves — the aggregator queries <c>tenant_{TenantId:N}_analytics</c>.</summary>
    private static Guid ResolveTenantId(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ModuleCurrentTenant>().TenantId;
    }

    /// <summary>Seeds a user and drives the real login → MFA flow against the given host, returning a bearer client.</summary>
    private async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> factory)
    {
        var actor = await _factory.SeedEnrolledUserAsync("P-01");
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = actor.Username, password = actor.Password });
        login.EnsureSuccessStatusCode();
        var challengeId = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("challengeId").GetString();

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify",
            new { challengeId, totpCode = SurveyBuilderApplicationFactory.ComputeTotp(actor.Base32Secret) });
        verify.EnsureSuccessStatusCode();
        var token = (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Creates the tenant analytics index once, mapping <c>survey_id</c>/<c>channel</c> as <c>keyword</c>
    /// (so the aggregator's <c>term survey_id</c> filter matches a hyphenated GUID rather than analysed
    /// tokens) and <c>bucket_start</c> as <c>date</c> — the shape M-04's real projection uses.
    /// </summary>
    private async Task EnsureAnalyticsIndexAsync(Guid tenantId)
    {
        var index = EsTestcontainer.AnalyticsIndex(tenantId);
        var exists = await _es.Client.Indices.ExistsAsync(index);
        if (exists.Exists)
        {
            return;
        }

        await _es.Client.Indices.CreateAsync(index, c => c
            .Mappings(m => m
                .Properties(p => p
                    .Keyword("survey_id")
                    .Keyword("channel")
                    .Date("bucket_start")
                    .LongNumber("sent")
                    .LongNumber("opened")
                    .LongNumber("started")
                    .LongNumber("finished"))));
    }

    private Task SeedFunnelAsync(
        Guid tenantId, Guid surveyId, string channel, DateTimeOffset bucketStart,
        long sent, long opened, long started, long finished)
    {
        // Seed a dictionary so the _source keys are stored verbatim (snake_case), exactly as M-04's real
        // projection writes them and as the aggregator's raw `term survey_id` / `range bucket_start`
        // queries expect — independent of how the client would name a typed object's properties.
        var doc = new Dictionary<string, object?>
        {
            ["survey_id"] = surveyId.ToString(),
            ["channel"] = channel,
            ["bucket_start"] = bucketStart,
            ["sent"] = sent,
            ["opened"] = opened,
            ["started"] = started,
            ["finished"] = finished,
        };
        return _es.SeedAnalyticsAsync(tenantId, $"{surveyId:N}-{channel}-{bucketStart.UtcDateTime:yyyyMMddHHmmss}", doc);
    }
}
