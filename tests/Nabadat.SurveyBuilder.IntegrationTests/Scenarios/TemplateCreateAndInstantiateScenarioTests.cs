using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Scenarios;

/// <summary>
/// T202 [US5] — scenario test for the F6/F7 template journey (spec.md US5 Independent Test): a P-01
/// saves a survey (with a journey binding + appearance) as a template, instantiates a new survey from
/// it, and confirms the settings, appearance, questions AND journey/stage/touchpoint bindings all
/// carried (FR-7.4 / FR-6.3). It then deletes the template and re-fetches the instantiated survey to
/// prove there is no cascade — the new survey is independent (Q4 / BR-7.1 snapshot-no-link).
/// <para>Enum fields are integers on the wire (no <c>JsonStringEnumConverter</c>): ThemeMode
/// Inherited=0 / Customized=1.</para>
/// </summary>
[Collection("survey-builder")]
public sealed class TemplateCreateAndInstantiateScenarioTests
{
    private const int ThemeModeCustomized = 1;
    private const string ArName = "استبيان ما بعد الزيارة";
    private const string ArQuestion = "ما مدى رضاك؟";

    private readonly SurveyBuilderApplicationFactory _factory;

    public TemplateCreateAndInstantiateScenarioTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task P01_saves_a_journey_bound_survey_as_a_template_instantiates_it_and_delete_does_not_cascade()
    {
        var client = await _factory.SignedInClientAsync("P-01");

        // ── Arrange: a journey-bound, Customize-themed survey with a KPI question ──────────────────
        var journeyId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var touchpointId = Guid.NewGuid();
        const string primaryColor = "#0D8BBC";
        var sourceId = await _factory.SeedJourneyBoundSurveyAsync(journeyId, "Post-visit satisfaction", themeMode: "Customized");
        var sectionId = await _factory.SeedSectionAsync(sourceId, "Experience");
        var questionId = await _factory.SeedKpiQuestionAsync(sourceId, sectionId, "CSAT", stageId, touchpointId);
        await _factory.SeedThemeAsync(sourceId, primaryColor);

        // An Arabic translation bundle on the source survey (FR-7.4 copy-all — TODO-M01-022).
        var putAr = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{sourceId}/translations/ar",
            new { keys = new Dictionary<string, string> { ["survey.name"] = ArName, [$"question.{questionId}.text"] = ArQuestion } });
        putAr.EnsureSuccessStatusCode();

        // ── Save as template ──────────────────────────────────────────────────────────────────────
        var create = await IntegrationHttp.PostJsonAsync(
            client, "/api/v1/templates",
            new { sourceSurveyId = sourceId, nameEn = "Post-visit template" }, idempotencyKey: Guid.NewGuid().ToString());
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var templateId = (await IntegrationHttp.ReadJsonAsync(create)).GetProperty("id").GetGuid();
        var templateEtag = create.Headers.ETag!.ToString();

        // ── Instantiate a new survey from the template ─────────────────────────────────────────────
        var instantiate = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/templates/{templateId}/instantiate", new { }, idempotencyKey: Guid.NewGuid().ToString());
        instantiate.StatusCode.Should().Be(HttpStatusCode.Created);
        var newSurvey = await IntegrationHttp.ReadJsonAsync(instantiate);
        var newSurveyId = newSurvey.GetProperty("id").GetGuid();
        newSurveyId.Should().NotBe(sourceId);

        // ── Assert: settings carried ────────────────────────────────────────────────────────────────
        newSurvey.GetProperty("nameEn").GetString().Should().Be("Post-visit satisfaction");
        newSurvey.GetProperty("boundJourneyId").GetGuid().Should().Be(journeyId);
        newSurvey.GetProperty("themeMode").GetInt32().Should().Be(ThemeModeCustomized);

        // ── Assert: appearance carried ────────────────────────────────────────────────────────────
        (await _factory.GetThemePrimaryColorAsync(newSurveyId)).Should().Be(primaryColor);

        // ── Assert: questions + journey/stage/touchpoint bindings carried ────────────────────────────
        (await _factory.GetSurveyBoundJourneyAsync(newSurveyId)).Should().Be(journeyId);
        var bindings = await _factory.GetKpiBindingsForSurveyAsync(newSurveyId);
        bindings.Should().ContainSingle()
            .Which.Should().Be(("CSAT", (Guid?)stageId, (Guid?)touchpointId));

        // ── Assert: Arabic translations carried, with the question key remapped to the new question ──
        var newQuestionId = (await _factory.GetQuestionIdsForSurveyAsync(newSurveyId)).Should().ContainSingle().Subject;
        var arBundle = await IntegrationHttp.ReadJsonAsync(
            await client.GetAsync($"/api/v1/surveys/{newSurveyId}/translations/ar"));
        var arKeys = arBundle.GetProperty("keys");
        arKeys.GetProperty("survey.name").GetString().Should().Be(ArName);
        arKeys.GetProperty($"question.{newQuestionId}.text").GetString().Should().Be(ArQuestion);

        // ── Delete the template — must NOT cascade to the instantiated survey (BR-7.1) ──────────────
        var delete = await IntegrationHttp.DeleteAsync(client, $"/api/v1/templates/{templateId}", ifMatch: templateEtag);
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        (await _factory.TemplateExistsAsync(templateId)).Should().BeFalse();
        (await _factory.SurveyExistsAsync(newSurveyId)).Should().BeTrue();

        var refetch = await client.GetAsync($"/api/v1/surveys/{newSurveyId}");
        refetch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IntegrationHttp.ReadJsonAsync(refetch)).GetProperty("boundJourneyId").GetGuid().Should().Be(journeyId);
        (await _factory.GetKpiBindingsForSurveyAsync(newSurveyId)).Should().ContainSingle();
    }
}
