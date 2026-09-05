using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Routing;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Routing;

/// <summary>
/// T164 [US4] — unit tests for <c>LayoutRoutingCoupler</c> (F9 / FR-9.1). Answer routing is only
/// available under the one-question-per-page layout: switching to any other layout turns routing
/// off, and enabling routing turns shuffle off and locks it.
/// <para>
/// Contract pinned for the implementer (T172):
/// <list type="bullet">
///   <item><c>LayoutRoutingCoupler</c> lives in <c>Application/Routing/</c> and mutates the passed
///   <see cref="Survey"/> in place (pure w.r.t. I/O — no persistence).</item>
///   <item><c>void OnLayoutChanged(Survey survey, LayoutMode next)</c> — clears
///   <see cref="Survey.RoutingOn"/> whenever <paramref name="next"/> is not
///   <see cref="LayoutMode.Question"/>; leaves it untouched for the question layout.</item>
///   <item><c>void OnRoutingEnabled(Survey survey)</c> — sets <see cref="Survey.RoutingOn"/> true,
///   <see cref="Survey.Shuffle"/> false, and <c>Survey.ShuffleLocked</c> true (a new bool the
///   implementer adds to the entity per FR-9.1).</item>
/// </list>
/// </para>
/// </summary>
public sealed class LayoutRoutingCouplerTests
{
    private readonly LayoutRoutingCoupler _coupler = new();

    private static Survey SurveyWith(LayoutMode layout, bool routingOn = false, bool shuffle = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Layout = layout,
            RoutingOn = routingOn,
            Shuffle = shuffle,
        };

    [Fact]
    public void OnLayoutChanged_turns_routing_off_when_the_layout_is_not_one_question_per_page()
    {
        var survey = SurveyWith(LayoutMode.Question, routingOn: true);

        _coupler.OnLayoutChanged(survey, LayoutMode.Single);

        survey.RoutingOn.Should().BeFalse();
    }

    [Fact]
    public void OnLayoutChanged_leaves_routing_on_when_the_layout_stays_one_question_per_page()
    {
        var survey = SurveyWith(LayoutMode.Question, routingOn: true);

        _coupler.OnLayoutChanged(survey, LayoutMode.Question);

        survey.RoutingOn.Should().BeTrue();
    }

    [Fact]
    public void OnRoutingEnabled_disables_and_locks_shuffle()
    {
        var survey = SurveyWith(LayoutMode.Question, shuffle: true);

        _coupler.OnRoutingEnabled(survey);

        survey.RoutingOn.Should().BeTrue();
        survey.Shuffle.Should().BeFalse();
        survey.ShuffleLocked.Should().BeTrue();
    }
}
