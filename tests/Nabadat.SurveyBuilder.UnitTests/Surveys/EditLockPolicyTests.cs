using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T108 — write-first unit tests for <c>EditLockPolicy</c> (T114). BR-15.1: while a
/// survey is PendingReview its submitter (P-03) is edit-locked; the reviewer (P-01)
/// may still edit before publishing. Matches the contract rule in
/// <c>contracts/approval-workflow.md</c> § "Edit-lock behaviour on PendingReview".
/// <para><c>EditLockPolicy</c> does not exist yet → the project fails to COMPILE (valid red).</para>
/// </summary>
public sealed class EditLockPolicyTests
{
    private const string ProgramManager = "P-01";
    private const string SurveyAdmin = "P-03";

    private readonly Guid _submitter = Guid.NewGuid();

    private static EditLockPolicy CreateSut() => new();

    [Fact]
    public void Evaluate_locks_edit_when_submitter_p03_opens_own_pending_review_survey()
    {
        var survey = new EditLockState(SurveyStatus.PendingReview, SubmittedByUserId: _submitter);

        var result = CreateSut().Evaluate(SurveyAdmin, callerUserId: _submitter, survey);

        result.CanEdit.Should().BeFalse();
        result.Reason.Should().Be("survey.edit_locked_by_pending_review");
    }

    [Fact]
    public void Evaluate_permits_edit_when_reviewer_p01_opens_pending_review_survey()
    {
        var survey = new EditLockState(SurveyStatus.PendingReview, SubmittedByUserId: _submitter);

        var result = CreateSut().Evaluate(ProgramManager, callerUserId: Guid.NewGuid(), survey);

        result.CanEdit.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_permits_edit_when_survey_is_draft_even_for_p03()
    {
        var survey = new EditLockState(SurveyStatus.Draft, SubmittedByUserId: null);

        var result = CreateSut().Evaluate(SurveyAdmin, callerUserId: _submitter, survey);

        result.CanEdit.Should().BeTrue();
    }
}
