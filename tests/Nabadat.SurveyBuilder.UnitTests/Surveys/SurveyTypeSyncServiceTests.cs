using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Surveys;

/// <summary>
/// T040 [US1] — unit tests for <c>SurveyTypeSyncService</c> (the BR-3.3 journey↔type invariant that
/// keeps <c>surveys.survey_type</c> in sync with <c>surveys.bound_journey_id</c>).
/// <para>
/// Contract pinned for the implementer (T068):
/// <list type="bullet">
///   <item><c>SurveyTypeSyncService</c> lives in <c>Application/Surveys/</c> and is pure.</item>
///   <item><c>void OnBoundJourneyChanged(Survey survey, Guid? journeyId)</c> sets
///   <c>survey.BoundJourneyId = journeyId</c> and <c>survey.SurveyType</c> to
///   <see cref="SurveyType.Transactional"/> when <paramref name="journeyId"/> is non-null, else
///   <see cref="SurveyType.SeasonalRelational"/> (BR-3.3).</item>
///   <item><see cref="Survey"/> gains (T053) a settable <c>SurveyType SurveyType</c> and a
///   nullable <c>Guid? BoundJourneyId</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class SurveyTypeSyncServiceTests
{
    private readonly SurveyTypeSyncService _service = new();

    [Fact]
    public void OnBoundJourneyChanged_sets_transactional_type_when_a_journey_is_bound()
    {
        var survey = new Survey { Id = Guid.NewGuid() };
        var journeyId = Guid.NewGuid();

        _service.OnBoundJourneyChanged(survey, journeyId);

        survey.SurveyType.Should().Be(SurveyType.Transactional);
        survey.BoundJourneyId.Should().Be(journeyId);
    }

    [Fact]
    public void OnBoundJourneyChanged_sets_seasonal_type_when_the_journey_is_cleared()
    {
        var survey = new Survey { Id = Guid.NewGuid() };

        _service.OnBoundJourneyChanged(survey, null);

        survey.SurveyType.Should().Be(SurveyType.SeasonalRelational);
        survey.BoundJourneyId.Should().BeNull();
    }
}
