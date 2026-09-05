using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;

/// <summary>
/// T241 [US8] — builds the singleton <see cref="ElasticsearchClient"/> the Report/Analytics read
/// adapters share (research.md §3). Elasticsearch is reached over HTTPS on port 9200 only
/// (constitution AD-04 / APIs-constitution Article 8.3); the connection URI comes from
/// <c>Elasticsearch:Uri</c>, and optional Basic-auth credentials from
/// <c>Elasticsearch:Username</c> / <c>Elasticsearch:Password</c>. Registered once in
/// <c>SurveyBuilderServiceCollectionExtensions</c>.
/// </summary>
public static class EsClientFactory
{
    /// <summary>
    /// Creates the shared client for the given cluster <paramref name="uri"/>.
    /// When <paramref name="username"/> is supplied, HTTP Basic authentication is configured.
    /// When <paramref name="trustSelfSignedCertificate"/> is true (dev only), the server
    /// certificate is accepted without CA validation so a self-signed dev cluster (the default
    /// security-enabled Elasticsearch install) can be reached over HTTPS.
    /// </summary>
    public static ElasticsearchClient Create(
        string uri,
        string? username = null,
        string? password = null,
        bool trustSelfSignedCertificate = false)
    {
        var settings = new ElasticsearchClientSettings(new Uri(uri));

        if (!string.IsNullOrWhiteSpace(username))
            settings = settings.Authentication(new BasicAuthentication(username, password ?? string.Empty));

        if (trustSelfSignedCertificate)
            settings = settings.ServerCertificateValidationCallback((_, _, _, _) => true);

        return new ElasticsearchClient(settings);
    }
}
