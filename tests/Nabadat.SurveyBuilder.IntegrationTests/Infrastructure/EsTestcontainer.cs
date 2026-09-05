using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Testcontainers.Elasticsearch;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// Testcontainers-backed Elasticsearch 8.x fixture for the M-01 Report / Analytics integration
/// tests, which read AD-04 projections owned by M-04/M-05/M-06/M-07 (data-model.md §Scope). Boots a
/// single-node cluster and exposes an <see cref="ElasticsearchClient"/> plus seeding helpers that
/// index documents into the tenant-scoped indices <c>tenant_{tenantId}_responses</c> and
/// <c>tenant_{tenantId}_analytics</c>.
/// <para>The document <b>shapes</b> are owned by the projecting modules and are not yet finalised
/// here, so the seeders take a caller-supplied document object — a test indexes whatever shape the
/// aggregator port it exercises expects. Docker must be running for this fixture to start (US1+
/// per-story checkpoint, never a per-task gate).</para>
/// </summary>
public sealed class EsTestcontainer : IAsyncLifetime
{
    private readonly ElasticsearchContainer _elasticsearch = new ElasticsearchBuilder()
        .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.13.4")
        .Build();

    private ElasticsearchClient? _client;

    /// <summary>The Elasticsearch client bound to the running container (valid after <see cref="InitializeAsync"/>).</summary>
    public ElasticsearchClient Client =>
        _client ?? throw new InvalidOperationException("EsTestcontainer not initialised — call InitializeAsync first.");

    public static string ResponsesIndex(Guid tenantId) => $"tenant_{tenantId:N}_responses";

    public static string AnalyticsIndex(Guid tenantId) => $"tenant_{tenantId:N}_analytics";

    public async ValueTask InitializeAsync()
    {
        await _elasticsearch.StartAsync();

        // The container ships a self-signed cert and a generated password baked into the
        // connection string; accept the dev cert and let the client read credentials from the URI.
        var settings = new ElasticsearchClientSettings(new Uri(_elasticsearch.GetConnectionString()))
            .ServerCertificateValidationCallback((_, _, _, _) => true);
        _client = new ElasticsearchClient(settings);
    }

    public async ValueTask DisposeAsync() => await _elasticsearch.DisposeAsync();

    /// <summary>Indexes a response projection document for a tenant and refreshes so it is immediately searchable.</summary>
    public Task SeedResponseAsync<TDocument>(Guid tenantId, string id, TDocument document) =>
        IndexAsync(ResponsesIndex(tenantId), id, document);

    /// <summary>Indexes an analytics projection document for a tenant and refreshes so it is immediately searchable.</summary>
    public Task SeedAnalyticsAsync<TDocument>(Guid tenantId, string id, TDocument document) =>
        IndexAsync(AnalyticsIndex(tenantId), id, document);

    private async Task IndexAsync<TDocument>(string index, string id, TDocument document)
    {
        var response = await Client.IndexAsync(document, i => i.Index(index).Id(id));
        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException($"Failed to seed document '{id}' into '{index}': {response.DebugInformation}");
        }

        await Client.Indices.RefreshAsync(index);
    }
}
