using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Infrastructure.ControlPlane.Configurations;

namespace Nabadat.UserManagement.Infrastructure.ControlPlane;

/// <summary>
/// EF Core context over the global control-plane PostgreSQL database
/// (<c>ConnectionStrings:ControlPlaneDb</c>) — persona baselines and SSO configs.
///
/// <para>Separate from <c>TenantDbContext</c> by design (DB-08 / database-constitution
/// Article 7): a control-plane write is its own <c>SaveChangesAsync</c> and is <b>never</b>
/// atomic with a tenant write (the connection topology cannot honour a cross-database
/// transaction — the documented cause of a prior 42P01). Control-plane tables carry an
/// explicit <c>tenant_id</c> column (DB-02 exemption).</para>
/// </summary>
public sealed class ControlPlaneDbContext : DbContext, IControlPlaneDbContext
{
    public ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : base(options)
    {
    }

    public DbSet<PersonaBaseline> PersonaBaselines => Set<PersonaBaseline>();

    public DbSet<IdentityProviderConfig> IdentityProviderConfigs => Set<IdentityProviderConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PersonaBaselineConfiguration());
        modelBuilder.ApplyConfiguration(new IdentityProviderConfigConfiguration());
    }
}
