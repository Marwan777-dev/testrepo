using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Nabadat.SurveyBuilder.UnitTests.TestSupport;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T111 — write-first unit tests for <c>AuditEventFactory</c> (T117). Every
/// approval-workflow action produces exactly one M-17 audit event carrying the correct
/// payload shape — actor, timestamp, remarks, correlation_id, previous_status,
/// new_status — and the constitution-registered event type (AMENDMENT-012:
/// <c>survey.submitted_for_review</c>, <c>survey.published</c>, <c>survey.status.changed</c>).
/// Timestamp comes from an injected <see cref="TimeProvider"/> (rule 8).
/// <para><c>AuditEventFactory</c> does not exist yet → the project fails to COMPILE (valid red).</para>
/// </summary>
public sealed class AuditEventFactoryTests
{
    private readonly SurveyId _survey = new(Guid.NewGuid());
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _correlation = Guid.NewGuid();

    private AuditEventFactory CreateSut() => new(TestTime.Provider());

    [Fact]
    public void Submitted_builds_one_submitted_for_review_event_with_full_payload()
    {
        var evt = CreateSut().Submitted(_survey, _actor, _correlation);

        evt.EventType.Should().Be("survey.submitted_for_review");
        evt.Survey.Should().Be(_survey);
        evt.ActorId.Should().Be(_actor);
        evt.CorrelationId.Should().Be(_correlation);
        evt.PreviousStatus.Should().Be(SurveyStatus.Draft);
        evt.NewStatus.Should().Be(SurveyStatus.PendingReview);
        evt.OccurredAt.Should().Be(TestTime.Anchor);
    }

    [Fact]
    public void Published_builds_one_published_event_carrying_remarks_and_previous_status()
    {
        var evt = CreateSut().Published(_survey, _actor, _correlation, previousStatus: SurveyStatus.PendingReview, remarks: "Looks good");

        evt.EventType.Should().Be("survey.published");
        evt.PreviousStatus.Should().Be(SurveyStatus.PendingReview);
        evt.NewStatus.Should().Be(SurveyStatus.Active);
        evt.Remarks.Should().Be("Looks good");
        evt.OccurredAt.Should().Be(TestTime.Anchor);
    }

    [Fact]
    public void ReturnedToDraft_builds_one_status_changed_event_carrying_reviewer_remarks()
    {
        var evt = CreateSut().ReturnedToDraft(_survey, _actor, _correlation, remarks: "Fix Arabic");

        evt.EventType.Should().Be("survey.status.changed");
        evt.PreviousStatus.Should().Be(SurveyStatus.PendingReview);
        evt.NewStatus.Should().Be(SurveyStatus.Draft);
        evt.Remarks.Should().Be("Fix Arabic");
        evt.ActorId.Should().Be(_actor);
        evt.CorrelationId.Should().Be(_correlation);
    }
}
