using Microsoft.Extensions.Configuration;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Api.Accessors;

/// <summary>
/// Reads the host's tenant id from configuration key <c>Tenant:Id</c> (each tenant
/// runs its own host instance against its own schema). Falls back to
/// <see cref="Guid.Empty"/> when unconfigured — control-plane lookups then find no
/// per-tenant rows and degrade to platform defaults rather than failing the request.
/// Replace with a request-scoped resolver if/when a single host serves multiple tenants.
/// </summary>
public sealed class ConfiguredCurrentTenant : ICurrentTenant
{
    public ConfiguredCurrentTenant(IConfiguration configuration) =>
        TenantId = Guid.TryParse(configuration["Tenant:Id"], out var id) ? id : Guid.Empty;

    public Guid TenantId { get; }

    /// <summary>
    /// Always empty in single-tenant mode: the host binds to its one schema via the
    /// plain <c>ConnectionStrings:TenantDb</c> with no <c>search_path</c> override.
    /// </summary>
    public string Slug => string.Empty;

    /// <summary>
    /// Always <c>true</c>: in single-tenant mode the host's one tenant is fixed by
    /// configuration, so there is nothing to resolve per request. The empty
    /// <see cref="Slug"/> deliberately selects the default schema.
    /// </summary>
    public bool IsResolved => true;
}
