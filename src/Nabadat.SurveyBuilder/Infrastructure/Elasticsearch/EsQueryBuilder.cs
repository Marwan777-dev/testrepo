using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Nabadat.SurveyBuilder.Application.Report;

namespace Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;

/// <summary>
/// T240 [US8] — shared query-clause helpers for the Report ES reads (research.md §3). Every report
/// query is a <c>bool</c> filter whose first clause is the survey match, followed by the period
/// <c>range</c> and the caller's data-scope <c>terms</c> clauses — all applied server-side before
/// dispatch (APIs-constitution Article 4.5). A verbatim query adds a per-question filter.
/// </summary>
public sealed class EsQueryBuilder
{
    /// <summary>
    /// Builds the <c>bool</c> filter for a survey report: <c>survey_id</c> term + <c>submitted_at</c>
    /// range over <paramref name="period"/> + one <c>terms</c> clause per data-scope parameter. When
    /// <paramref name="questionId"/> is supplied, a nested <c>answers.question_id</c> filter narrows
    /// to that question's verbatims.
    /// </summary>
    public Query BuildResponseQuery(Guid surveyId, ResolvedPeriod period, ReportScope scope, Guid? questionId = null)
    {
        var filters = new List<Query>
        {
            new TermQuery("survey_id", surveyId.ToString()),
            new DateRangeQuery("submitted_at")
            {
                Gte = period.From.UtcDateTime,
                Lte = period.To.UtcDateTime,
            },
        };

        // Data-scope narrowing (Article 4.5): each parameter (branch, region, …) becomes a terms clause.
        foreach (var (parameter, values) in scope.Assignments)
        {
            if (values.Count == 0)
            {
                continue;
            }

            filters.Add(new TermsQuery
            {
                Field = parameter,
                Terms = new TermsQueryField(values.Select(v => FieldValue.String(v)).ToArray()),
            });
        }

        if (questionId is { } qid)
        {
            filters.Add(new TermQuery("answers.question_id", qid.ToString()));
        }

        return new BoolQuery { Filter = filters };
    }
}
