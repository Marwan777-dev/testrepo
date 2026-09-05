using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>
/// Output of <c>SessionService.CreateSessionAsync</c>: the raw opaque token (returned
/// to the client exactly once — only its hash is persisted) plus the created session.
/// </summary>
public sealed record SessionCreationResult
{
    public required string RawToken { get; init; }

    public required AuthSession Session { get; init; }
}
