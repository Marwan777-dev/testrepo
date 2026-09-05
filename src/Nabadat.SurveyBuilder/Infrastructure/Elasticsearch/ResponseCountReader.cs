using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;

/// <summary>
/// Reads M-04's per-question response-count projection from Elasticsearch (T145, research.md §7):
/// the <c>tenant_{tenantId}_analytics</c> index holds one <c>question_response_counts</c> document
/// per question. The counts drive FR-10.4 low-response ordering; M-01 only reads them (off the
/// dispatch hot path — computed at render time, no cache per AD-04).
/// <para>Read-only and resilient: a missing index, an unreachable cluster, or a query error resolves
/// to an <b>empty</b> map, so low-response ordering degrades to insertion order rather than failing
/// a dispatch. The index is scanned tenant-wide; the caller looks up only its own question ids, so
/// no per-survey filter is required (question ids are globally unique within a tenant).</para>
/// </summary>
public sealed class ResponseCountReader : IResponseCountReader
{
    private const int MaxDocuments = 10_000;

    private readonly ElasticsearchClient _client;
    private readonly ICurrentTenant _tenant;

    public ResponseCountReader(ElasticsearchClient client, ICurrentTenant tenant)
    {
        _client = client;
        _tenant = tenant;
    }

    public async Task<IReadOnlyDictionary<Guid, long>> GetResponseCountsAsync(Guid surveyId, CancellationToken ct = default)
    {
        var index = $"tenant_{_tenant.TenantId:N}_analytics";

        try
        {
            var response = await _client.SearchAsync<QuestionResponseCountDocument>(
                s => s.Indices(index).Size(MaxDocuments), ct);

            if (!response.IsValidResponse)
            {
                return Empty;
            }

            var counts = new Dictionary<Guid, long>();
            foreach (var doc in response.Documents)
            {
                if (doc is not null && Guid.TryParse(doc.QuestionId, out var questionId))
                {
                    counts[questionId] = doc.Count;
                }
            }

            return counts;
        }
        catch
        {
            // ES unavailable / index absent / auth failure — degrade to empty (ordering falls back).
            return Empty;
        }
    }

    private static readonly IReadOnlyDictionary<Guid, long> Empty = new Dictionary<Guid, long>();

    /// <summary>Shape of a <c>question_response_counts</c> document in the tenant analytics index.</summary>
    private sealed class QuestionResponseCountDocument
    {
        [JsonPropertyName("question_id")]
        public string QuestionId { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public long Count { get; set; }
    }
}
