using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Scenarios;

/// <summary>
/// T102 [US1] — scenario test for the US1 Independent Test: a P-01 walks a survey from creation to
/// Active and it shows up Active in the Survey Library. The journey spans multiple endpoints with
/// state (the survey id + ETag) carried between them, and asserts the final state-of-the-world
/// (per CLAUDE.md Unit Test Policy rule 11).
/// <para>The section + question are seeded directly (the Sections/Questions controllers are T147–T149,
/// US3), so this exercises the survey lifecycle — create → publish-gate satisfied → Active — through
/// the real API. Enum fields are integers on the wire (SurveyStatus Draft=0, Active=2).</para>
/// </summary>
[Collection("survey-builder")]
public sealed class SurveyLifecycleFromDraftToActiveScenarioTests
{
    private const int StatusActive = 2;

    private readonly SurveyBuilderApplicationFactory _factory;

    public SurveyLifecycleFromDraftToActiveScenarioTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task NewSurvey_goes_from_draft_to_active_and_appears_active_in_the_library()
    {
        var client = await _factory.SignedInClientAsync("P-01");

        // 1. Create the Draft survey (F5 Continue out of Settings).
        var create = await client.PostAsJsonAsync("/api/v1/surveys", new { nameEn = "Post-visit satisfaction" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var surveyId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var etag = create.Headers.ETag!.ToString();

        // 2. Add one section with one question (F8 builder — seeded; Sections/Questions API is US3).
        var sectionId = await _factory.SeedSectionAsync(surveyId, "Experience");
        await _factory.SeedQuestionAsync(surveyId, sectionId, type: "Scale", subtype: "Stars", text: "How was your visit?");

        // 3. Publish → Active (BR-1.7 gate now satisfied: 1 section + 1 question).
        var activate = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/surveys/{surveyId}/status", new { to = StatusActive }, ifMatch: etag);
        activate.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. The survey now reads Active on its detail (row-click deep-link) …
        var detail = await client.GetAsync($"/api/v1/surveys/{surveyId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32().Should().Be(StatusActive);

        // 5. … and appears in the Library filtered to Active.
        var library = await client.GetAsync($"/api/v1/surveys?status={StatusActive}");
        library.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await library.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToList();
        items.Should().Contain(i =>
            i.GetProperty("id").GetGuid() == surveyId && i.GetProperty("status").GetInt32() == StatusActive);
    }

    [Fact(Skip = "M-17 audit assertion blocked: IEventLogWriter resolves to NoOpEventLogWriter (audit events dropped) until the M-17 adapter is wired — TODO-M01-011. Re-enable and assert CountEventsAsync(actor, \"survey.published\") == 1 once wired.")]
    public Task NewSurvey_emits_survey_created_and_published_audit_events() => Task.CompletedTask;
}
