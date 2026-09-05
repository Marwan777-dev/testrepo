using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Auth.Interfaces;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>EF <see cref="IPasswordResetTokenService"/> over <see cref="ITenantDbContext"/>.
/// <c>MarkUsedAsync</c> uses <c>ExecuteUpdateAsync</c> so it participates in the ambient
/// unit-of-work transaction.</summary>
public sealed class PasswordResetTokenService : IPasswordResetTokenService
{
    private readonly ITenantDbContext _context;

    public PasswordResetTokenService(ITenantDbContext context) => _context = context;

    public async Task<PasswordResetToken?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default) =>
        await _context.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        _context.PasswordResetTokens.Add(token);
        await _context.SaveChangesAsync(ct);
    }

    public Task MarkUsedAsync(Guid tokenId, DateTimeOffset usedAtUtc, CancellationToken ct = default) =>
        _context.PasswordResetTokens
            .Where(t => t.TokenId == tokenId)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAtUtc, usedAtUtc), ct);
}
