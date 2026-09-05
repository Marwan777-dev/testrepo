using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Routing;

/// <summary>
/// T172 [US4] — enforces the F9 layout↔routing↔shuffle coupling (FR-9.1) by mutating the passed
/// <see cref="Survey"/> in place (no persistence; the caller saves inside its own transaction).
/// Answer routing is available only under the one-question-per-page layout
/// (<see cref="LayoutMode.Question"/>): switching to any other layout turns routing off, and enabling
/// routing turns shuffle off and locks it (<see cref="Survey.ShuffleLocked"/> is derived from
/// <see cref="Survey.RoutingOn"/>).
/// </summary>
public sealed class LayoutRoutingCoupler
{
    /// <summary>Clears <see cref="Survey.RoutingOn"/> whenever the next layout is not one-question-per-page.</summary>
    public void OnLayoutChanged(Survey survey, LayoutMode next)
    {
        if (next != LayoutMode.Question)
        {
            survey.RoutingOn = false;
        }
    }

    /// <summary>Turns routing on and disables shuffle (which becomes locked via <see cref="Survey.ShuffleLocked"/>).</summary>
    public void OnRoutingEnabled(Survey survey)
    {
        survey.RoutingOn = true;
        survey.Shuffle = false;
    }

    /// <summary>Turns routing off; shuffle becomes unlocked again (<see cref="Survey.ShuffleLocked"/> follows).</summary>
    public void OnRoutingDisabled(Survey survey)
    {
        survey.RoutingOn = false;
    }
}
