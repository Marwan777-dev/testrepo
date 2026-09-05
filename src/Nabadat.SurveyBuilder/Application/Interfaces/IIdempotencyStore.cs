namespace Nabadat.SurveyBuilder.Application.Interfaces;

/// <summary>
/// Port for the <c>Idempotency-Key</c> replay store (APIs-constitution Article 7.1). Keyed by the
/// header value; the 24-hour window returns the same response body — and the same audit-log entry,
/// via no re-execution — on retry. Implemented in Infrastructure.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Returns the stored snapshot for <paramref name="key"/>, or null if none within TTL.</summary>
    Task<IdempotencyRecord?> TryGetAsync(string key, CancellationToken ct);

    /// <summary>Stores <paramref name="record"/> under <paramref name="key"/> for <paramref name="ttl"/>.</summary>
    Task SaveAsync(string key, IdempotencyRecord record, TimeSpan ttl, CancellationToken ct);
}
