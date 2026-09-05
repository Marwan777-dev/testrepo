using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Auth.Interfaces;

/// <summary>
/// Context-holding data-access service over <c>password_reset_tokens</c> (EF /
/// <c>TenantDbContext</c>), replacing the raw-Npgsql <c>IPasswordResetTokenRepository</c>.
/// Write methods persist immediately; compose them inside
/// <c>ITenantDbContext.ExecuteAsync</c> to commit atomically with other writes.
/// </summary>
public interface IPasswordResetTokenService
{
    Task<PasswordResetToken?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);

    /// <summary>Inserts a new token and saves.</summary>
    Task AddAsync(PasswordResetToken token, CancellationToken ct = default);

    /// <summary>Marks a token redeemed (sets <c>used_at_utc</c>).</summary>
    Task MarkUsedAsync(Guid tokenId, DateTimeOffset usedAtUtc, CancellationToken ct = default);
}
