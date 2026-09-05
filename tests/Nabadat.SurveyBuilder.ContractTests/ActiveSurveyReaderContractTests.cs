using FluentAssertions;
using Nabadat.SurveyBuilder.Application.RenderPlan;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nabadat.SurveyBuilder.ContractTests;

/// <summary>
/// T161 [US3] — contract tests for the M-01 published <see cref="IActiveSurveyReader"/> (AD-01), the
/// reader M-04 calls at response-submission time to enforce the active-period lifecycle (BR-3.4).
/// Pure-logic verification of the <see cref="ActiveSurveyState"/> shape M-04 binds to (the store is
/// substituted): the survey's live <see cref="SurveyStatus"/> is surfaced, a missing survey reports a
/// terminal (non-collectable) status, and the absolute <c>ExpiresAt</c> is derived from
/// <c>ActivatedAt + ActivePeriod</c> — <c>null</c> when there is no active period or no activation
/// instant ("never auto-expires", FR-3.4).
/// </summary>
public sealed class ActiveSurveyReaderContractTests
{
    private readonly ISurveyStore _surveys = Substitute.For<ISurveyStore>();

    private IActiveSurveyReader CreateReader() => new ActiveSurveyReader(_surveys);

    [Fact]
    public async Task GetStateAsync_surfaces_the_active_status_when_the_survey_is_active()
    {
        var surveyId = Guid.NewGuid();
        _surveys.GetAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new Survey { Id = surveyId, Status = SurveyStatus.Active });

        var state = await CreateReader().GetStateAsync(
            new SurveyId(surveyId), DateTimeOffset.UnixEpoch, CancellationToken.None);

        state.Status.Should().Be(SurveyStatus.Active);
    }

    [Fact]
    public async Task GetStateAsync_derives_expiry_from_activation_and_active_period_when_both_are_set()
    {
        var surveyId = Guid.NewGuid();
        var activatedAt = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);
        _surveys.GetAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new Survey
            {
                Id = surveyId,
                Status = SurveyStatus.Active,
                ActivatedAt = activatedAt,
                ActivePeriod = new ActivePeriod(Days: 3, Hours: 12),
            });

        var state = await CreateReader().GetStateAsync(
            new SurveyId(surveyId), DateTimeOffset.UnixEpoch, CancellationToken.None);

        state.ActivatedAt.Should().Be(activatedAt);
        state.ExpiresAt.Should().Be(activatedAt.AddDays(3).AddHours(12));
    }

    [Fact]
    public async Task GetStateAsync_reports_no_expiry_when_the_active_period_is_null()
    {
        var surveyId = Guid.NewGuid();
        var activatedAt = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);
        _surveys.GetAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new Survey
            {
                Id = surveyId,
                Status = SurveyStatus.Active,
                ActivatedAt = activatedAt,
                ActivePeriod = null,
            });

        var state = await CreateReader().GetStateAsync(
            new SurveyId(surveyId), DateTimeOffset.UnixEpoch, CancellationToken.None);

        // An empty active period means the survey never auto-expires (FR-3.4).
        state.ActivatedAt.Should().Be(activatedAt);
        state.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task GetStateAsync_reports_no_expiry_when_the_survey_is_not_yet_activated()
    {
        var surveyId = Guid.NewGuid();
        _surveys.GetAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new Survey
            {
                Id = surveyId,
                Status = SurveyStatus.Draft,
                ActivatedAt = null,
                ActivePeriod = new ActivePeriod(Days: 3, Hours: 0),
            });

        var state = await CreateReader().GetStateAsync(
            new SurveyId(surveyId), DateTimeOffset.UnixEpoch, CancellationToken.None);

        // No start instant to measure from ⇒ no absolute expiry can be computed.
        state.ActivatedAt.Should().BeNull();
        state.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task GetStateAsync_surfaces_the_live_status_for_a_non_active_survey()
    {
        var surveyId = Guid.NewGuid();
        _surveys.GetAsync(surveyId, Arg.Any<CancellationToken>())
            .Returns(new Survey { Id = surveyId, Status = SurveyStatus.Paused });

        var state = await CreateReader().GetStateAsync(
            new SurveyId(surveyId), DateTimeOffset.UnixEpoch, CancellationToken.None);

        state.Status.Should().Be(SurveyStatus.Paused);
    }

    [Fact]
    public async Task GetStateAsync_reports_a_terminal_status_when_the_survey_is_missing()
    {
        var surveyId = Guid.NewGuid();
        _surveys.GetAsync(surveyId, Arg.Any<CancellationToken>()).Returns((Survey?)null);

        var state = await CreateReader().GetStateAsync(
            new SurveyId(surveyId), DateTimeOffset.UnixEpoch, CancellationToken.None);

        // A missing survey is not collectable — M-04 must reject; Archived is the terminal signal.
        state.Status.Should().Be(SurveyStatus.Archived);
    }
}
