using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Api.Surveys;

/// <summary>
/// T124 [US2] — API tests for the approval-workflow endpoints (contracts/approval-workflow.md),
/// driven end-to-end through the real MFA-gated bearer session. Covers submit (transition + audit +
/// M-09 broadcast), publish authorization (403 for a P-03 without the grant; 200 for a reviewer),
/// the non-destructive return-to-draft (200 + remarks recorded in the audit log), and the BR-15.1
/// PendingReview edit-lock enforced by <c>EditLockFilter</c> (403 for the P-03 submitter editing their
/// own survey; 200 for the P-01 reviewer, TODO-M01-015).
/// </summary>
public sealed class SurveyLifecycleEndpointTests : IClassFixture<SurveyBuilderApplicationFactory>
{
    private const string PublishTemplate = "survey.submitted_for_review";
    private readonly SurveyBuilderApplicationFactory _factory;

    public SurveyLifecycleEndpointTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_submit_transitions_draft_to_pending_review_and_emits_event_and_broadcast_when_actor_is_p03()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-03");
        var surveyId = await SeedPublishableDraftAsync(actor.UserId);

        var response = await SendAsync(client, HttpMethod.Post, $"/api/v1/surveys/{surveyId}/submit",
            await GetEtagAsync(client, surveyId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStatusAsync(response)).Should().Be("PendingReview");

        (await _factory.CountEventsAsync(actor.UserId, "survey.submitted_for_review")).Should().Be(1);
        _factory.Notifications.Broadcasts.Should().ContainSingle(b =>
            b.DeepLink == $"/surveys/{surveyId}"
            && b.Permission == "survey.publish"
            && b.Scope == "tenant"
            && b.Template == PublishTemplate);
    }

    [Fact]
    public async Task POST_publish_returns_403_when_caller_is_p03_without_grant()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-03");
        var surveyId = await SeedPublishableDraftAsync(actor.UserId);

        var response = await SendAsync(client, HttpMethod.Post, $"/api/v1/surveys/{surveyId}/publish",
            await GetEtagAsync(client, surveyId), idempotencyKey: Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task POST_publish_returns_200_and_activates_when_actor_is_p01()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");
        var surveyId = await SeedPublishableDraftAsync(actor.UserId);

        var response = await SendAsync(client, HttpMethod.Post, $"/api/v1/surveys/{surveyId}/publish",
            await GetEtagAsync(client, surveyId), idempotencyKey: Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStatusAsync(response)).Should().Be("Active");
        (await _factory.CountEventsAsync(actor.UserId, "survey.published")).Should().Be(1);
    }

    [Fact]
    public async Task POST_return_to_draft_returns_200_and_records_remarks_when_actor_is_p01()
    {
        // Author (P-03) submits so the survey is PendingReview.
        var (authorClient, author) = await _factory.SignedInWithActorAsync("P-03");
        var surveyId = await SeedPublishableDraftAsync(author.UserId);
        var submit = await SendAsync(authorClient, HttpMethod.Post, $"/api/v1/surveys/{surveyId}/submit",
            await GetEtagAsync(authorClient, surveyId));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        // Reviewer (P-01) returns it to Draft with remarks.
        var (reviewerClient, reviewer) = await _factory.SignedInWithActorAsync("P-01");
        const string remarks = "Fix the Arabic welcome copy.";
        var response = await SendAsync(reviewerClient, HttpMethod.Post, $"/api/v1/surveys/{surveyId}/return-to-draft",
            await GetEtagAsync(reviewerClient, surveyId), body: new { remarks });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStatusAsync(response)).Should().Be("Draft");
        (await _factory.CountEventsAsync(reviewer.UserId, "survey.status.changed")).Should().Be(1);
        (await LatestEventPayloadAsync(reviewer.UserId, "survey.status.changed")).Should().Contain(remarks);
    }

    [Fact]
    public async Task PUT_survey_returns_403_when_p03_submitter_edits_own_pending_review_survey()
    {
        // P-03 authors + submits their own survey, leaving it PendingReview with SubmittedBy == self.
        var (client, actor) = await _factory.SignedInWithActorAsync("P-03");
        var surveyId = await SeedPublishableDraftAsync(actor.UserId);
        var submit = await SendAsync(client, HttpMethod.Post, $"/api/v1/surveys/{surveyId}/submit",
            await GetEtagAsync(client, surveyId));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        // The submitter now attempts to edit their own PendingReview survey — BR-15.1 blocks it (403).
        var response = await SendAsync(client, HttpMethod.Put, $"/api/v1/surveys/{surveyId}",
            await GetEtagAsync(client, surveyId), body: new { nameEn = "Edited by submitter" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadErrorCodeAsync(response)).Should().Be("survey.edit_locked_by_pending_review");
    }

    [Fact]
    public async Task PUT_survey_returns_200_when_p01_reviewer_edits_pending_review_survey()
    {
        // P-03 authors + submits so the survey is PendingReview.
        var (authorClient, author) = await _factory.SignedInWithActorAsync("P-03");
        var surveyId = await SeedPublishableDraftAsync(author.UserId);
        var submit = await SendAsync(authorClient, HttpMethod.Post, $"/api/v1/surveys/{surveyId}/submit",
            await GetEtagAsync(authorClient, surveyId));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        // The reviewer (P-01) MAY edit while PendingReview — the filter permits it and flags it (BR-15.1).
        var (reviewerClient, _) = await _factory.SignedInWithActorAsync("P-01");
        var response = await SendAsync(reviewerClient, HttpMethod.Put, $"/api/v1/surveys/{surveyId}",
            await GetEtagAsync(reviewerClient, surveyId), body: new { nameEn = "Edited by reviewer" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Warning").Should().ContainSingle().Which.Should().Be("survey.edit_during_review");
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedPublishableDraftAsync(Guid ownerId)
    {
        var surveyId = await _factory.SeedDraftSurveyAsync(ownerId: ownerId);
        var sectionId = await _factory.SeedSectionAsync(surveyId);
        await _factory.SeedQuestionAsync(surveyId, sectionId);
        return surveyId;
    }

    private static async Task<EntityTagHeaderValue> GetEtagAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/v1/surveys/{id}");
        response.EnsureSuccessStatusCode();
        return response.Headers.ETag ?? throw new InvalidOperationException("GET did not return an ETag.");
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, EntityTagHeaderValue etag,
        object? body = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.IfMatch.Add(etag);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static async Task<string?> ReadStatusAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString();

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString();

    private async Task<string> LatestEventPayloadAsync(Guid actorId, string eventType)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT new_value::text FROM event_log WHERE actor_id = @a AND event_type = @t ORDER BY occurred_at_utc DESC LIMIT 1",
            connection);
        command.Parameters.AddWithValue("a", actorId);
        command.Parameters.AddWithValue("t", eventType);
        return (await command.ExecuteScalarAsync()) as string ?? string.Empty;
    }
}
