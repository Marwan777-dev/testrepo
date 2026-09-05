using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.RenderPlan;

/// <summary>
/// T158 [US3] — FR-10.4 low-response ordering, end-to-end against real Postgres + Elasticsearch. A
/// survey with three sections, each holding a <c>low_response</c> Questions Set, whose lowest-response
/// questions are (7, 4, 12), must render sections least-answered-first → [section2, section1, section3]
/// (research.md §7 Set → Section → Survey cascade). Response counts are seeded into the tenant
/// <c>tenant_{tenantId}_analytics</c> index and read back through the real <c>ResponseCountReader</c>.
///
/// <para>The first test drives the published <see cref="ISurveyRenderService"/> directly (the AD-01
/// seam M-02/M-04 consume); the T150 tests below also drive the <c>GET …/render-plan</c> HTTP route,
/// now wired to that service (TODO-M01-019 resolved). See <see cref="RenderPlanApplicationFactory"/>.</para>
/// </summary>
[Collection("render-plan")]
public sealed class RenderPlanEndpointTests
{
    private readonly RenderPlanApplicationFactory _factory;

    public RenderPlanEndpointTests(RenderPlanApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetRenderPlan_orders_sections_least_answered_first_across_the_survey()
    {
        var surveyId = await _factory.SeedActiveSurveyAsync();

        // section1 lowest = 7, section2 lowest = 4, section3 lowest = 12  →  expected [s2, s1, s3].
        var (section1, set1) = await SeedSectionWithLowResponseSetAsync(surveyId, order: 0, memberCounts: new long[] { 7, 20 });
        var (section2, set2) = await SeedSectionWithLowResponseSetAsync(surveyId, order: 1, memberCounts: new long[] { 4, 9 });
        var (section3, set3) = await SeedSectionWithLowResponseSetAsync(surveyId, order: 2, memberCounts: new long[] { 12, 30 });

        var plan = await _factory.InScopeAsync(render => render.GetRenderPlanAsync(
            new SurveyId(surveyId), new RespondentContext(Guid.NewGuid(), new LocaleCode("en")), CancellationToken.None));

        plan.Sections.Select(s => s.SectionId).Should().Equal(section2, section1, section3);
        // Every section still renders its set (count = 2 = full membership); no route overrides seeded.
        plan.Sections.SelectMany(s => s.Items).OfType<RenderSetSample>().Should().HaveCount(3);
        plan.RoutingMap.Should().BeEmpty();
    }

    [Fact]
    public async Task GET_render_plan_endpoint_orders_sections_least_answered_first_end_to_end()
    {
        // T150 — the same (7,4,12) fixture as above, but driven through the real HTTP route
        // (GET …/render-plan) now that it is wired to ISurveyRenderService. Resolves TODO-M01-019.
        var surveyId = await _factory.SeedActiveSurveyAsync();
        var (section1, _) = await SeedSectionWithLowResponseSetAsync(surveyId, order: 0, memberCounts: new long[] { 7, 20 });
        var (section2, _) = await SeedSectionWithLowResponseSetAsync(surveyId, order: 1, memberCounts: new long[] { 4, 9 });
        var (section3, _) = await SeedSectionWithLowResponseSetAsync(surveyId, order: 2, memberCounts: new long[] { 12, 30 });

        var client = await _factory.SignedInClientAsync();
        var response = await client.GetAsync($"/api/v1/surveys/{surveyId}/render-plan?respondent_id={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var sectionOrder = body.GetProperty("sectionsOrder").EnumerateArray()
            .Select(s => Guid.Parse(s.GetProperty("sectionId").GetString()!))
            .ToList();
        sectionOrder.Should().Equal(section2, section1, section3);

        // Each section renders its low_response set as a "set" item (count = full membership).
        var setItems = body.GetProperty("sectionsOrder").EnumerateArray()
            .SelectMany(s => s.GetProperty("items").EnumerateArray())
            .Count(i => i.GetProperty("kind").GetString() == "set");
        setItems.Should().Be(3);
    }

    [Fact]
    public async Task GET_render_plan_endpoint_returns_404_for_an_unknown_survey()
    {
        var client = await _factory.SignedInClientAsync();

        var response = await client.GetAsync($"/api/v1/surveys/{Guid.NewGuid()}/render-plan?respondent_id={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Seeds a section + one low_response set (count = every member) whose members carry the given response counts.</summary>
    private async Task<(Guid SectionId, Guid SetId)> SeedSectionWithLowResponseSetAsync(
        Guid surveyId, int order, long[] memberCounts)
    {
        var sectionId = await _factory.SeedSectionAsync(surveyId, order);
        var setId = await _factory.SeedSetAsync(sectionId, "low_response", count: memberCounts.Length, order: 0);
        for (var i = 0; i < memberCounts.Length; i++)
        {
            var questionId = await _factory.SeedQuestionAsync(surveyId, sectionId, setId, order: i);
            await _factory.SeedResponseCountAsync(questionId, memberCounts[i]);
        }

        return (sectionId, setId);
    }
}
