using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T107 — write-first unit tests for <c>ApprovalStateMachine</c> (T113), the pure
/// state-transition policy behind US2's Draft → PendingReview → Active loop
/// (FR-15.1/.5, BR-15.2). Submit locks the draft to its submitter and targets the
/// reviewers; Publish is gated by role + the <c>PublishOwnSurveys</c> grant on the
/// author's own survey; ReturnToDraft records reviewer remarks.
/// <para><c>ApprovalStateMachine</c> does not exist yet → this project fails to COMPILE,
/// the valid red state for a write-first story (CLAUDE.md Unit Test Policy, rule 7).</para>
/// </summary>
public sealed class ApprovalStateMachineTests
{
    private const string ProgramManager = "P-01"; // reviewer / publisher
    private const string SurveyAdmin = "P-03";     // author / submitter
    private const string PublishGrant = "PublishOwnSurveys";

    private readonly Guid _author = Guid.NewGuid();

    private static ApprovalStateMachine CreateSut() => new();

    // ── Submit ──
    [Fact]
    public void Submit_transitions_draft_to_pending_review_and_targets_reviewers_when_actor_is_p03()
    {
        var outcome = CreateSut().Submit(SurveyStatus.Draft, SurveyAdmin);

        outcome.NewStatus.Should().Be(SurveyStatus.PendingReview);
        outcome.NotificationTo.Should().Be("survey.publish");
        outcome.EditLockOwner.Should().Be(SurveyAdmin);
    }

    // ── Publish (Forbidden) ──
    [Fact]
    public void Publish_is_forbidden_when_actor_is_p03_without_grant()
    {
        var outcome = CreateSut().Publish(
            SurveyStatus.PendingReview, SurveyAdmin, grant: null, ownerId: _author, actorId: _author);

        outcome.Decision.Should().Be(PublishDecision.Forbidden);
        outcome.NewStatus.Should().Be(SurveyStatus.PendingReview); // unchanged
    }

    // ── Publish (grant escape hatch, BR-15.2) ──
    [Fact]
    public void Publish_transitions_to_active_when_p03_has_grant_on_own_survey()
    {
        var outcome = CreateSut().Publish(
            SurveyStatus.PendingReview, SurveyAdmin, grant: PublishGrant, ownerId: _author, actorId: _author);

        outcome.Decision.Should().Be(PublishDecision.Published);
        outcome.NewStatus.Should().Be(SurveyStatus.Active);
    }

    // ── Publish (grant but not the personal author → still forbidden) ──
    [Fact]
    public void Publish_is_forbidden_when_p03_has_grant_but_is_not_the_author()
    {
        var outcome = CreateSut().Publish(
            SurveyStatus.PendingReview, SurveyAdmin, grant: PublishGrant, ownerId: _author, actorId: Guid.NewGuid());

        outcome.Decision.Should().Be(PublishDecision.Forbidden);
    }

    // ── Publish (reviewer) ──
    [Fact]
    public void Publish_transitions_to_active_when_actor_is_p01()
    {
        var outcome = CreateSut().Publish(
            SurveyStatus.PendingReview, ProgramManager, grant: null, ownerId: _author, actorId: Guid.NewGuid());

        outcome.Decision.Should().Be(PublishDecision.Published);
        outcome.NewStatus.Should().Be(SurveyStatus.Active);
    }

    // ── ReturnToDraft ──
    [Fact]
    public void ReturnToDraft_transitions_to_draft_and_persists_remarks_when_actor_is_p01()
    {
        var outcome = CreateSut().ReturnToDraft(SurveyStatus.PendingReview, ProgramManager, remarks: "Fix Arabic");

        outcome.NewStatus.Should().Be(SurveyStatus.Draft);
        outcome.RemarksPersisted.Should().BeTrue();
    }
}
