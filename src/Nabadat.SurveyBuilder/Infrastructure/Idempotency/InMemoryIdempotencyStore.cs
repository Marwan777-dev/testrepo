using Microsoft.Extensions.Caching.Memory;
using Nabadat.SurveyBuilder.Application.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.Idempotency;

/// <summary>
/// <see cref="IMemoryCache"/>-backed <see cref="IIdempotencyStore"/> with a per-entry absolute
/// expiration (the 24-hour idempotency window, APIs-constitution Article 7.1).
/// <para><b>Single-instance only.</b> This backing is per-process and does not survive a restart —
/// adequate for dev and a single host. A multi-instance production deployment needs a distributed
/// backing (e.g. Redis) behind this same port; swapping the registration in the composition root is
/// the only change required (the port abstracts it).</para>
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private const string KeyPrefix = "m01:idempotency:";

    private readonly IMemoryCache _cache;

    public InMemoryIdempotencyStore(IMemoryCache cache) => _cache = cache;

    public Task<IdempotencyRecord?> TryGetAsync(string key, CancellationToken ct)
    {
        _cache.TryGetValue(KeyPrefix + key, out IdempotencyRecord? record);
        return Task.FromResult(record);
    }

    public Task SaveAsync(string key, IdempotencyRecord record, TimeSpan ttl, CancellationToken ct)
    {
        _cache.Set(KeyPrefix + key, record, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
        return Task.CompletedTask;
    }
}
