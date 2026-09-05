using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Api.Accessors;

/// <summary>
/// Request-scoped <see cref="ICurrentTenant"/> for multi-tenant mode
/// (<c>ENABLE_MULTI_TENANT=true</c>). Populated exactly once per request by
/// <c>TenantResolutionMiddleware</c> from the request subdomain (AD-07 / API-02),
/// before authentication runs — so the per-request <c>TenantDbContext</c> binds to the
/// correct <c>tenant_{slug}</c> schema when the auth service first reads it.
/// </summary>
/// <remarks>
/// AD-07 makes tenant context immutable once resolved: <see cref="Resolve"/> throws on
/// a second call, so no downstream code can repoint the request at another tenant.
/// </remarks>
public sealed class RequestCurrentTenant : ICurrentTenant
{
    public Guid TenantId { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    /// <summary>True once <see cref="Resolve"/> has run for this request.</summary>
    public bool IsResolved { get; private set; }

    public void Resolve(Guid tenantId, string slug)
    {
        if (IsResolved)
        {
            throw new InvalidOperationException(
                "Tenant context is immutable once resolved (AD-07); it cannot be reassigned mid-request.");
        }

        TenantId = tenantId;
        Slug = slug;
        IsResolved = true;
    }
}
