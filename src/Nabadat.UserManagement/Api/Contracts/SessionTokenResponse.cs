using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Session-created response shared by MFA verify and enrollment confirm.</summary>
public sealed record SessionTokenResponse
{
    public required string SessionToken { get; init; }

    public required Guid UserId { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required PermissionSnapshot PermissionSnapshot { get; init; }
}
