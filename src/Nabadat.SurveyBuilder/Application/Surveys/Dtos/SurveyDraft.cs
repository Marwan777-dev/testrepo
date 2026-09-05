using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys.Dtos;

/// <summary>
/// The F3 Survey Settings input shape validated by <c>SurveyValidator</c> (T067) and applied by
/// <c>SurveyCommandService</c> (T074). Mirrors the <c>surveys</c> settings columns (data-model.md
/// §2.1); the lifecycle/audit columns (status, owner, timestamps) are set by the service, not the
/// caller.
/// </summary>
public sealed class SurveyDraft
{
    public string? NameEn { get; init; }

    public string? Description { get; init; }

    /// <summary>Bound journey id; null ⇒ SeasonalRelational, set ⇒ Transactional (BR-3.3).</summary>
    public Guid? BoundJourney { get; init; }

    public string? WelcomeHtml { get; init; }

    public string? ThanksHtml { get; init; }

    public string? RedirectUrl { get; init; }

    public int RedirectAfterS { get; init; }

    public LayoutMode Layout { get; init; } = LayoutMode.Section;

    public int? QuestionsPerPage { get; init; }

    public ActivePeriod? ActivePeriod { get; init; }

    public bool Shuffle { get; init; }

    public string ShuffleMode { get; init; } = "random";

    public bool RoutingOn { get; init; }

    public ThemeMode ThemeMode { get; init; } = ThemeMode.Inherited;
}
