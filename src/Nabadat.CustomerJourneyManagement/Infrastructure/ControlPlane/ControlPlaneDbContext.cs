using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.ControlPlane;

/// <summary>
/// EF Core context over the global control-plane database (<c>ConnectionStrings:ControlPlaneDb</c>).
/// The second of M-16's two data-access ports (<see cref="IControlPlaneDbContext"/>), mirroring
/// the M-10 reference module's two-context shape (architecture-constitution Article 1A).
///
/// <para><b>M-16 owns no control-plane tables today</b> — all M-16 data is tenant-scoped
/// (DB-02/AD-02), so this context maps no entities and issues no queries. It is wired for
/// convention parity with M-10 and as the seam for any future M-16 control-plane table; a
/// control-plane write would be its own <c>SaveChangesAsync</c>, never atomic with a tenant
/// write (DB-08).</para>
/// </summary>
public sealed class ControlPlaneDbContext : DbContext, IControlPlaneDbContext
{
    public ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : base(options)
    {
    }
}
