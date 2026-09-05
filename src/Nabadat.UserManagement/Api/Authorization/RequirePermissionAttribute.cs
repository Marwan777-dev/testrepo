using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.UserManagement.Api.Authorization;

/// <summary>
/// Declarative permission gate for a controller (or action): the authenticated actor's effective
/// <see cref="Domain.ValueObjects.PermissionSnapshot"/> must grant <see cref="Mode"/> on the DOC-02
/// <see cref="Module"/> — e.g. <c>[RequirePermission("KpiConfiguration", "Manage")]</c> on a write,
/// <c>[RequirePermission("KpiConfiguration", "View")]</c> on a read. Layers on top of the
/// authentication gate (<c>[Authorize]</c>, which 401s an unauthenticated request first); a missing
/// grant short-circuits with 403 + the API-05 envelope (code <c>PERMISSION_DENIED</c>).
///
/// <para>This is a cross-cutting platform filter — any module's controllers (M-06, M-16, …) may
/// apply it. It is an <see cref="IFilterFactory"/> rather than a plain attribute so the filter can
/// resolve the request-scoped <see cref="ISessionContextAccessor"/> from DI; an attribute by itself
/// cannot take constructor dependencies.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(PermissionModule module, PermissionMode mode) : Attribute, IFilterFactory
{
    /// <summary>The DOC-02 module the action belongs to (e.g. <see cref="PermissionModule.KpiConfiguration"/>).</summary>
    public PermissionModule Module { get; } = module;

    /// <summary>Coarse mode required to perform the action: <see cref="PermissionMode.View"/> / <see cref="PermissionMode.Manage"/> / <see cref="PermissionMode.Full"/>.</summary>
    public PermissionMode Mode { get; } = mode;

    /// <summary>A fresh filter is created per request (it captures the request-scoped session).</summary>
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new RequirePermissionFilter(
            Module.ToString(),
            Mode.ToString(),
            serviceProvider.GetRequiredService<ISessionContextAccessor>());
}
