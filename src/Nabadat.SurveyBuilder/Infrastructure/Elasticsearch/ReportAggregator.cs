using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Report;
using Nabadat.SurveyBuilder.Application.Report.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;

/// <summary>
/// T239 [US8] — reads the Survey Report aggregate from Elasticsearch (AD-04): the
/// <c>tenant_{tenantId}_responses</c> index holds one document per response with its nested answers,
/// scope fields, and completion metadata. All filtering (period range, data scope, per-question) is
/// applied by <see cref="EsQueryBuilder"/> server-side before dispatch (APIs-constitution Article
/// 4.5); the window filter (FR-13.6) drops any response submitted after the survey's active period
/// elapsed. Aggregation over the period-filtered, bounded result set is done in-process (correct at
/// fixture/tenant scale; TODO-M01-023 tracks moving to native ES aggregations for large surveys).
/// <para>Read-only and resilient: a missing index, an unreachable cluster, or a query error resolves
/// to <see cref="ReportAggregate.Empty"/> / an empty verbatim list, so the report degrades to an
/// empty state rather than 500-ing.</para>
/// </summary>
public sealed class ReportAggregator : IReportAggregator
{
    private const int MaxDocuments = 10_000;
    private const int DefaultVerbatimSampleSize = 5;

    private readonly ElasticsearchClient _client;
    private readonly ICurrentTenant _tenant;
    private readonly EsQueryBuilder _queryBuilder;
    private readonly ResponseWindowFilter _windowFilter;
    private readonly VerbatimSampler _verbatimSampler;

    public ReportAggregator(
        ElasticsearchClient client,
        ICurrentTenant tenant,
        EsQueryBuilder queryBuilder,
        ResponseWindowFilter windowFilter,
        VerbatimSampler verbatimSampler)
    {
        _client = client;
        _tenant = tenant;
        _queryBuilder = queryBuilder;
        _windowFilter = windowFilter;
        _verbatimSampler = verbatimSampler;
    }

    public async Task<ReportAggregate> AggregateAsync(ReportAggregateQuery query, CancellationToken ct = default)
    {
        var docs = await FetchAsync(query.SurveyId, query.Period, query.Scope, questionId: null, ct);
        if (docs is null)
        {
            return ReportAggregate.Empty;
        }

        var windowed = ApplyWindow(docs, query.ActivePeriod);
        if (windowed.Count == 0)
        {
            return ReportAggregate.Empty;
        }

        var responsesCount = windowed.Count;
        var completed = windowed.Count(d => d.Completed);
        var completionRate = responsesCount == 0 ? 0m : Math.Round((decimal)completed / responsesCount, 4);

        var durations = windowed
            .Where(d => d.CompletionTimeSeconds is not null)
            .Select(d => d.CompletionTimeSeconds!.Value)
            .ToList();
        int? medianTime = Median(durations);

        var touchpoints = windowed
            .Where(d => !string.IsNullOrWhiteSpace(d.TouchpointId))
            .Select(d => d.TouchpointId!)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var perQuestion = BuildPerQuestion(windowed);
        var (csatValues, npsValue, cesValue) = HeadlineValues(windowed);

        return new ReportAggregate(
            responsesCount,
            completionRate,
            medianTime,
            touchpoints,
            csatValues,
            npsValue,
            cesValue,
            perQuestion);
    }

    public async Task<IReadOnlyList<VerbatimResponse>> GetVerbatimsAsync(VerbatimQuery query, CancellationToken ct = default)
    {
        var docs = await FetchAsync(query.SurveyId, query.Period, query.Scope, query.QuestionId, ct);
        if (docs is null)
        {
            return Array.Empty<VerbatimResponse>();
        }

        var windowed = ApplyWindow(docs, query.ActivePeriod);
        var verbatims = ExtractVerbatims(windowed, query.QuestionId);
        return _verbatimSampler.Sample(verbatims, query.Limit);
    }

    private async Task<IReadOnlyCollection<ResponseDocument>?> FetchAsync(
        Guid surveyId, ResolvedPeriod period, ReportScope scope, Guid? questionId, CancellationToken ct)
    {
        var index = $"tenant_{_tenant.TenantId:N}_responses";
        var esQuery = _queryBuilder.BuildResponseQuery(surveyId, period, scope, questionId);

        try
        {
            var response = await _client.SearchAsync<ResponseDocument>(
                s => s.Indices(index).Query(esQuery).Size(MaxDocuments), ct);

            return response.IsValidResponse ? response.Documents : null;
        }
        catch
        {
            // ES unavailable / index absent / auth failure — degrade to an empty report.
            return null;
        }
    }

    private List<ResponseDocument> ApplyWindow(IReadOnlyCollection<ResponseDocument> docs, TimeSpan? activePeriod)
    {
        if (activePeriod is not { } period)
        {
            // A null active period means the survey never auto-expires — no window filtering (FR-3.4).
            return docs.Where(d => d is not null).ToList();
        }

        return docs
            .Where(d => d is not null && _windowFilter.Include(d.SubmittedAt, d.SentAt, period))
            .ToList();
    }

    private IReadOnlyDictionary<Guid, PerQuestionAggregate> BuildPerQuestion(List<ResponseDocument> windowed)
    {
        // question id → responses that answered it (with the answer payload).
        var byQuestion = new Dictionary<Guid, List<(ResponseDocument Doc, AnswerDocument Answer)>>();
        foreach (var doc in windowed)
        {
            foreach (var answer in doc.Answers)
            {
                if (Guid.TryParse(answer.QuestionId, out var questionId))
                {
                    if (!byQuestion.TryGetValue(questionId, out var list))
                    {
                        list = [];
                        byQuestion[questionId] = list;
                    }

                    list.Add((doc, answer));
                }
            }
        }

        var result = new Dictionary<Guid, PerQuestionAggregate>();
        foreach (var (questionId, entries) in byQuestion)
        {
            var respondentsBase = entries.Select(e => e.Doc.ResponseId).Distinct(StringComparer.Ordinal).Count();
            var distribution = BuildDistribution(entries, respondentsBase);

            var numerics = entries
                .Where(e => e.Answer.NumericValue is not null)
                .Select(e => e.Answer.NumericValue!.Value)
                .ToList();
            decimal? gaugeValue = numerics.Count > 0 ? Math.Round(numerics.Average(), 2) : null;
            decimal? average = gaugeValue;

            var gaugeTarget = entries
                .Select(e => e.Answer.GaugeTarget)
                .FirstOrDefault(t => t is not null);

            var verbatimSample = _verbatimSampler.Sample(
                ExtractVerbatims(entries), DefaultVerbatimSampleSize);

            result[questionId] = new PerQuestionAggregate(
                questionId,
                ResponsesCount: respondentsBase,
                RespondentsBase: respondentsBase,
                Distribution: distribution,
                GaugeValue: gaugeValue,
                GaugeTarget: gaugeTarget,
                Average: average,
                VerbatimSample: verbatimSample);
        }

        return result;
    }

    private static IReadOnlyList<DistributionBucket> BuildDistribution(
        List<(ResponseDocument Doc, AnswerDocument Answer)> entries, int respondentsBase)
    {
        // Each selected option contributes one to its bucket; multi-select answers carry several labels.
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, answer) in entries)
        {
            var labels = answer.OptionLabels is { Count: > 0 }
                ? answer.OptionLabels
                : answer.OptionLabel is not null ? [answer.OptionLabel] : (IReadOnlyList<string>)[];

            foreach (var label in labels)
            {
                counts[label] = counts.GetValueOrDefault(label) + 1;
            }
        }

        return counts
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => new DistributionBucket(
                kvp.Key,
                kvp.Value,
                respondentsBase > 0 ? Math.Round((decimal)kvp.Value / respondentsBase * 100m, 1) : null))
            .ToList();
    }

    private static (IReadOnlyList<decimal> Csat, decimal? Nps, decimal? Ces) HeadlineValues(List<ResponseDocument> windowed)
    {
        // Per-question CSAT averages contribute to the headline CSAT (FR-13.2); NPS/CES are single
        // headline values averaged across their answers. The kpi family is denormalised on each answer.
        var csatByQuestion = new Dictionary<string, List<decimal>>(StringComparer.Ordinal);
        var nps = new List<decimal>();
        var ces = new List<decimal>();

        foreach (var doc in windowed)
        {
            foreach (var answer in doc.Answers)
            {
                if (answer.NumericValue is not { } value || string.IsNullOrEmpty(answer.KpiFamily))
                {
                    continue;
                }

                switch (answer.KpiFamily.ToLowerInvariant())
                {
                    case "csat":
                        if (!csatByQuestion.TryGetValue(answer.QuestionId, out var list))
                        {
                            list = [];
                            csatByQuestion[answer.QuestionId] = list;
                        }

                        list.Add(value);
                        break;
                    case "nps":
                        nps.Add(value);
                        break;
                    case "ces":
                        ces.Add(value);
                        break;
                }
            }
        }

        var csatValues = csatByQuestion.Values
            .Select(v => Math.Round(v.Average(), 2))
            .ToList();

        return (
            csatValues,
            nps.Count > 0 ? Math.Round(nps.Average(), 2) : null,
            ces.Count > 0 ? Math.Round(ces.Average(), 2) : null);
    }

    private static IReadOnlyList<VerbatimResponse> ExtractVerbatims(
        List<(ResponseDocument Doc, AnswerDocument Answer)> entries) =>
        entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Answer.Text))
            .Select(e => new VerbatimResponse(
                ParseGuidOrEmpty(e.Doc.ResponseId), e.Doc.Channel, e.Doc.SubmittedAt, e.Answer.Text!))
            .ToList();

    private static IReadOnlyList<VerbatimResponse> ExtractVerbatims(List<ResponseDocument> windowed, Guid questionId)
    {
        var target = questionId.ToString();
        var verbatims = new List<VerbatimResponse>();
        foreach (var doc in windowed)
        {
            foreach (var answer in doc.Answers)
            {
                if (string.Equals(answer.QuestionId, target, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(answer.Text))
                {
                    verbatims.Add(new VerbatimResponse(
                        ParseGuidOrEmpty(doc.ResponseId), doc.Channel, doc.SubmittedAt, answer.Text!));
                }
            }
        }

        return verbatims;
    }

    private static Guid ParseGuidOrEmpty(string value) => Guid.TryParse(value, out var id) ? id : Guid.Empty;

    private static int? Median(List<int> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 1
            ? values[mid]
            : (int)Math.Round((values[mid - 1] + values[mid]) / 2.0);
    }

    /// <summary>Shape of a response document in the <c>tenant_{id}_responses</c> index.</summary>
    private sealed class ResponseDocument
    {
        [JsonPropertyName("response_id")]
        public string ResponseId { get; set; } = string.Empty;

        [JsonPropertyName("survey_id")]
        public string SurveyId { get; set; } = string.Empty;

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = string.Empty;

        [JsonPropertyName("submitted_at")]
        public DateTimeOffset SubmittedAt { get; set; }

        [JsonPropertyName("sent_at")]
        public DateTimeOffset SentAt { get; set; }

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("completion_time_seconds")]
        public int? CompletionTimeSeconds { get; set; }

        [JsonPropertyName("touchpoint_id")]
        public string? TouchpointId { get; set; }

        [JsonPropertyName("answers")]
        public List<AnswerDocument> Answers { get; set; } = [];
    }

    /// <summary>Shape of a nested answer within a response document.</summary>
    private sealed class AnswerDocument
    {
        [JsonPropertyName("question_id")]
        public string QuestionId { get; set; } = string.Empty;

        [JsonPropertyName("kpi_family")]
        public string? KpiFamily { get; set; }

        [JsonPropertyName("numeric_value")]
        public decimal? NumericValue { get; set; }

        [JsonPropertyName("gauge_target")]
        public decimal? GaugeTarget { get; set; }

        [JsonPropertyName("option_label")]
        public string? OptionLabel { get; set; }

        [JsonPropertyName("option_labels")]
        public List<string>? OptionLabels { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
