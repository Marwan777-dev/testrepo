using System.Collections.Concurrent;
using System.Security.Cryptography;
using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Application.Auth.Interfaces;

namespace Nabadat.UserManagement.Infrastructure.Auth;

/// <summary>
/// In-memory <see cref="IMfaChallengeService"/> with a 5-minute TTL (auth-api.md).
/// Challenges and enrollments are transient auth-flow artifacts (seconds-to-minutes
/// lived), not a data cache — so this does not breach AD-03. Single-host only; a
/// multi-host deployment swaps this for a durable implementation behind the same port.
/// </summary>
public sealed class InMemoryMfaChallengeService : IMfaChallengeService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, (MfaChallenge Value, DateTimeOffset ExpiresAt)> _challenges = new();
    private readonly ConcurrentDictionary<string, (MfaEnrollment Value, DateTimeOffset ExpiresAt)> _enrollments = new();
    private readonly TimeProvider _clock;

    public InMemoryMfaChallengeService(TimeProvider clock) => _clock = clock;

    public string CreateChallenge(Guid userId, bool requiresEnrollment)
    {
        var id = NewToken();
        _challenges[id] = (new MfaChallenge { UserId = userId, RequiresEnrollment = requiresEnrollment }, _clock.GetUtcNow() + Ttl);
        return id;
    }

    public MfaChallenge? ResolveChallenge(string challengeId) => Resolve(_challenges, challengeId);

    public void ConsumeChallenge(string challengeId) => _challenges.TryRemove(challengeId, out _);

    public string CreateEnrollment(Guid userId, string base32Secret)
    {
        var token = NewToken();
        _enrollments[token] = (new MfaEnrollment { UserId = userId, Base32Secret = base32Secret }, _clock.GetUtcNow() + Ttl);
        return token;
    }

    public MfaEnrollment? ResolveEnrollment(string enrollmentToken) => Resolve(_enrollments, enrollmentToken);

    public void ConsumeEnrollment(string enrollmentToken) => _enrollments.TryRemove(enrollmentToken, out _);

    private T? Resolve<T>(ConcurrentDictionary<string, (T Value, DateTimeOffset ExpiresAt)> store, string key)
        where T : class
    {
        if (!store.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAt <= _clock.GetUtcNow())
        {
            store.TryRemove(key, out _);
            return null;
        }

        return entry.Value;
    }

    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
