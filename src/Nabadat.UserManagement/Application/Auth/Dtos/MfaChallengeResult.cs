using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>
/// Output of a successful MFA verification (challenge or enrollment confirm): the
/// new session token plus the data the client needs to bootstrap its session.
/// </summary>
public sealed record MfaChallengeResult
{
    public required string SessionToken { get; init; }

    public required Guid UserId { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required PermissionSnapshot PermissionSnapshot { get; init; }
}
