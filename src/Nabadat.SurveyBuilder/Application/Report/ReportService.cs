using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.Report.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// T242 [US8] — composes the F13 Survey Report (contracts/report-and-analytics.md). Resolves the
/// reporting window (<see cref="PeriodResolver"/>), pulls the in-window aggregate from ES
/// (<see cref="IReportAggregator"/>), averages the CSAT questions into the headline gauge
/// (<see cref="HeadlineCsatCalculator"/>, FR-13.2), computes period-over-period deltas against the
/// equal-length previous window, and picks each question's view by its type
/// (<see cref="PerQuestionViewSelector"/>, FR-13.3). Reads the survey's question structure from
/// PostgreSQL; every metric value comes from ES (AD-04). The clock is injected (rule 8). Returns
/// Application-layer results — the Api layer maps them to the wire DTOs (Article 1A).
/// </summary>
public sealed class ReportService
{
    private const string DefaultPeriod = "last_7_days";
    private const int MaxVerbatimSampleSize = 100;

    private readonly ISurveyStore _surveys;
    private readonly IQuestionStore _questions;
    private readonly IReportAggregator _aggregator;
    private readonly PeriodResolver _periodResolver;
    private readonly HeadlineCsatCalculator _csatCalculator;
    private readonly PerQuestionViewSelector _viewSelector;
    private readonly TimeProvider _timeProvider;

    public ReportService(
        ISurveyStore surveys,
        IQuestionStore questions,
        IReportAggregator aggregator,
        PeriodResolver periodResolver,
        HeadlineCsatCalculator csatCalculator,
        PerQuestionViewSelector viewSelector,
        TimeProvider timeProvider)
    {
        _surveys = surveys;
        _questions = questions;
        _aggregator = aggregator;
        _periodResolver = periodResolver;
        _csatCalculator = csatCalculator;
        _viewSelector = viewSelector;
        _timeProvider = timeProvider;
    }

    /// <summary>Builds the report for a survey over the requested period.</summary>
    public async Task<SurveyReport> GetReportAsync(
        Guid surveyId, string? period, DateTimeOffset? from, DateTimeOffset? to, ReportScope scope, CancellationToken ct = default)
    {
        var survey = await _surveys.GetAsync(surveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");

        var resolved = ResolvePeriod(period, from, to);
        var previous = PreviousWindow(resolved);
        var activePeriod = survey.ActivePeriod?.ToTimeSpan();

        var current = await _aggregator.AggregateAsync(
            new ReportAggregateQuery(surveyId, resolved, activePeriod, scope), ct);
        var prior = await _aggregator.AggregateAsync(
            new ReportAggregateQuery(surveyId, previous, activePeriod, scope), ct);

        var questions = await _questions.GetBySurveyAsync(surveyId, ct);

        return new SurveyReport(
            resolved,
            new ReportMetrics(current.ResponsesCount, current.CompletionRate, current.MedianTimeSeconds, current.Touchpoints),
            BuildHeadline(current, prior),
            questions.Select(q => BuildCard(q, current)).ToList());
    }

    /// <summary>Returns the newest verbatims for a question, capped at <paramref name="limit"/> (FR-13.7).</summary>
    public async Task<IReadOnlyList<VerbatimResponse>> GetVerbatimsAsync(
        Guid surveyId, Guid questionId, int limit, ReportScope scope, CancellationToken ct = default)
    {
        var survey = await _surveys.GetAsync(surveyId, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");

        var boundedLimit = Math.Clamp(limit, 1, MaxVerbatimSampleSize);
        var activePeriod = survey.ActivePeriod?.ToTimeSpan();

        // The verbatims endpoint takes no period (contracts/report-and-analytics.md); sample the
        // survey's full collectable range and let the active-period window filter exclude late arrivals.
        var now = _timeProvider.GetUtcNow();
        var wideWindow = new ResolvedPeriod(now.AddYears(-5), now);

        return await _aggregator.GetVerbatimsAsync(
            new VerbatimQuery(surveyId, questionId, boundedLimit, wideWindow, activePeriod, scope), ct);
    }

    private ResolvedPeriod ResolvePeriod(string? period, DateTimeOffset? from, DateTimeOffset? to)
    {
        var requested = string.IsNullOrWhiteSpace(period) ? DefaultPeriod : period.Trim();

        if (requested == "custom")
        {
            if (from is not { } f || to is not { } t || f > t)
            {
                throw new SurveyBuilderException(
                    "report.period.invalid", 400, "A custom period requires valid 'from' and 'to' timestamps.");
            }

            return new ResolvedPeriod(f, t);
        }

        try
        {
            return _periodResolver.Resolve(requested, _timeProvider.GetUtcNow());
        }
        catch (ArgumentException)
        {
            throw new SurveyBuilderException(
                "report.period.invalid", 400, $"Unknown report period '{requested}'.");
        }
    }

    // The equal-length window immediately before the current one (for period-over-period deltas).
    private static ResolvedPeriod PreviousWindow(ResolvedPeriod current)
    {
        var length = current.To - current.From;
        return new ResolvedPeriod(current.From - length, current.From);
    }

    private ReportHeadline BuildHeadline(ReportAggregate current, ReportAggregate prior)
    {
        var csat = BuildKpi(_csatCalculator.Compute(current.CsatValues), _csatCalculator.Compute(prior.CsatValues));
        var nps = BuildKpi(current.NpsValue, prior.NpsValue);
        var ces = BuildKpi(current.CesValue, prior.CesValue);
        return new ReportHeadline(csat, nps, ces);
    }

    private static ReportHeadlineKpi? BuildKpi(decimal? currentValue, decimal? previousValue)
    {
        if (currentValue is not { } v)
        {
            return null;
        }

        var deltaPp = previousValue is { } p ? v - p : (decimal?)null;
        // Target is null pending an M-06 KPI-target source — IKpiCatalogReader exposes no target (TODO-M01-025).
        return new ReportHeadlineKpi(v, Target: null, deltaPp);
    }

    private ReportQuestionCard BuildCard(Question question, ReportAggregate aggregate)
    {
        var kind = _viewSelector.Pick(question.Type, question.Subtype);
        aggregate.PerQuestion.TryGetValue(question.Id, out var agg);
        return new ReportQuestionCard(question.Id, question.Type, question.Subtype, kind, agg);
    }
}
