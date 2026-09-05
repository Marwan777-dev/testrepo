using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Templates;

/// <summary>
/// T201 [US5] — API tests for <c>TemplatesController</c> (contracts/templates.md), end-to-end through
/// the real host pipeline (auth + M-01 middleware) against a Dockerised Postgres. Covers save-as-template
/// (FR-7.4, bindings captured), instantiate (FR-6.3, journey/stage/touchpoint copied exactly), the
/// built-in edit lock (FR-7.1 → 403), and name/tag search (FR-6.2).
/// <para>Enum fields are integers on the wire (no <c>JsonStringEnumConverter</c> — CLAUDE.md Backend
/// Integration): TemplateClass BuiltIn=0/Customized=1; ThemeMode Inherited=0/Customized=1.</para>
/// </summary>
[Collection("survey-builder")]
public sealed class TemplateEndpointTests
{
    private readonly SurveyBuilderApplicationFactory _factory;

    public TemplateEndpointTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_templates_returns_201_with_etag_and_captures_bindings_when_saved_from_a_survey()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var journeyId = Guid.NewGuid();
        var surveyId = await _factory.SeedJourneyBoundSurveyAsync(journeyId, "Post-visit satisfaction");
        var sectionId = await _factory.SeedSectionAsync(surveyId);
        await _factory.SeedKpiQuestionAsync(surveyId, sectionId, "CSAT", Guid.NewGuid(), Guid.NewGuid());

        var response = await IntegrationHttp.PostJsonAsync(
            client, "/api/v1/templates",
            new { sourceSurveyId = surveyId, nameEn = "Post-visit template", tags = new[] { "cx" } },
            idempotencyKey: Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag!.ToString().Should().Be("W/\"1\"");
        response.Headers.Location.Should().NotBeNull();
        var created = await IntegrationHttp.ReadJsonAsync(response);
        var templateId = created.GetProperty("id").GetGuid();

        // The redacted summary proves the snapshot captured the section, the question, and its KPI binding.
        var view = await IntegrationHttp.ReadJsonAsync(await client.GetAsync($"/api/v1/templates/{templateId}"));
        view.GetProperty("sectionCount").GetInt32().Should().Be(1);
        view.GetProperty("questionCount").GetInt32().Should().Be(1);
        view.GetProperty("hasKpiBindings").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task POST_instantiate_returns_201_with_a_new_survey_whose_bindings_match_the_template()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var journeyId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var touchpointId = Guid.NewGuid();
        var surveyId = await _factory.SeedJourneyBoundSurveyAsync(journeyId, "Source survey");
        var sectionId = await _factory.SeedSectionAsync(surveyId);
        await _factory.SeedKpiQuestionAsync(surveyId, sectionId, "NPS", stageId, touchpointId);
        var templateId = await CreateTemplateAsync(client, surveyId, "Reusable NPS template");

        var response = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/templates/{templateId}/instantiate", new { }, idempotencyKey: Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        var newSurveyId = body.GetProperty("id").GetGuid();
        newSurveyId.Should().NotBe(surveyId);

        (await _factory.GetSurveyBoundJourneyAsync(newSurveyId)).Should().Be(journeyId);
        var bindings = await _factory.GetKpiBindingsForSurveyAsync(newSurveyId);
        bindings.Should().ContainSingle()
            .Which.Should().Be(("NPS", (Guid?)stageId, (Guid?)touchpointId));
    }

    [Fact]
    public async Task POST_instantiate_copies_the_arabic_translations_and_remaps_their_keys_to_the_new_questions()
    {
        // FR-7.4 / TODO-M01-022 — save-as-template copies translations; instantiate re-persists them
        // with section.{id}.* / question.{id}.* keys remapped onto the regenerated rows.
        const string arName = "استبيان قابل لإعادة الاستخدام";
        const string arQuestion = "ما مدى رضاك؟";
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await _factory.SeedJourneyBoundSurveyAsync(Guid.NewGuid(), "Localized source");
        var sectionId = await _factory.SeedSectionAsync(surveyId);
        var questionId = await _factory.SeedKpiQuestionAsync(surveyId, sectionId, "CSAT", Guid.NewGuid(), Guid.NewGuid());

        var put = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/translations/ar",
            new { keys = new Dictionary<string, string> { ["survey.name"] = arName, [$"question.{questionId}.text"] = arQuestion } });
        put.EnsureSuccessStatusCode();

        var templateId = await CreateTemplateAsync(client, surveyId, "Localized template");
        var instantiate = await IntegrationHttp.PostJsonAsync(
            client, $"/api/v1/templates/{templateId}/instantiate", new { }, idempotencyKey: Guid.NewGuid().ToString());
        instantiate.StatusCode.Should().Be(HttpStatusCode.Created);
        var newSurveyId = (await IntegrationHttp.ReadJsonAsync(instantiate)).GetProperty("id").GetGuid();

        // The new survey's Arabic bundle carries the survey-level string verbatim and the question string
        // under the NEW question id — proving both the copy and the id remap on instantiate.
        var newQuestionId = (await _factory.GetQuestionIdsForSurveyAsync(newSurveyId)).Should().ContainSingle().Subject;
        var body = await IntegrationHttp.ReadJsonAsync(await client.GetAsync($"/api/v1/surveys/{newSurveyId}/translations/ar"));
        var keys = body.GetProperty("keys");
        keys.GetProperty("survey.name").GetString().Should().Be(arName);
        keys.GetProperty($"question.{newQuestionId}.text").GetString().Should().Be(arQuestion);

        // The question string resolved from the saved Arabic (not English fallback) → not reported missing.
        var missing = body.GetProperty("missingKeys").EnumerateArray().Select(k => k.GetString()).ToList();
        missing.Should().NotContain($"question.{newQuestionId}.text");
    }

    [Fact]
    public async Task PATCH_template_returns_403_when_the_template_is_built_in()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var templateId = await _factory.SeedBuiltInTemplateAsync("Banking CX baseline", new[] { "Banking" });

        var response = await IntegrationHttp.PatchJsonAsync(
            client, $"/api/v1/templates/{templateId}", new { nameEn = "Renamed" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("template.built_in_not_editable");
    }

    [Fact]
    public async Task GET_templates_matches_on_name_or_tag_when_searching()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var byName = await _factory.SeedTemplateAsync("Onboarding pulse");
        var byTag = await _factory.SeedCustomizedTemplateWithTagsAsync("Branch visit", new[] { "onboarding" });
        var noMatch = await _factory.SeedTemplateAsync("Weekly ops review");

        var response = await client.GetAsync("/api/v1/templates?search=onboarding");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        var ids = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(byName);
        ids.Should().Contain(byTag);
        ids.Should().NotContain(noMatch);
    }

    private static async Task<Guid> CreateTemplateAsync(HttpClient client, Guid sourceSurveyId, string nameEn)
    {
        var response = await IntegrationHttp.PostJsonAsync(
            client, "/api/v1/templates",
            new { sourceSurveyId, nameEn }, idempotencyKey: Guid.NewGuid().ToString());
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
