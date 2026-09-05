using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T041 [US1] — unit tests for <c>StatusTransitionPolicy</c>, the Application-layer wrapper over the
/// authoritative Survey Status Transition Matrix (<see cref="SurveyStatusTransitions"/>, BR-1.4) that
/// additionally enforces the §3.15 "unpublished pending review" lock.
/// <para>
/// Contract pinned for the implementer (T069):
/// <list type="bullet">
///   <item><c>StatusTransitionPolicy</c> lives in <c>Application/Surveys/</c>.</item>
///   <item><c>bool Allowed(SurveyStatus current, SurveyStatus next, string actorRole,
///   bool isDestructive = false, bool hasUnpublishedPendingReview = false)</c> delegates the base
///   matrix to <see cref="SurveyStatusTransitions.AllowedTransitions"/> and then denies
///   <c>Draft → Active</c> while <paramref name="hasUnpublishedPendingReview"/> is true (§3.15 lock).</item>
///   <item>Role tokens are the shared <see cref="SurveyStatusTransitions.ManagerRole"/> (P-01) and
///   <see cref="SurveyStatusTransitions.AuthorRole"/> (P-03) constants.</item>
/// </list>
/// </para>
/// </summary>
public sealed class StatusTransitionPolicyTests
{
    private const string Manager = SurveyStatusTransitions.ManagerRole;
    private const string Author = SurveyStatusTransitions.AuthorRole;

    private readonly StatusTransitionPolicy _policy = new();

    [Fact]
    public void Allowed_returns_false_for_archived_to_active()
    {
        _policy.Allowed(SurveyStatus.Archived, SurveyStatus.Active, Manager).Should().BeFalse();
    }

    [Fact]
    public void Allowed_returns_true_for_archived_to_draft_unarchive()
    {
        // BR-1.3 / FR-1.14 — Archived is terminal except Unarchive → Draft.
        _policy.Allowed(SurveyStatus.Archived, SurveyStatus.Draft, Manager).Should().BeTrue();
    }

    [Fact]
    public void Allowed_returns_false_for_draft_to_active_when_an_unpublished_pending_review_exists()
    {
        // §3.15 lock — a draft cannot be published while a pending-review version is outstanding.
        _policy.Allowed(SurveyStatus.Draft, SurveyStatus.Active, Manager, hasUnpublishedPendingReview: true)
            .Should().BeFalse();
    }

    [Fact]
    public void Allowed_returns_true_for_draft_to_active_when_no_pending_review_lock()
    {
        _policy.Allowed(SurveyStatus.Draft, SurveyStatus.Active, Manager, hasUnpublishedPendingReview: false)
            .Should().BeTrue();
    }

    [Theory]
    // Representative rows of the authoritative matrix (spec.md → Survey Status Transition Matrix).
    [InlineData(SurveyStatus.Draft, SurveyStatus.PendingReview, "P-03", false, true)]
    [InlineData(SurveyStatus.Draft, SurveyStatus.PendingReview, "P-01", false, true)]
    [InlineData(SurveyStatus.PendingReview, SurveyStatus.Active, "P-01", false, true)]
    [InlineData(SurveyStatus.PendingReview, SurveyStatus.Draft, "P-01", false, true)]
    [InlineData(SurveyStatus.Active, SurveyStatus.Paused, "P-01", false, true)]
    [InlineData(SurveyStatus.Paused, SurveyStatus.Active, "P-01", false, true)]
    [InlineData(SurveyStatus.Active, SurveyStatus.Archived, "P-01", false, true)]
    [InlineData(SurveyStatus.Active, SurveyStatus.Draft, "P-01", true, true)]   // destructive Return-to-Draft (BR-1.6)
    [InlineData(SurveyStatus.Active, SurveyStatus.Draft, "P-01", false, false)] // same transition without the destructive flag → denied
    [InlineData(SurveyStatus.Active, SurveyStatus.Paused, "P-03", false, false)] // Author cannot pause
    public void Allowed_matches_the_authoritative_matrix(
        SurveyStatus current, SurveyStatus next, string actorRole, bool isDestructive, bool expected)
    {
        _policy.Allowed(current, next, actorRole, isDestructive).Should().Be(expected);
    }
}
