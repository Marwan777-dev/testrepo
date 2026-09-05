using HostCurrentTenant = Nabadat.UserManagement.Application.Interfaces.ICurrentTenant;
using ModuleCurrentTenant = Nabadat.SurveyBuilder.Application.Interfaces.ICurrentTenant;

namespace Nabadat.SurveyBuilder.Api.Accessors;

/// <summary>
/// Bridges M-01's own <see cref="ModuleCurrentTenant"/> (T010) to the host's request-scoped tenant
/// accessor (M-10's <c>ICurrentTenant</c>, wired by <c>ENABLE_MULTI_TENANT</c> to
/// <c>ConfiguredCurrentTenant</c> / <c>RequestCurrentTenant</c>). M-01 declares its own port so its
/// persistence/Api layers don't reference M-10 concretes; the host supplies the value, and this
/// adapter is the "concrete implementation lives in the host layer" the interface's remarks call for.
/// <para>Without this registration the M-01 error-envelope middleware — which resolves
/// <see cref="ModuleCurrentTenant"/> per request — fails DI resolution and every
/// <c>/api/v1/surveys</c> request 500s. Registered in <c>SurveyBuilderServiceCollectionExtensions</c>.</para>
/// </summary>
public sealed class HostCurrentTenantAdapter : ModuleCurrentTenant
{
    private readonly HostCurrentTenant _host;

    public HostCurrentTenantAdapter(HostCurrentTenant host) => _host = host;

    public Guid TenantId => _host.TenantId;

    public string Slug => _host.Slug;

    public bool IsResolved => _host.IsResolved;
}
