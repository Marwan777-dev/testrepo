using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.HtmlSanitisation;
using Nabadat.SurveyBuilder.Application.HtmlSanitisation.Interfaces;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Survey create / update / clone / get / search (T074). Validates the draft (<see cref="SurveyValidator"/>),
/// keeps the journey↔type invariant (<see cref="SurveyTypeSyncService"/>), validates the bound
/// journey via the M-16 <see cref="IJourneyReader"/>, and sanitises <c>welcome_html</c>/<c>thanks_html</c>
/// via <see cref="IHtmlSanitiser"/> (persisting <c>sanitiser_policy_version = 1</c>) on every save.
/// </summary>
public sealed class SurveyCommandService
{
    private readonly ISurveyStore _surveys;
    private readonly SurveyValidator _validator;
    private readonly SurveyTypeSyncService _typeSync;
    private readonly IJourneyReader _journeys;
    private readonly IHtmlSanitiser _sanitiser;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SurveyCommandService(
        ISurveyStore surveys,
        SurveyValidator validator,
        SurveyTypeSyncService typeSync,
        IJourneyReader journeys,
        IHtmlSanitiser sanitiser,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _surveys = surveys;
        _validator = validator;
        _typeSync = typeSync;
        _journeys = journeys;
        _sanitiser = sanitiser;
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task<Survey?> GetAsync(Guid id, CancellationToken ct = default) => _surveys.GetAsync(id, ct);

    public Task<SurveySearchResult> SearchAsync(SurveySearchQuery query, CancellationToken ct = default) =>
        _surveys.SearchAsync(query, ct);

    public async Task<Survey> CreateAsync(SurveyDraft draft, Guid actorId, Guid? clientId, CancellationToken ct = default)
    {
        await ValidateAsync(draft, ct);

        var now = _timeProvider.GetUtcNow();
        var survey = Survey.Create(clientId ?? Guid.NewGuid(), draft.NameEn!, actorId, draft.BoundJourney, actorId, now);
        ApplySettings(survey, draft);

        await _context.ExecuteAsync(async () => await _surveys.AddAsync(survey, ct), ct);
        return survey;
    }

    public async Task<Survey> UpdateAsync(Guid id, SurveyDraft draft, Guid actorId, CancellationToken ct = default)
    {
        var survey = await _surveys.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");

        await ValidateAsync(draft, ct);

        var now = _timeProvider.GetUtcNow();
        survey.NameEn = draft.NameEn!;
        survey.Description = draft.Description;
        _typeSync.OnBoundJourneyChanged(survey, draft.BoundJourney);
        ApplySettings(survey, draft);
        survey.UpdatedBy = actorId;
        survey.UpdatedAt = now;
        survey.IncrementRowVersion();

        await _context.ExecuteAsync(async () => await _surveys.UpdateAsync(survey, ct), ct);
        return survey;
    }

    public async Task<Survey> CloneAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var source = await _surveys.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");

        var now = _timeProvider.GetUtcNow();
        var clone = Survey.Create(Guid.NewGuid(), $"Copy of — {source.NameEn}", actorId, source.BoundJourneyId, actorId, now);
        clone.Description = source.Description;
        clone.WelcomeHtml = source.WelcomeHtml;
        clone.ThanksHtml = source.ThanksHtml;
        clone.SanitiserPolicyVersion = source.SanitiserPolicyVersion;
        clone.RedirectUrl = source.RedirectUrl;
        clone.RedirectAfterS = source.RedirectAfterS;
        clone.Layout = source.Layout;
        clone.QuestionsPerPage = source.QuestionsPerPage;
        clone.ActivePeriod = source.ActivePeriod;
        clone.Shuffle = source.Shuffle;
        clone.ShuffleMode = source.ShuffleMode;
        clone.RoutingOn = source.RoutingOn;
        clone.ThemeMode = source.ThemeMode;

        await _context.ExecuteAsync(async () => await _surveys.AddAsync(clone, ct), ct);
        return clone;
    }

    private async Task ValidateAsync(SurveyDraft draft, CancellationToken ct)
    {
        var result = _validator.Validate(draft);
        if (!result.IsValid)
        {
            throw new SurveyBuilderException(result.Errors[0], 400, "Survey settings are invalid.");
        }

        if (draft.BoundJourney is { } journeyId && !await _journeys.JourneyExistsAsync(journeyId, ct))
        {
            throw new SurveyBuilderException("survey.bound_journey.not_found", 400, "The bound journey does not exist.");
        }
    }

    private void ApplySettings(Survey survey, SurveyDraft draft)
    {
        var welcome = _sanitiser.Sanitise(draft.WelcomeHtml ?? string.Empty, SanitiserPolicyVersion.V1);
        var thanks = _sanitiser.Sanitise(draft.ThanksHtml ?? string.Empty, SanitiserPolicyVersion.V1);

        survey.WelcomeHtml = welcome.Html;
        survey.ThanksHtml = thanks.Html;
        survey.SanitiserPolicyVersion = SanitiserPolicyVersion.V1.PolicyVersion;
        survey.RedirectUrl = draft.RedirectUrl;
        survey.RedirectAfterS = draft.RedirectAfterS;
        survey.Layout = draft.Layout;
        survey.QuestionsPerPage = draft.QuestionsPerPage;
        survey.ActivePeriod = draft.ActivePeriod;
        survey.Shuffle = draft.RoutingOn ? false : draft.Shuffle; // routing_on locks shuffle off (F9)
        survey.ShuffleMode = draft.ShuffleMode;
        survey.RoutingOn = draft.RoutingOn;
        survey.ThemeMode = draft.ThemeMode;
    }
}
