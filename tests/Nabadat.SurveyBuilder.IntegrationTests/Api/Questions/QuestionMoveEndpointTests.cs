using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Questions;

/// <summary>
/// T157 [US3] — API tests for <c>QuestionsController</c>'s move endpoint
/// (<c>POST …/questions/{qid}/move</c>, contracts/questions.md), end-to-end through the real host
/// pipeline against a Dockerised Postgres. A move persists all three placement fields
/// (<c>section_id</c>, <c>set_id</c>, <c>order</c>) AND compacts sibling <c>order</c> values so both the
/// source and destination <c>(section_id, set_id)</c> containers stay contiguous and unique
/// (FR-8.2 — contracts/questions.md "Sibling order values compact within (section_id, set_id)"). A move
/// that lands the question inside a Questions Set also strips any pre-existing routing for it (FR-9.5,
/// set members can be neither routing source nor target).
/// </summary>
[Collection("survey-builder")]
public sealed class QuestionMoveEndpointTests
{
    private readonly SurveyBuilderApplicationFactory _factory;

    public QuestionMoveEndpointTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_move_across_sections_compacts_source_and_inserts_at_target_index()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client);
        var sectionA = await _factory.SeedSectionAsync(surveyId, "A", order: 0);
        var sectionB = await _factory.SeedSectionAsync(surveyId, "B", order: 1);
        // Source A: [a0, a1, a2]; destination B: [b0, b1].
        var a0 = await _factory.SeedQuestionAsync(surveyId, sectionA, text: "a0", order: 0);
        var a1 = await _factory.SeedQuestionAsync(surveyId, sectionA, text: "a1", order: 1);
        var a2 = await _factory.SeedQuestionAsync(surveyId, sectionA, text: "a2", order: 2);
        var b0 = await _factory.SeedQuestionAsync(surveyId, sectionB, text: "b0", order: 0);
        var b1 = await _factory.SeedQuestionAsync(surveyId, sectionB, text: "b1", order: 1);

        // Move the middle of A into B at index 1.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/surveys/{surveyId}/sections/{sectionA}/questions/{a1}/move",
            new { targetSectionId = sectionB, targetSetId = (Guid?)null, targetOrder = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Destination B compacts to [b0(0), a1(1), b1(2)] — inserted at index 1, the rest shifted down.
        (await OrderOf(a1)).Should().Be(1);
        (await _factory.GetQuestionPlacementAsync(a1)).SectionId.Should().Be(sectionB);
        (await OrderOf(b0)).Should().Be(0);
        (await OrderOf(b1)).Should().Be(2);

        // Source A compacts to [a0(0), a2(1)] — the vacated slot is closed, no gap.
        (await OrderOf(a0)).Should().Be(0);
        (await OrderOf(a2)).Should().Be(1);
    }

    [Fact]
    public async Task POST_move_within_a_section_reorders_siblings_contiguously()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client);
        var section = await _factory.SeedSectionAsync(surveyId, "A", order: 0);
        var q0 = await _factory.SeedQuestionAsync(surveyId, section, text: "q0", order: 0);
        var q1 = await _factory.SeedQuestionAsync(surveyId, section, text: "q1", order: 1);
        var q2 = await _factory.SeedQuestionAsync(surveyId, section, text: "q2", order: 2);

        // Drag the first question to the end of the same section.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/surveys/{surveyId}/sections/{section}/questions/{q0}/move",
            new { targetSectionId = section, targetSetId = (Guid?)null, targetOrder = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Result is contiguous and unique: [q1(0), q2(1), q0(2)].
        (await OrderOf(q1)).Should().Be(0);
        (await OrderOf(q2)).Should().Be(1);
        (await OrderOf(q0)).Should().Be(2);
    }

    [Fact]
    public async Task POST_move_into_set_inserts_at_index_compacts_members_and_strips_routing()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client);
        var sectionA = await _factory.SeedSectionAsync(surveyId, "A", order: 0);
        var sectionB = await _factory.SeedSectionAsync(surveyId, "B", order: 1);
        var setId = await _factory.SeedQuestionsSetAsync(sectionB, count: 0, order: 0);
        // The set already has two members [m0, m1].
        var m0 = await _factory.SeedQuestionAsync(surveyId, sectionB, text: "m0", order: 0, setId: setId);
        var m1 = await _factory.SeedQuestionAsync(surveyId, sectionB, text: "m1", order: 1, setId: setId);
        var questionId = await _factory.SeedQuestionAsync(surveyId, sectionA, order: 0);
        // The question is currently a routing source — moving it into a set must remove that route (FR-9.5).
        await _factory.SeedRoutingAsync(surveyId, questionId, answerKey: "1", targetQuestionId: null);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/surveys/{surveyId}/sections/{sectionA}/questions/{questionId}/move",
            new { targetSectionId = sectionB, targetSetId = (Guid?)setId, targetOrder = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var placement = await _factory.GetQuestionPlacementAsync(questionId);
        placement.SectionId.Should().Be(sectionB);
        placement.SetId.Should().Be(setId);

        // Set members compact to [m0(0), moved(1), m1(2)].
        (await OrderOf(m0)).Should().Be(0);
        (await OrderOf(questionId)).Should().Be(1);
        (await OrderOf(m1)).Should().Be(2);
        (await _factory.CountRoutingForQuestionAsync(questionId)).Should().Be(0);
    }

    [Fact]
    public async Task POST_move_returns_404_when_the_question_does_not_exist()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var surveyId = await CreateSurveyAsync(client);
        var sectionA = await _factory.SeedSectionAsync(surveyId, "A", order: 0);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/surveys/{surveyId}/sections/{sectionA}/questions/{Guid.NewGuid()}/move",
            new { targetSectionId = sectionA, targetSetId = (Guid?)null, targetOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await IntegrationHttp.ErrorCodeAsync(response)).Should().Be("question.not_found");
    }

    private static async Task<Guid> CreateSurveyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/surveys", new { nameEn = "Move host" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<int> OrderOf(Guid questionId) =>
        (await _factory.GetQuestionPlacementAsync(questionId)).Order;
}
