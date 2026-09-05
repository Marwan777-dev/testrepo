namespace Nabadat.CustomerJourneyManagement.Application.Interfaces;

/// <summary>
/// Application-owned abstraction of the global control-plane EF context (implemented by
/// <c>ControlPlaneDbContext</c> in Infrastructure). Mirrors the M-10 reference module's
/// two-context-port shape (architecture-constitution Article 1A): the second of M-16's two
/// data-access ports. A control-plane write is its own <see cref="SaveChangesAsync"/> and is
/// never atomic with a tenant write (DB-08).
///
/// <para><b>M-16 owns no control-plane tables today</b> — all M-16 data is tenant-scoped
/// (plan.md, spec.md DB-02/AD-02), so this port exposes no <c>DbSet</c>s. It exists for
/// convention parity with M-10 and as the seam for any future M-16 control-plane table; the
/// concrete context binds to <c>ConnectionStrings:ControlPlaneDb</c> but issues no queries
/// until an entity is mapped onto it.</para>
/// </summary>
public interface IControlPlaneDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
