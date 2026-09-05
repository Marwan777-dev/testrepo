using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Tenancy;

namespace Nabadat.UserManagement.Infrastructure.Persistence;

/// <summary>
/// Binds the per-request tenant schema onto a freshly opened pooled connection by issuing
/// <c>SET search_path TO "tenant_{slug}"</c> (AD-02 / DB-01). Unlike baking the schema into
/// the connection string — which forks Npgsql's pool per tenant — this keeps ALL tenants
/// on one shared connection string and one pool: the schema is selected per connection
/// open instead. Npgsql resets connection state on return to the pool, so a slug never
/// leaks from one request to the next that reuses the same physical connection.
/// </summary>
/// <remarks>
/// An EF persistence concern, so it lives in Infrastructure next to <c>TenantDbContext</c>
/// and depends only inward (Application's <see cref="ICurrentTenant"/> + <see cref="TenantSlug"/>),
/// never on the Api layer. Scoped, because it reads the scoped <see cref="ICurrentTenant"/>.
/// An empty slug (single-tenant mode) leaves the connection on its default schema — the
/// interceptor no-ops. The slug is re-validated here as defence in depth even though
/// <c>TenantResolutionMiddleware</c> already rejected malformed slugs at the edge.
/// </remarks>
public sealed class TenantSchemaConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ICurrentTenant _currentTenant;

    public TenantSchemaConnectionInterceptor(ICurrentTenant currentTenant) => _currentTenant = currentTenant;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        ApplySearchPath(connection);

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) =>
        await ApplySearchPathAsync(connection, cancellationToken);

    private void ApplySearchPath(DbConnection connection)
    {
        var sql = BuildSetSearchPathSql(_currentTenant.Slug, _currentTenant.IsResolved);
        if (sql is null)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private async Task ApplySearchPathAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var sql = BuildSetSearchPathSql(_currentTenant.Slug, _currentTenant.IsResolved);
        if (sql is null)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// The <c>SET search_path</c> statement for the current tenant, or <c>null</c> when no
    /// statement is needed (a resolved tenant with an empty slug → single-tenant default
    /// schema). Fails closed in two ways so a query never runs against the wrong schema:
    /// <list type="bullet">
    ///   <item><paramref name="isResolved"/> is <c>false</c> → throw. A DB operation reached
    ///   the interceptor on a request whose tenant was never resolved (e.g. a bypass route
    ///   that unexpectedly touches the DB); running on the default schema would risk
    ///   cross-tenant leakage (GP-04), so refuse rather than fall back.</item>
    ///   <item>the slug is non-empty but unsafe → throw, so a bad slug can never be
    ///   interpolated into SQL.</item>
    /// </list>
    /// </summary>
    public static string? BuildSetSearchPathSql(string slug, bool isResolved)
    {
        if (!isResolved)
        {
            throw new InvalidOperationException(
                "No tenant was resolved for this request; refusing to run a query on the default schema (GP-04).");
        }

        if (string.IsNullOrEmpty(slug))
        {
            // Single-tenant mode: no subdomain slug, but we still need to pin the schema
            // to "public". Without this, PgBouncer-fronted servers (which block the
            // search_path startup parameter) would use the server's role default, which
            // may not include "public".
            return "SET search_path TO public";
        }

        if (!TenantSlug.IsValid(slug))
        {
            throw new InvalidOperationException(
                $"Refusing to set search_path for unsafe tenant slug '{slug}'.");
        }

        return $"SET search_path TO \"{TenantSlug.SchemaName(slug)}\"";
    }
}
