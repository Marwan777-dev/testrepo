using Nabadat.UserManagement.Domain.Interfaces;

namespace Nabadat.UserManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Test double for M-09 that succeeds (so password-reset requests reach 202 instead
/// of the production fail-closed 503) and captures the raw reset token, letting a
/// scenario test redeem it end-to-end.
/// </summary>
public sealed class CapturingM09NotificationService : IM09NotificationService
{
    public string? LastEmail { get; private set; }

    public string? LastRawToken { get; private set; }

    public Task SendPasswordResetAsync(string email, string rawToken, CancellationToken ct = default)
    {
        LastEmail = email;
        LastRawToken = rawToken;
        return Task.CompletedTask;
    }
}
