using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Interfaces;

/// <summary>
/// Application-owned abstraction of the global control-plane EF context (implemented by
/// <c>ControlPlaneDbContext</c> in Infrastructure). Control-plane data-access services
/// depend on this so they can live in the Application layer. Control-plane writes are their
/// own <see cref="SaveChangesAsync"/> — never atomic with a tenant write (DB-08).
/// </summary>
public interface IControlPlaneDbContext
{
    DbSet<PersonaBaseline> PersonaBaselines { get; }

    DbSet<IdentityProviderConfig> IdentityProviderConfigs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
