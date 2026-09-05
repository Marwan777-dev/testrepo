using Nabadat.UserManagement.Api.Tenancy;
using Nabadat.UserManagement.Application.Tenancy;
using Npgsql;

namespace Nabadat.TenantAdmin.Development;

/// <summary>
/// Development-only provisioner that makes multi-tenant mode runnable locally without an
/// M-11 provisioning module. For each tenant in the registry it ensures the
/// <c>tenant_{slug}</c> schema exists and applies <c>_Baseline.sql</c> into it; it also
/// ensures the control-plane tables exist once. Idempotent (guarded by <c>to_regclass</c>),
/// so restarts are safe. Production provisioning is M-11's job — this never runs outside Development.
/// </summary>
public static class DevTenantSchemaBootstrapper
{
    public static async Task EnsureSchemasAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DevTenantSchemaBootstrapper));
        var configuration = sp.GetRequiredService<IConfiguration>();
        var registry = sp.GetService<ITenantRegistry>();

        // Single-tenant mode has no registry — its one schema is provisioned out of band.
        if (registry is null)
        {
            return;
        }

        var tenantConn = configuration.GetConnectionString("TenantDb")
            ?? throw new InvalidOperationException("ConnectionStrings:TenantDb is not configured.");
        var controlPlaneConn = configuration.GetConnectionString("ControlPlaneDb")
            ?? throw new InvalidOperationException("ConnectionStrings:ControlPlaneDb is not configured.");

        var baselineSql = await ReadMigrationAsync("_Baseline.sql", ct);
        var controlPlaneSql = await ReadMigrationAsync("_ControlPlane.sql", ct);
        // M-16's journey tables live in a separate module baseline, copied to the host output via the
        // M-16 project reference. Tolerant read: a missing file skips the M-16 step rather than
        // crashing dev startup (e.g. if the module reference is ever dropped).
        var m16BaselineSql = await TryReadMigrationAsync("001_customer_journey_baseline.sql", ct);
        // M-06 CX Metrics & KPI Engine baseline, copied to host output via the M-06 project reference.
        // Same tolerant read so a dropped reference skips the step rather than crashing startup.
        var m06BaselineSql = await TryReadMigrationAsync("KpiManagement_Baseline.sql", ct);
        // M-06 organization_settings (US-6), M-06-owned, applied per tenant schema after the
        // M-06 baseline and gated by its own `organization_settings` sentinel. Tolerant read.
        var m06OrgSettingsSql = await TryReadMigrationAsync("KpiManagement_OrganizationSettings.sql", ct);
        // M-01 Survey & Form Builder baseline (Feature 004, T008), copied to host output via the
        // M-01 project reference under a module-prefixed name. Same tolerant read.
        var m01BaselineSql = await TryReadMigrationAsync("SurveyBuilder_Baseline.sql", ct);
        // M-13 Integration Hub baseline (feature 006, T009), copied to host output via the M-13 project
        // reference. Same tolerant read. It also seeds the 23 built-in parameters (FR-F0-10 / BR-23).
        var m13BaselineSql = await TryReadMigrationAsync("IntegrationHub_Baseline.sql", ct);

        // Control-plane tables (shared) — apply once if absent.
        await EnsureControlPlaneAsync(controlPlaneConn, controlPlaneSql, logger, ct);

        // Per-tenant schemas: M-10 baseline, then the M-16 journey baseline, then the M-06 KPI
        // baseline — each gated independently by its own sentinel table.
        foreach (var (slug, _) in registry.All)
        {
            await EnsureTenantSchemaAsync(tenantConn, slug, baselineSql, m16BaselineSql, m06BaselineSql, m06OrgSettingsSql, m01BaselineSql, m13BaselineSql, logger, ct);
        }
    }

    private static async Task EnsureTenantSchemaAsync(
        string connectionString, string slug, string baselineSql, string? m16BaselineSql, string? m06BaselineSql, string? m06OrgSettingsSql, string? m01BaselineSql, string? m13BaselineSql, ILogger logger, CancellationToken ct)
    {
        // Same slug→schema gate the request pipeline uses — a malformed registry slug must
        // never reach the CREATE SCHEMA / SET search_path DDL below (search-path injection).
        if (!TenantSlug.IsValid(slug))
        {
            logger.LogWarning("[DEV] Skipping tenant with unsafe slug '{Slug}'.", slug);
            return;
        }

        var schema = TenantSlug.SchemaName(slug);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using (var createSchema = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{schema}\";", conn))
        {
            await createSchema.ExecuteNonQueryAsync(ct);
        }

        // Both baselines' CREATE TABLEs are unqualified, so point search_path at this schema first.
        await using (var setPath = new NpgsqlCommand($"SET search_path TO \"{schema}\";", conn))
        {
            await setPath.ExecuteNonQueryAsync(ct);
        }

        // Apply each baseline independently, gated by a sentinel table, so a schema that already
        // carries the M-10 tables but predates the M-16 baseline is healed on the next run — and a
        // fully-provisioned schema is a no-op.
        var m10Applied = await ApplyBaselineIfAbsentAsync(conn, schema, "tenant_users", baselineSql, ct);
        var m16Applied = !string.IsNullOrWhiteSpace(m16BaselineSql)
            && await ApplyBaselineIfAbsentAsync(conn, schema, "journeys", m16BaselineSql!, ct);
        var m06Applied = !string.IsNullOrWhiteSpace(m06BaselineSql)
            && await ApplyBaselineIfAbsentAsync(conn, schema, "kpi_definitions", m06BaselineSql!, ct);
        var m06OrgApplied = !string.IsNullOrWhiteSpace(m06OrgSettingsSql)
            && await ApplyBaselineIfAbsentAsync(conn, schema, "organization_settings", m06OrgSettingsSql!, ct);
        var m01Applied = !string.IsNullOrWhiteSpace(m01BaselineSql)
            && await ApplyBaselineIfAbsentAsync(conn, schema, "surveys", m01BaselineSql!, ct);
        var m13Applied = !string.IsNullOrWhiteSpace(m13BaselineSql)
            && await ApplyBaselineIfAbsentAsync(conn, schema, "service_channels", m13BaselineSql!, ct);

        // The dev bootstrapper has no incremental-migration runner: a baseline is applied in full
        // only when its sentinel table is absent. Additive columns that landed in a baseline AFTER
        // a schema was first provisioned (e.g. kpi_bindings.kpi_id, Feature 003 / T020) therefore
        // never reach pre-existing schemas. Heal those with idempotent ADD COLUMN / CREATE INDEX
        // IF NOT EXISTS patches that run every startup and are a no-op once applied.
        await ApplyIdempotentPatchesAsync(conn, ct);

        logger.LogInformation(
            m10Applied || m16Applied || m06Applied || m06OrgApplied || m01Applied || m13Applied
                ? "[DEV] Provisioned tenant schema {Schema} (M-10 applied: {M10}, M-16 applied: {M16}, M-06 applied: {M06}, M-06 org applied: {M06Org}, M-01 applied: {M01}, M-13 applied: {M13})."
                : "[DEV] Schema {Schema} already provisioned (M-10 applied: {M10}, M-16 applied: {M16}, M-06 applied: {M06}, M-06 org applied: {M06Org}, M-01 applied: {M01}, M-13 applied: {M13}).",
            schema, m10Applied, m16Applied, m06Applied, m06OrgApplied, m01Applied, m13Applied);
    }

    /// <summary>
    /// Applies <paramref name="sql"/> within the connection's current schema only when its
    /// <paramref name="sentinelTable"/> is absent; returns whether it ran. The probe casts
    /// regclass → text so Npgsql yields a string (NULL when the table is missing), making each
    /// baseline independently idempotent.
    /// </summary>
    private static async Task<bool> ApplyBaselineIfAbsentAsync(
        NpgsqlConnection conn, string schema, string sentinelTable, string sql, CancellationToken ct)
    {
        await using (var probe = new NpgsqlCommand($"SELECT to_regclass('\"{schema}\".{sentinelTable}')::text;", conn))
        {
            if (await probe.ExecuteScalarAsync(ct) is not (null or DBNull))
            {
                return false;
            }
        }

        await using (var ddl = new NpgsqlCommand(sql, conn))
        {
            await ddl.ExecuteNonQueryAsync(ct);
        }

        return true;
    }

    /// <summary>
    /// Runs idempotent additive DDL patches against the connection's current schema to bring a
    /// schema provisioned before a later baseline change up to date. Each statement is guarded by
    /// <c>IF NOT EXISTS</c>, so this is safe to run on every startup and a no-op once applied.
    /// Production schema evolution is M-11's job — this only keeps the dev loop runnable.
    /// </summary>
    private static async Task ApplyIdempotentPatchesAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string patches =
            // Feature 003 / T020: logical reference to M-06's kpi_definitions.id on kpi_bindings.
            "ALTER TABLE kpi_bindings ADD COLUMN IF NOT EXISTS kpi_id uuid NULL;" +
            "CREATE INDEX IF NOT EXISTS ix_kpi_bindings_kpi_id ON kpi_bindings (kpi_id);" +
            // Feature 002 US-2 Amendment: reshape scoring_configs per-journey → per-tenant SINGLETON
            // (SRS §4.2.9 / §11.7, Q11). Idempotent and NON-destructive after the first run: the
            // incompatible per-journey rows are cleared ONLY while the old journey_id column still
            // exists (the DO guard), so a tenant's saved singleton survives later restarts.
            "DO $$ BEGIN " +
            "  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() " +
            "             AND table_name = 'scoring_configs' AND column_name = 'journey_id') THEN " +
            "    DELETE FROM scoring_configs; " +
            "  END IF; END $$;" +
            "ALTER TABLE scoring_configs DROP CONSTRAINT IF EXISTS uq_scoring_configs_journey_id;" +
            "ALTER TABLE scoring_configs DROP COLUMN IF EXISTS journey_id;" +
            "ALTER TABLE scoring_configs DROP COLUMN IF EXISTS model_type;" +
            "ALTER TABLE scoring_configs DROP COLUMN IF EXISTS stage_weight_mode;" +
            "ALTER TABLE scoring_configs DROP COLUMN IF EXISTS normalization_params;" +
            "ALTER TABLE scoring_configs ADD COLUMN IF NOT EXISTS alpha numeric(4,3) NOT NULL DEFAULT 0.500;" +
            "ALTER TABLE scoring_configs ADD COLUMN IF NOT EXISTS mot_multiplier numeric(3,1) NOT NULL DEFAULT 1.5;" +
            "ALTER TABLE scoring_configs ADD COLUMN IF NOT EXISTS n_floor integer NOT NULL DEFAULT 100;" +
            "ALTER TABLE scoring_configs ADD COLUMN IF NOT EXISTS flag_percentile integer NOT NULL DEFAULT 25;" +
            "ALTER TABLE scoring_configs ADD COLUMN IF NOT EXISTS rolling_window_days integer NOT NULL DEFAULT 30;" +
            "ALTER TABLE scoring_configs ADD COLUMN IF NOT EXISTS updated_by uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';" +
            "CREATE UNIQUE INDEX IF NOT EXISTS scoring_configs_singleton_uniq ON scoring_configs ((true));" +
            "INSERT INTO scoring_configs (scoring_config_id, created_at, updated_at) " +
            "SELECT gen_random_uuid(), now(), now() WHERE NOT EXISTS (SELECT 1 FROM scoring_configs);";

        // Only meaningful once the M-16 baseline (which owns kpi_bindings) exists in this schema.
        await using var probe = new NpgsqlCommand("SELECT to_regclass('kpi_bindings')::text;", conn);
        if (await probe.ExecuteScalarAsync(ct) is null or DBNull)
        {
            return;
        }

        await using var ddl = new NpgsqlCommand(patches, conn);
        await ddl.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureControlPlaneAsync(
        string connectionString, string controlPlaneSql, ILogger logger, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using (var probe = new NpgsqlCommand("SELECT to_regclass('public.persona_baselines')::text;", conn))
        {
            if (await probe.ExecuteScalarAsync(ct) is not (null or DBNull))
            {
                return;
            }
        }

        await using (var ddl = new NpgsqlCommand(controlPlaneSql, conn))
        {
            await ddl.ExecuteNonQueryAsync(ct);
        }

        logger.LogInformation("[DEV] Provisioned control-plane tables.");
    }

    private static async Task<string> ReadMigrationAsync(string fileName, CancellationToken ct)
    {
        // The module csprojs copy their Migrations/*.sql to the host output directory.
        var path = Path.Combine(AppContext.BaseDirectory, "Migrations", fileName);
        return await File.ReadAllTextAsync(path, ct);
    }

    /// <summary>As <see cref="ReadMigrationAsync"/> but returns <c>null</c> when the file is absent
    /// (an optional module baseline), so a missing file never crashes dev startup.</summary>
    private static async Task<string?> TryReadMigrationAsync(string fileName, CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Migrations", fileName);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
    }
}
