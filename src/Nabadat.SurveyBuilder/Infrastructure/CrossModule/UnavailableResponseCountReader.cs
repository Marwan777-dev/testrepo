using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.CrossModule;

/// <summary>
/// Default <see cref="IResponseCountReader"/> for environments with no Elasticsearch configured
/// (dev / E2E): returns an empty projection, so FR-10.4 low-response ordering degrades gracefully to
/// insertion order. Registered via <c>TryAddScoped</c> and replaced by the real
/// <c>ResponseCountReader</c> when <c>Elasticsearch:Uri</c> is configured (see the DI extension).
/// </summary>
public sealed class UnavailableResponseCountReader : IResponseCountReader
{
    public Task<IReadOnlyDictionary<Guid, long>> GetResponseCountsAsync(Guid surveyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, long>>(new Dictionary<Guid, long>());
}
