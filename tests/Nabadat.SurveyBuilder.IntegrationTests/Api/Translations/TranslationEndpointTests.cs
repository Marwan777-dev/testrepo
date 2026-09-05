using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Translations;

/// <summary>
/// T218 [US6] — API tests for <c>SurveyTranslationsController</c> (contracts/translations.md), end-to-end
/// through the real host pipeline (auth + M-01 middleware) against a Dockerised Postgres. Covers the F11
/// Translate workspace: GET a resolved locale bundle (target values + English fallback + missing keys),
/// PUT a target bundle with merge semantics (persist + echo on next GET), the per-locale coverage list,
/// the unknown-key + not-configured guards, and BR-3.2 (a survey ships usable with no Arabic saved).
/// <para>The English source bundle is derived live from the survey + its sections/questions
/// (<c>TranslatableStringExtractor</c>), so keys are asserted against the seeded graph. Enum fields are
/// integers on the wire; feature DTOs are camelCase (CLAUDE.md Backend Integration).</para>
/// </summary>
[Collection("survey-builder")]
public sealed class TranslationEndpointTests
{
    private const string ArName = "استبيان ما بعد الزيارة";
    private const string ArQuestion = "كيف كانت زيارتك؟";

    private readonly SurveyBuilderApplicationFactory _factory;

    public TranslationEndpointTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_translations_ar_resolves_every_key_to_english_and_lists_all_missing_when_none_saved()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, sectionId, questionId) = await SeedSurveyGraphAsync(client, "Post-visit survey");

        var response = await client.GetAsync($"/api/v1/surveys/{surveyId}/translations/ar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("locale").GetString().Should().Be("ar");

        var keys = body.GetProperty("keys");
        keys.GetProperty("survey.name").GetString().Should().Be("Post-visit survey");         // English fallback
        keys.GetProperty($"section.{sectionId}.title").GetString().Should().Be("Overall");     // English fallback
        keys.GetProperty($"question.{questionId}.text").GetString().Should().Be("How was it?"); // English fallback

        // With nothing translated yet, every source key is missing.
        MissingKeys(body).Should().BeEquivalentTo(new[]
        {
            "survey.name",
            $"section.{sectionId}.title",
            $"question.{questionId}.text",
        });
    }

    [Fact]
    public async Task PUT_translations_ar_persists_values_and_echoes_them_on_next_GET_with_fallback_for_the_rest()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, sectionId, questionId) = await SeedSurveyGraphAsync(client, "Post-visit survey");

        var put = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/translations/ar",
            new { keys = new Dictionary<string, string> { ["survey.name"] = ArName, [$"question.{questionId}.text"] = ArQuestion } });

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        put.Headers.ETag!.ToString().Should().Be("W/\"1\"");

        var get = await client.GetAsync($"/api/v1/surveys/{surveyId}/translations/ar");
        var body = await IntegrationHttp.ReadJsonAsync(get);
        var keys = body.GetProperty("keys");

        keys.GetProperty("survey.name").GetString().Should().Be(ArName);                    // saved Arabic echoed
        keys.GetProperty($"question.{questionId}.text").GetString().Should().Be(ArQuestion); // saved Arabic echoed
        keys.GetProperty($"section.{sectionId}.title").GetString().Should().Be("Overall");   // untranslated → English fallback

        MissingKeys(body).Should().BeEquivalentTo(new[] { $"section.{sectionId}.title" });
    }

    [Fact]
    public async Task PUT_translations_ar_merges_with_previously_saved_keys()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, _, questionId) = await SeedSurveyGraphAsync(client, "Post-visit survey");

        var first = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/translations/ar",
            new { keys = new Dictionary<string, string> { ["survey.name"] = ArName } });
        first.EnsureSuccessStatusCode();

        // Second save carries only the question key; the earlier survey.name must be preserved (merge).
        var second = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/translations/ar",
            new { keys = new Dictionary<string, string> { [$"question.{questionId}.text"] = ArQuestion } },
            ifMatch: "W/\"1\"");

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.ETag!.ToString().Should().Be("W/\"2\"");

        var body = await IntegrationHttp.ReadJsonAsync(await client.GetAsync($"/api/v1/surveys/{surveyId}/translations/ar"));
        var keys = body.GetProperty("keys");
        keys.GetProperty("survey.name").GetString().Should().Be(ArName);
        keys.GetProperty($"question.{questionId}.text").GetString().Should().Be(ArQuestion);
    }

    [Fact]
    public async Task GET_translations_lists_en_at_full_coverage_and_ar_partial_after_a_save()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, _, _) = await SeedSurveyGraphAsync(client, "Post-visit survey");

        await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/translations/ar",
            new { keys = new Dictionary<string, string> { ["survey.name"] = ArName } });

        var body = await IntegrationHttp.ReadJsonAsync(await client.GetAsync($"/api/v1/surveys/{surveyId}/translations"));
        var locales = body.GetProperty("locales").EnumerateArray().ToList();

        var en = locales.Single(l => l.GetProperty("locale").GetString() == "en");
        en.GetProperty("coveragePercent").GetInt32().Should().Be(100);

        var ar = locales.Single(l => l.GetProperty("locale").GetString() == "ar");
        ar.GetProperty("keysTranslated").GetInt32().Should().Be(1);          // only survey.name saved
        ar.GetProperty("coveragePercent").GetInt32().Should().BeInRange(1, 99);
    }

    [Fact]
    public async Task Survey_is_usable_with_english_only_when_no_arabic_is_saved()
    {
        // BR-3.2 — only the English name is required; a survey with no Arabic translation still resolves.
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, _, _) = await SeedSurveyGraphAsync(client, "English only survey");

        var response = await client.GetAsync($"/api/v1/surveys/{surveyId}/translations/ar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await IntegrationHttp.ReadJsonAsync(response);
        body.GetProperty("keys").GetProperty("survey.name").GetString().Should().Be("English only survey");
    }

    [Fact]
    public async Task DELETE_question_purges_its_translation_keys_from_every_locale_bundle()
    {
        // FR-2.8 — deleting a question purges its translations in every locale (TODO-M01-003). The purge
        // hook (TranslationStore.PurgeQuestionKeysAsync) runs inside QuestionDeletionService's transaction;
        // assert against raw storage, since a GET drops the key anyway once the question is gone.
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, sectionId, questionId) = await SeedSurveyGraphAsync(client, "Purge-on-delete host");

        var put = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/translations/ar",
            new { keys = new Dictionary<string, string> { ["survey.name"] = ArName, [$"question.{questionId}.text"] = ArQuestion } });
        put.EnsureSuccessStatusCode();

        // Sanity: the question key is present in storage before the delete.
        (await _factory.GetTranslationKeyNamesAsync(surveyId, "ar"))
            .Should().Contain($"question.{questionId}.text");

        var delete = await IntegrationHttp.DeleteAsync(
            client, $"/api/v1/surveys/{surveyId}/sections/{sectionId}/questions/{questionId}", ifMatch: "W/\"1\"");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        // The question's key is scrubbed from the stored ar bundle; the unrelated survey.name survives.
        var remaining = await _factory.GetTranslationKeyNamesAsync(surveyId, "ar");
        remaining.Should().NotContain($"question.{questionId}.text");
        remaining.Should().Contain("survey.name");
    }

    [Fact]
    public async Task PUT_translations_returns_400_when_a_key_is_unknown()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, _, _) = await SeedSurveyGraphAsync(client, "Unknown key host");

        var response = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/translations/ar",
            new { keys = new Dictionary<string, string> { ["question.00000000-0000-0000-0000-000000000000.text"] = "x" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("translation.key.unknown");
    }

    [Fact]
    public async Task PUT_translations_returns_400_when_the_locale_is_not_configured()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var (surveyId, _, _) = await SeedSurveyGraphAsync(client, "Locale gate host");

        var response = await IntegrationHttp.PutJsonAsync(
            client,
            $"/api/v1/surveys/{surveyId}/translations/fr",
            new { keys = new Dictionary<string, string> { ["survey.name"] = "Bonjour" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("translation.locale.not_configured");
    }

    private async Task<(Guid SurveyId, Guid SectionId, Guid QuestionId)> SeedSurveyGraphAsync(HttpClient client, string nameEn)
    {
        var create = await client.PostAsJsonAsync("/api/v1/surveys", new { nameEn });
        create.EnsureSuccessStatusCode();
        var surveyId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var sectionId = await _factory.SeedSectionAsync(surveyId, "Overall");
        var questionId = await _factory.SeedQuestionAsync(surveyId, sectionId, text: "How was it?");
        return (surveyId, sectionId, questionId);
    }

    private static IEnumerable<string> MissingKeys(JsonElement body) =>
        body.GetProperty("missingKeys").EnumerateArray().Select(k => k.GetString()!);
}
