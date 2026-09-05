using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// EF data-access service over the control-plane <c>identity_provider_configs</c> table
/// (forward-compatibility for FR-004/FR-018 SSO; no Phase-1 endpoint). Replaces the
/// raw-Npgsql <c>IdentityProviderConfigRepository</c>. Control-plane writes are their own
/// <c>SaveChangesAsync</c> on <see cref="IControlPlaneDbContext"/>.
/// </summary>
public sealed class IdentityProviderConfigService
{
    private readonly IControlPlaneDbContext _context;
    private readonly TimeProvider _timeProvider;

    public IdentityProviderConfigService(IControlPlaneDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<IdentityProviderConfig>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
        await _context.IdentityProviderConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.ProviderType)
            .ToListAsync(ct);

    /// <summary>Inserts or updates the config for <c>(tenant_id, provider_type)</c> and saves.</summary>
    public async Task UpsertAsync(IdentityProviderConfig config, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        var existing = await _context.IdentityProviderConfigs
            .FirstOrDefaultAsync(c => c.TenantId == config.TenantId && c.ProviderType == config.ProviderType, ct);

        if (existing is null)
        {
            if (config.ProviderId == Guid.Empty)
            {
                config.ProviderId = Guid.NewGuid();
            }

            config.CreatedAt = config.CreatedAt == default ? now : config.CreatedAt;
            config.UpdatedAt = now;
            _context.IdentityProviderConfigs.Add(config);
        }
        else
        {
            existing.Settings = config.Settings;
            existing.IsActive = config.IsActive;
            existing.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(ct);
    }
}
