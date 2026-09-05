namespace Nabadat.UserManagement.Application.Interfaces;

/// <summary>
/// The tenant the current request belongs to. M-10 tenant data is schema-isolated
/// (DB-02, no <c>tenant_id</c> columns), so <see cref="Slug"/> selects the
/// <c>tenant_{slug}</c> schema the per-request <c>TenantDbContext</c> binds to;
/// <see cref="TenantId"/> keys the shared control-plane rows (persona baselines,
/// SSO configs) that DO carry a tenant id.
/// </summary>
/// <remarks>
/// An Application-owned abstraction (peer of <c>ITenantDbContext</c>) so the persistence
/// layer's schema interceptor and the Application services can both depend on it without
/// referencing the Api layer. Named <c>ICurrentTenant</c> (not <c>…Context</c>) so it is
/// never confused with the EF <c>TenantDbContext</c>. Two implementations are wired by the
/// <c>ENABLE_MULTI_TENANT</c> flag (AD-05), both living in <c>Api/Accessors</c>:
/// <list type="bullet">
///   <item><c>ConfiguredCurrentTenant</c> (flag off) — single tenant per host,
///   id from <c>Tenant:Id</c>, empty slug → default schema.</item>
///   <item><c>RequestCurrentTenant</c> (flag on) — resolved once per request from the
///   subdomain by <c>TenantResolutionMiddleware</c> (AD-07 / API-02).</item>
/// </list>
/// </remarks>
public interface ICurrentTenant
{
    Guid TenantId { get; }

    /// <summary>
    /// The tenant's URL slug, used as the <c>tenant_{slug}</c> schema name.
    /// Empty string in single-tenant mode — callers treat empty as "default schema".
    /// </summary>
    string Slug { get; }

    /// <summary>
    /// True when a concrete tenant has been established for this request. Single-tenant
    /// implementations are always resolved (to the host's one tenant, whose empty
    /// <see cref="Slug"/> means the default schema). The multi-tenant request
    /// implementation is resolved only after <c>TenantResolutionMiddleware</c> runs — so a
    /// <c>false</c> here on a DB-touching path means a tenant was never resolved, and the
    /// schema interceptor refuses to run rather than silently using the default schema (GP-04).
    /// </summary>
    bool IsResolved { get; }
}
