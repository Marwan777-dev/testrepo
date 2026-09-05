using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Api.Accessors;
using Nabadat.UserManagement.Application.Tenancy;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Multi-tenant variant of the M-10 fixture (<c>ENABLE_MULTI_TENANT=true</c>). Boots one
/// Dockerised PostgreSQL holding TWO tenant schemas (<c>tenant_alpha</c>, <c>tenant_beta</c>)
/// and exposes a per-tenant DI scope via <see cref="CreateTenantScope"/> — the same thing
/// <c>TenantResolutionMiddleware</c> does per request: resolve <c>RequestCurrentTenant</c>
/// to a slug so the scope's <c>TenantDbContext</c> binds to that tenant's schema.
///
/// It exists to prove <c>TenantSchemaConnectionInterceptor</c> isolates tenants while ALL
/// of them share one connection string / one Npgsql pool (the design chosen over baking the
/// schema into a per-tenant connection string). Standalone (its own container) so it does
/// not perturb the single-tenant <see cref="UserManagementApplicationFactory"/> collection fixture.
/// </summary>
public sealed class MultiTenantApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AlphaSlug = "alpha";
    public const string BetaSlug = "beta";
    public static readonly Guid AlphaId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid BetaId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

#pragma warning disable CS0618 // PostgreSqlBuilder() ctor deprecated upstream; still functional in Testcontainers 4.x.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("nabadat_tenant")
        .WithUsername("nabadat")
        .WithPassword("nabadat")
        .Build();
#pragma warning restore CS0618

    private static readonly string MfaEncryptionKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray());

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Control-plane tables (shared, public schema), then one schema per tenant — the
        // dev bootstrapper's job, done here directly since the dev block does not run under
        // the test host's (Production) environment.
        await ApplyControlPlaneAsync();
        await ProvisionTenantSchemaAsync(AlphaSlug);
        await ProvisionTenantSchemaAsync(BetaSlug);
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TenantDb"] = ConnectionString,
                ["ConnectionStrings:ControlPlaneDb"] = ConnectionString,
                ["ENABLE_MULTI_TENANT"] = "true",
                ["MfaEncryptionKey"] = MfaEncryptionKeyBase64,
                [$"Tenants:{AlphaSlug}:Id"] = AlphaId.ToString(),
                [$"Tenants:{AlphaSlug}:DisplayName"] = "Alpha",
                [$"Tenants:{BetaSlug}:Id"] = BetaId.ToString(),
                [$"Tenants:{BetaSlug}:DisplayName"] = "Beta",
            });
        });
    }

    /// <summary>
    /// A DI scope whose <c>ICurrentTenant</c> is frozen to <paramref name="slug"/> —
    /// resolving a <c>TenantDbContext</c> from it binds to <c>tenant_{slug}</c> via the
    /// connection interceptor, exactly as a real request would after middleware resolution.
    /// </summary>
    public IServiceScope CreateTenantScope(string slug, Guid id)
    {
        var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RequestCurrentTenant>().Resolve(id, slug);
        return scope;
    }

    private async Task ApplyControlPlaneAsync()
    {
        var sql = await ReadMigrationAsync("_ControlPlane.sql");
        if (sql is null)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ProvisionTenantSchemaAsync(string slug)
    {
        var baseline = await ReadMigrationAsync("_Baseline.sql");
        if (baseline is null)
        {
            return;
        }

        var schema = TenantSlug.SchemaName(slug);
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // CREATE then point search_path at the new schema so the baseline's unqualified
        // CREATE TABLEs land in it. Both run on the same open connection, so the SET sticks.
        await using (var setup = new NpgsqlCommand(
            $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"; SET search_path TO \"{schema}\";", conn))
        {
            await setup.ExecuteNonQueryAsync();
        }

        await using var ddl = new NpgsqlCommand(baseline, conn);
        await ddl.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadMigrationAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Migrations", fileName);
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
    }
}
