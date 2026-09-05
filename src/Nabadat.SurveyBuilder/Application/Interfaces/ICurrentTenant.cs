namespace Nabadat.SurveyBuilder.Application.Interfaces;

/// <summary>
/// The tenant the current request belongs to. M-01 data is schema-isolated (DB-02, no
/// <c>tenant_id</c> columns), so <see cref="Slug"/> selects the <c>tenant_{slug}</c> schema the
/// per-request <c>TenantDbContext</c> binds to; <see cref="TenantId"/> keys any shared
/// control-plane rows.
/// </summary>
/// <remarks>
/// An Application-owned abstraction (peer of <see cref="ITenantDbContext"/>, read-only per AD-07)
/// so the persistence layer's schema interceptor and the Application services can both depend on
/// it without referencing the Api layer. Named <c>ICurrentTenant</c> (not <c>…Context</c>) so it
/// is never confused with the EF <c>TenantDbContext</c> (architecture-constitution Article 1A
/// rule 7). The concrete implementation lives in the host layer (<c>Nabadat.TenantAdmin</c>),
/// wired by the <c>ENABLE_MULTI_TENANT</c> flag (AD-05); M-01 only consumes it.
/// </remarks>
public interface ICurrentTenant
{
    Guid TenantId { get; }

    /// <summary>
    /// The tenant's URL slug, used as the <c>tenant_{slug}</c> schema name. Empty string in
    /// single-tenant mode — callers treat empty as "default schema".
    /// </summary>
    string Slug { get; }

    /// <summary>
    /// True when a concrete tenant has been established for this request. A <c>false</c> on a
    /// DB-touching path means a tenant was never resolved, and the schema interceptor refuses to
    /// run rather than silently using the default schema (GP-04).
    /// </summary>
    bool IsResolved { get; }
}
