using Nabadat.UserManagement.Application.Auth.Dtos;

namespace Nabadat.UserManagement.Application.Auth.Interfaces;

/// <summary>
/// Short-lived (5-minute TTL) server-side store for post-password MFA challenges
/// and pending enrollments, keyed by opaque ids (auth-api.md). These are transient
/// auth-flow artifacts, not a data cache — the default implementation is in-memory;
/// it can be swapped for a durable store if AD-03 strictness later requires it.
/// </summary>
public interface IMfaChallengeService
{
    /// <summary>Creates a challenge after a successful password step; returns its opaque id.</summary>
    string CreateChallenge(Guid userId, bool requiresEnrollment);

    /// <summary>Resolves a challenge id, or null if unknown/expired.</summary>
    MfaChallenge? ResolveChallenge(string challengeId);

    /// <summary>Removes a challenge once consumed.</summary>
    void ConsumeChallenge(string challengeId);

    /// <summary>Creates a pending enrollment holding the generated secret; returns its opaque token.</summary>
    string CreateEnrollment(Guid userId, string base32Secret);

    /// <summary>Resolves an enrollment token, or null if unknown/expired.</summary>
    MfaEnrollment? ResolveEnrollment(string enrollmentToken);

    /// <summary>Removes an enrollment once consumed.</summary>
    void ConsumeEnrollment(string enrollmentToken);
}
