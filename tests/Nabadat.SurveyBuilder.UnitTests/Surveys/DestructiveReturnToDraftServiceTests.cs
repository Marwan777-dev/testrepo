using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Nabadat.SurveyBuilder.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T049 [US1] — unit tests for <c>DestructiveReturnToDraftService</c> (BR-1.6): returning an Active or
/// Paused survey to Draft hard-purges its responses via the M-04 <c>IResponsePurgeService</c> after
/// the M-01 transaction commits, compensates (reverts status) if the purge fails, does NOT purge for
/// the non-destructive Pending-review → Draft path (BR-1.5/FR-15.4), and audits with the
/// <c>purged_response_count</c>.
/// <para>
/// Contract pinned for the implementer (T072):
/// <list type="bullet">
///   <item><c>DestructiveReturnToDraftService</c> lives in <c>Application/Surveys/</c>.</item>
///   <item>ctor <c>(ISurveyStore surveys, IResponsePurgeService purge, IEventLogWriter events,
///   ITenantDbContext context, TimeProvider timeProvider)</c>.</item>
///   <item><c>Task&lt;ReturnToDraftResult&gt; ReturnToDraftAsync(ReturnToDraftCommand command,
///   CancellationToken ct = default)</c> where
///   <c>ReturnToDraftCommand(Guid SurveyId, Guid ActorId, Guid CorrelationId)</c>.</item>
///   <item>The M-01 write (status → Draft, response count → 0) runs inside one
///   <c>ITenantDbContext.ExecuteAsync</c>; the purge is invoked <b>after</b> that commit; on purge
///   failure the service reverts <c>survey.Status</c> to its prior value and rethrows.</item>
///   <item><c>IResponsePurgeService</c> (consumer-defined M-01 port implemented by M-04, in
///   <c>Domain/Interfaces/</c>):
///   <c>Task&lt;int&gt; PurgeSurveyResponsesAsync(Guid surveyId, Guid actorId, Guid correlationId,
///   CancellationToken ct = default)</c> returning the number of responses deleted.</item>
///   <item>The emitted <c>SurveyAuditEvent</c> for the destructive path carries the returned count in
///   its <c>Payload["purged_response_count"]</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class DestructiveReturnToDraftServiceTests
{
    private readonly ISurveyStore _surveys = Substitute.For<ISurveyStore>();
    private readonly IResponsePurgeService _purge = Substitute.For<IResponsePurgeService>();
    private readonly IEventLogWriter _events = Substitute.For<IEventLogWriter>();
    private readonly RecordingTenantDbContext _context = new();

    private DestructiveReturnToDraftService CreateService() =>
        new(_surveys, _purge, _events, _context, TestTime.Provider());

    [Fact]
    public async Task ReturnToDraftAsync_purges_responses_and_transitions_an_active_survey_to_draft()
    {
        var survey = new Survey { Id = Guid.NewGuid(), Status = SurveyStatus.Active };
        _surveys.GetAsync(survey.Id, Arg.Any<CancellationToken>()).Returns(survey);
        _purge.PurgeSurveyResponsesAsync(survey.Id, Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(42);

        var service = CreateService();
        await service.ReturnToDraftAsync(new ReturnToDraftCommand(survey.Id, Guid.NewGuid(), Guid.NewGuid()));

        survey.Status.Should().Be(SurveyStatus.Draft);
        await _purge.Received(1).PurgeSurveyResponsesAsync(
            survey.Id, Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _context.ExecuteAsyncCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReturnToDraftAsync_audits_the_purged_response_count()
    {
        var survey = new Survey { Id = Guid.NewGuid(), Status = SurveyStatus.Active };
        _surveys.GetAsync(survey.Id, Arg.Any<CancellationToken>()).Returns(survey);
        _purge.PurgeSurveyResponsesAsync(survey.Id, Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(42);

        var service = CreateService();
        await service.ReturnToDraftAsync(new ReturnToDraftCommand(survey.Id, Guid.NewGuid(), Guid.NewGuid()));

        await _events.Received(1).WriteAsync(
            Arg.Is<SurveyAuditEvent>(e => Equals(e.Payload["purged_response_count"], 42)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnToDraftAsync_reverts_status_and_preserves_activation_when_the_purge_fails()
    {
        // Activation is an earlier instant than the (fixed) rollback clock, so a re-stamp would show.
        var activatedAt = TestTime.Anchor.AddDays(-5);
        var survey = new Survey { Id = Guid.NewGuid(), Status = SurveyStatus.Active, ActivatedAt = activatedAt };
        _surveys.GetAsync(survey.Id, Arg.Any<CancellationToken>()).Returns(survey);
        _purge.PurgeSurveyResponsesAsync(survey.Id, Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("purge unavailable"));

        var service = CreateService();
        var act = () => service.ReturnToDraftAsync(new ReturnToDraftCommand(survey.Id, Guid.NewGuid(), Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>();
        survey.Status.Should().Be(SurveyStatus.Active); // compensated back to the prior status
        survey.ActivatedAt.Should().Be(activatedAt);    // a rollback is not a fresh start (FR-3.4)
    }

    [Fact]
    public async Task ReturnToDraftAsync_does_not_purge_for_the_non_destructive_pending_review_path()
    {
        var survey = new Survey { Id = Guid.NewGuid(), Status = SurveyStatus.PendingReview };
        _surveys.GetAsync(survey.Id, Arg.Any<CancellationToken>()).Returns(survey);

        var service = CreateService();
        await service.ReturnToDraftAsync(new ReturnToDraftCommand(survey.Id, Guid.NewGuid(), Guid.NewGuid()));

        survey.Status.Should().Be(SurveyStatus.Draft);
        await _purge.DidNotReceive().PurgeSurveyResponsesAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
