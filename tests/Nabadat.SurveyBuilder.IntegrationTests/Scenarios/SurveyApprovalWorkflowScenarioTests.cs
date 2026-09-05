using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Scenarios;

/// <summary>
/// T125 [US2] — end-to-end approval-workflow scenario (spec.md US2 Independent Test). Walks the full
/// business journey: a P-03 saves a Draft and submits it → a reviewer notification fans out → a P-01
/// deep-links to the survey and publishes → the survey is Active with a complete audit trail. Also
/// exercises the FR-15.5 self-publish-grant variant (a granted P-03 publishes their own survey
/// directly, with no reviewer notification).
/// </summary>
public sealed class SurveyApprovalWorkflowScenarioTests : IClassFixture<SurveyBuilderApplicationFactory>
{
    private const string PublishOwnSurveysGrant = "PublishOwnSurveys";
    private readonly SurveyBuilderApplicationFactory _factory;

    public SurveyApprovalWorkflowScenarioTests(SurveyBuilderApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task NewSurvey_submitted_by_p03_is_published_by_p01_with_full_audit_trail()
    {
        // 1. P-03 authors a Draft and submits it for review.
        var (authorClient, author) = await _factory.SignedInWithActorAsync("P-03");
        var surveyId = await SeedPublishableDraftAsync(author.UserId);

        var submit = await SendAsync(authorClient, $"/api/v1/surveys/{surveyId}/submit",
            await GetEtagAsync(authorClient, surveyId));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStatusAsync(submit)).Should().Be("PendingReview");

        // 2. A reviewer notification fanned out to survey.publish holders, deep-linked to Settings (Q7).
        _factory.Notifications.Broadcasts.Should().Contain(b =>
            b.DeepLink == $"/surveys/{surveyId}" && b.Permission == "survey.publish");

        // 3. A reviewer (P-01) lands on the deep-link Settings screen (its Pending-review state was
        // already confirmed by the submit response above; SurveyView serializes status as an int, so
        // we only assert the deep link is reachable here).
        var (reviewerClient, reviewer) = await _factory.SignedInWithActorAsync("P-01");
        var settings = await reviewerClient.GetAsync($"/api/v1/surveys/{surveyId}");
        settings.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. The reviewer publishes → Active.
        var publish = await SendAsync(reviewerClient, $"/api/v1/surveys/{surveyId}/publish",
            settings.Headers.ETag!, idempotencyKey: Guid.NewGuid().ToString());
        publish.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStatusAsync(publish)).Should().Be("Active");

        // 5. The audit trail records both the submit (by the author) and the publish (by the reviewer).
        (await _factory.CountEventsAsync(author.UserId, "survey.submitted_for_review")).Should().Be(1);
        (await _factory.CountEventsAsync(reviewer.UserId, "survey.published")).Should().Be(1);
    }

    [Fact]
    public async Task P03_with_publish_grant_publishes_own_survey_directly_without_reviewer_notification()
    {
        var (client, author) = await _factory.SignedInWithActorAsync("P-03");
        _factory.Permissions.AllowGrant(author.UserId, PublishOwnSurveysGrant); // FR-15.5 grant
        var surveyId = await SeedPublishableDraftAsync(author.UserId);

        // The granted author publishes their own Draft directly — no submit-for-review step.
        var publish = await SendAsync(client, $"/api/v1/surveys/{surveyId}/publish",
            await GetEtagAsync(client, surveyId), idempotencyKey: Guid.NewGuid().ToString());

        publish.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStatusAsync(publish)).Should().Be("Active");
        (await _factory.CountEventsAsync(author.UserId, "survey.published")).Should().Be(1);

        // No reviewer was notified for this survey (the submit step was skipped, BR-15.2).
        _factory.Notifications.Broadcasts.Should().NotContain(b => b.DeepLink == $"/surveys/{surveyId}");
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
        HttpClient client, string path, EntityTagHeaderValue etag, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.IfMatch.Add(etag);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task<string?> ReadStatusAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString();
}
