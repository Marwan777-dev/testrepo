using Nabadat.UserManagement.Domain.Interfaces;

namespace Nabadat.UserManagement.Infrastructure.Notifications;

/// <summary>
/// Placeholder <see cref="IM09NotificationService"/> registered until the M-09
/// Notifications module ships. It throws so that a password-reset request fails
/// closed (the token write rolls back) and the endpoint returns 503 — matching the
/// auth-api.md "M-09 unavailable" contract. Replace by registering M-09's real client.
/// </summary>
public sealed class UnavailableM09NotificationService : IM09NotificationService
{
    public Task SendPasswordResetAsync(string email, string rawToken, CancellationToken ct = default) =>
        throw new InvalidOperationException("The M-09 Notifications module is not available in this deployment.");
}
