using Npgsql;
using Nabadat.TenantAdmin.Development;

namespace Nabadat.TenantAdmin;

/// <summary>
/// On-startup schema migration and data seeder for production deployments.
/// Activated by <c>MIGRATE_ON_STARTUP=true</c> and/or <c>SEED_ON_STARTUP=true</c>
/// in appsettings.Production.json. Idempotent — checks whether key tables already
/// exist before running SQL. After the first successful startup, set both flags to
/// false so subsequent restarts don't re-seed dev accounts.
/// </summary>
public static class ProductionSetupRunner
{
    public static async Task RunAsync(WebApplication app, CancellationToken ct = default)
    {
        var config = app.Configuration;
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ProductionSetup");

        var migrate = config.GetValue<bool>("MIGRATE_ON_STARTUP");
        var seed    = config.GetValue<bool>("SEED_ON_STARTUP");

        if (!migrate && !seed) return;

        if (migrate)
        {
            logger.LogInformation("[Setup] Running database migrations...");
            await RunMigrationsAsync(config, logger, ct);
            logger.LogInformation("[Setup] Migrations complete.");
        }

        if (seed)
        {
            logger.LogInformation("[Setup] Seeding initial data...");
            await DevDataSeeder.SeedAsync(app.Services, ct);
            logger.LogInformation("[Setup] Seeding complete.");
        }
    }

    private static async Task RunMigrationsAsync(IConfiguration config, ILogger logger, CancellationToken ct)
    {
        var tenantConn  = GetConnStr(config, "TenantDb");
        var controlConn = GetConnStr(config, "ControlPlaneDb");
        var dir         = Path.Combine(AppContext.BaseDirectory, "Migrations");

        // Control-plane tables (persona_baselines, identity_provider_configs)
        await ApplyIfNeededAsync(controlConn, Path.Combine(dir, "_ControlPlane.sql"),
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='persona_baselines'",
            logger, ct);

        // Tenant tables (users, sessions, auth, etc.)
        await ApplyIfNeededAsync(tenantConn, Path.Combine(dir, "_Baseline.sql"),
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='tenant_users'",
            logger, ct);

        // Customer journey tables
        await ApplyIfNeededAsync(tenantConn, Path.Combine(dir, "001_customer_journey_baseline.sql"),
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='journeys'",
            logger, ct);
    }

    private static string GetConnStr(IConfiguration config, string name) =>
        config.GetConnectionString(name)
        ?? throw new InvalidOperationException($"ConnectionStrings:{name} is not configured.");

    private static async Task ApplyIfNeededAsync(
        string connStr, string filePath, string existsCheckSql, ILogger logger, CancellationToken ct)
    {
        var file = Path.GetFileName(filePath);

        if (!File.Exists(filePath))
        {
            logger.LogWarning("[Setup] Migration file not found, skipping: {File}", file);
            return;
        }

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        await using var check = new NpgsqlCommand(existsCheckSql, conn);
        var count = (long)(await check.ExecuteScalarAsync(ct) ?? 0L);
        if (count > 0)
        {
            logger.LogInformation("[Setup] Already applied, skipping: {File}", file);
            return;
        }

        logger.LogInformation("[Setup] Applying: {File}", file);
        var sql = await File.ReadAllTextAsync(filePath, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
        logger.LogInformation("[Setup] Applied: {File}", file);
    }
}
