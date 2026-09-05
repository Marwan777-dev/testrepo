using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Npgsql;
using OtpNet;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Shared integration-test fixture for the M-06 KPI Management module (T026 — first M-06
/// integration story owns it; later stories reuse + extend the seeding helpers, per CLAUDE.md
/// Unit Test Policy rule 12).
///
/// <para>Boots a fresh Dockerised PostgreSQL via Testcontainers and applies, in order, the M-10
/// tenant baseline (<c>_Baseline.sql</c> — required because M-06 endpoints are authenticated by
/// M-10's bearer-session middleware), the M-10 control-plane baseline (<c>_ControlPlane.sql</c>),
/// the M-16 journey baseline (<c>001_customer_journey_baseline.sql</c> — for binding-usage tests),
/// and the M-06 baseline (<c>KpiManagement_Baseline.sql</c> — 4 tables + 8 seed KPIs). All flow to
/// this project's output <c>Migrations/</c> folder transitively (M-10/M-16 via the host project
/// reference, M-06 via the module reference). <c>event_log</c> is <c>CREATE TABLE IF NOT EXISTS</c>
/// in every baseline, so they coexist.</para>
///
/// <para>The M-11 <c>TenantAdministration_OrganizationSettings.sql</c> is intentionally NOT applied
/// — the M-11 module is deferred; <see cref="ApplyMigrationAsync"/> no-ops on the missing file, so
/// Organization-settings tests are simply absent until M-11 lands.</para>
/// </summary>
public sealed class KpiManagementApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
#pragma warning disable CS0618 // PostgreSqlBuilder() ctor deprecated upstream; still functional in Testcontainers 4.x.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("nabadat_tenant")
        .WithUsername("nabadat")
        .WithPassword("nabadat")
        .Build();
#pragma warning restore CS0618

    // Fixed 256-bit AES key for the on-prem MFA secret encryption used in tests.
    private static readonly string MfaEncryptionKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray());

    /// <summary>Connection string for the running Testcontainers PostgreSQL instance.</summary>
    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await ApplyMigrationAsync("_Baseline.sql");                       // M-10 (auth tables + event_log)
        await ApplyMigrationAsync("_ControlPlane.sql");                   // M-10 control-plane
        await ApplyMigrationAsync("001_customer_journey_baseline.sql");   // M-16 (binding-usage joins)
        await ApplyMigrationAsync("KpiManagement_Baseline.sql");          // M-06 (4 tables + 8 seeds)
        await ApplyMigrationAsync("KpiManagement_OrganizationSettings.sql"); // M-06 US-6 (organization_settings + seeded default row)
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
                // On-prem single-tenant mode → one schema; LocalAesEncryptionService with a
                // config-supplied key so seeded MFA users can log in.
                ["ENABLE_MULTI_TENANT"] = "false",
                ["MfaEncryptionKey"] = MfaEncryptionKeyBase64,
            });
        });
    }

    /// <summary>Applies a named raw-SQL migration from the output <c>Migrations/</c> folder; a missing file is a no-op.</summary>
    private async Task ApplyMigrationAsync(string fileName)
    {
        var migrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations", fileName);
        if (!File.Exists(migrationPath))
        {
            return;
        }

        var sql = await File.ReadAllTextAsync(migrationPath);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    // ── Authenticated client ───────────────────────────────────────────────────

    /// <summary>
    /// Seeds an MFA-enrolled active user with the given persona, drives login → MFA verify, and
    /// returns an <see cref="HttpClient"/> whose Authorization header carries the bearer session
    /// token. Personas: P-01 (CX Program Manager), P-02 (CX Analyst), P-06, P-07.
    /// </summary>
    public async Task<HttpClient> SignedInClientAsync(string persona = "P-01") =>
        (await SignedInWithActorAsync(persona)).Client;

    /// <summary>As <see cref="SignedInClientAsync"/> but also returns the seeded actor (for audit assertions by actor_id).</summary>
    public async Task<(HttpClient Client, SeededUser Actor)> SignedInWithActorAsync(string persona = "P-01")
    {
        var actor = await SeedEnrolledUserAsync(persona);
        var client = CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = actor.Username, password = actor.Password });
        login.EnsureSuccessStatusCode();
        var challengeId = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("challengeId").GetString();

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify",
            new { challengeId, totpCode = ComputeTotp(actor.Base32Secret!) });
        verify.EnsureSuccessStatusCode();
        var token = (await verify.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("sessionToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, actor);
    }

    /// <summary>Seeds an MFA-enrolled, active tenant user. The returned secret lets the test compute live TOTP codes.</summary>
    public async Task<SeededUser> SeedEnrolledUserAsync(string persona = "P-01", string password = "ValidP@ss1")
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<ITenantUserService>();
        var permissions = sp.GetRequiredService<IPermissionModuleAssignmentService>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var totp = sp.GetRequiredService<ITotpService>();
        var encryption = sp.GetRequiredService<IMfaSecretEncryptionService>();
        var clock = sp.GetRequiredService<TimeProvider>();

        var secret = totp.GenerateSecret();
        var encrypted = await encryption.EncryptAsync(secret);
        var now = clock.GetUtcNow();

        var user = new TenantUser
        {
            UserId = Guid.NewGuid(),
            Username = UniqueEmail(),
            PasswordHash = hasher.Hash(password),
            IsMfaEnrolled = true,
            MfaSecretEncrypted = encrypted.Cipher,
            MfaSecretKeyRef = encrypted.KeyRef,
            Persona = persona,
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await users.AddAsync(user);

        // Grant the module modes the persona's snapshot must carry for the M-06 [RequirePermission]
        // gates (the snapshot is built from these assignments at login; the gate is default-deny).
        //   KpiConfiguration  — KPI catalogue/config (P-01 Manage; P-02/P-06 View; else none).
        //   TenantConfiguration — Organization settings, US-6 / FR-052 (P-01 + P-07 edit; else none).
        var assignments = new List<PermissionModuleAssignment>();
        AddAssignment(assignments, user.UserId, "KpiConfiguration", KpiModuleModesFor(persona), now);
        AddAssignment(assignments, user.UserId, "TenantConfiguration", TenantConfigModesFor(persona), now);
        if (assignments.Count > 0)
        {
            await permissions.ReplaceAssignmentsAsync(user.UserId, assignments);
        }

        return new SeededUser(user.UserId, user.Username, password, secret);
    }

    private static void AddAssignment(
        List<PermissionModuleAssignment> assignments,
        Guid userId,
        string moduleId,
        IReadOnlyList<string>? modes,
        DateTimeOffset now)
    {
        if (modes is null)
        {
            return;
        }

        assignments.Add(new PermissionModuleAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = userId,
            ModuleId = moduleId,
            AllowedModes = modes,
            AssignedBy = userId,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    /// <summary>
    /// The TenantConfiguration coarse modes a persona receives in the fixture (Organization settings,
    /// US-6 / FR-052): P-01 and P-07 may edit (View+Manage), every other persona gets nothing.
    /// </summary>
    private static IReadOnlyList<string>? TenantConfigModesFor(string persona) => persona switch
    {
        "P-01" => ["View", "Manage", "Full"],
        "P-07" => ["View", "Manage"],
        _ => null,
    };

    /// <summary>
    /// The KpiConfiguration coarse modes a persona receives in the fixture, mirroring the
    /// authorization matrix: P-01 manages (View+Manage+Full), P-02 Analyst and P-06 Executive view
    /// only, every other persona (incl. P-07 non-CX) gets no grant (null → reads and writes 403).
    /// </summary>
    private static IReadOnlyList<string>? KpiModuleModesFor(string persona) => persona switch
    {
        "P-01" => ["View", "Manage", "Full"],
        "P-02" or "P-06" => ["View"],
        _ => null,
    };

    // ── M-06 seeding helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a minimal active custom KPI (definition + ascending threshold) directly via SQL and
    /// returns its id. Short Name is case-insensitively unique, so callers pass a unique value.
    /// </summary>
    public async Task<Guid> SeedCustomKpiAsync(
        string shortName,
        string fullName,
        string scale = "Scale1_5",
        string calculationMethod = "WeightedAverage",
        decimal target = 80,
        Guid? actorId = null)
    {
        var id = Guid.NewGuid();
        var actor = actorId ?? Guid.Empty;
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using (var insertDef = new NpgsqlCommand(
            """
            INSERT INTO kpi_definitions
                (id, short_name, full_name, kpi_type, is_composite, calculation_method, scale,
                 representation_style, target, is_active, show_on_dashboard, created_by, updated_by)
            VALUES (@id, @sn, @fn, 'Custom', false, @cm, @scale, 'Number', @target, true, false, @actor, @actor)
            """, connection))
        {
            insertDef.Parameters.AddWithValue("id", id);
            insertDef.Parameters.AddWithValue("sn", shortName);
            insertDef.Parameters.AddWithValue("fn", fullName);
            insertDef.Parameters.AddWithValue("cm", calculationMethod);
            insertDef.Parameters.AddWithValue("scale", scale);
            insertDef.Parameters.AddWithValue("target", target);
            insertDef.Parameters.AddWithValue("actor", actor);
            await insertDef.ExecuteNonQueryAsync();
        }

        await using (var insertThreshold = new NpgsqlCommand(
            "INSERT INTO kpi_thresholds (kpi_id, lower_bound, x, y, upper_bound) VALUES (@id, 0, 20, 70, 100)",
            connection))
        {
            insertThreshold.Parameters.AddWithValue("id", id);
            await insertThreshold.ExecuteNonQueryAsync();
        }

        return id;
    }

    /// <summary>
    /// Flips a KPI's <c>is_active</c> by Short Name (case-insensitive). Used to exercise the
    /// "present regardless of status" rule (BR-1.1) without a deactivation endpoint; callers that
    /// touch a shared seeded standard MUST restore it in a <c>finally</c> so the fixture stays clean.
    /// </summary>
    public async Task SetKpiActiveByShortNameAsync(string shortName, bool active)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE kpi_definitions SET is_active = @a, updated_at = now() WHERE LOWER(short_name) = LOWER(@sn)",
            connection);
        command.Parameters.AddWithValue("a", active);
        command.Parameters.AddWithValue("sn", shortName);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Returns a KPI's id by Short Name (case-insensitive), or null if absent — e.g. to resolve a seeded standard KPI.</summary>
    public async Task<Guid?> GetKpiIdByShortNameAsync(string shortName)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id FROM kpi_definitions WHERE LOWER(short_name) = LOWER(@sn)", connection);
        command.Parameters.AddWithValue("sn", shortName);
        var result = await command.ExecuteScalarAsync();
        return result is Guid g ? g : null;
    }

    /// <summary>
    /// Binds a KPI to a touchpoint by inserting an M-16 <c>kpi_bindings</c> row carrying the logical
    /// <c>kpi_id</c> reference (T020), so <c>IJourneyBindingQuery</c> counts it. Assumes the
    /// touchpoint/stage/journey rows already exist (seed them with M-16's helpers as needed).
    /// </summary>
    public async Task BindKpiToTouchpointAsync(Guid touchpointId, Guid kpiId, string kpiType, decimal weight = 100)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO kpi_bindings
                (kpi_binding_id, touchpoint_id, kpi_type, is_platform_standard, kpi_id, weight, created_at, updated_at)
            VALUES (@bid, @tid, @kt, true, @kid, @w, now(), now())
            """, connection);
        command.Parameters.AddWithValue("bid", Guid.NewGuid());
        command.Parameters.AddWithValue("tid", touchpointId);
        command.Parameters.AddWithValue("kt", kpiType);
        command.Parameters.AddWithValue("kid", kpiId);
        command.Parameters.AddWithValue("w", weight);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Seeds a non-archived M-16 journey → stage → touchpoint chain and binds <paramref name="kpiId"/>
    /// to the touchpoint, so <c>IJourneyBindingQuery</c> reports the KPI as used by one touchpoint in
    /// one journey (exercises FR-017 / FR-026). Returns the new touchpoint id. The journey name is
    /// made unique to satisfy the case-insensitive non-archived name index.
    /// </summary>
    public async Task<Guid> SeedBoundTouchpointAsync(Guid kpiId, string kpiType = "custom", string journeyStatus = "Active")
    {
        var journeyId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var touchpointId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using (var insertJourney = new NpgsqlCommand(
            """
            INSERT INTO journeys (journey_id, name, journey_type, status, created_by, created_at, updated_at)
            VALUES (@j, @name, 'Transactional', @status, @actor, now(), now())
            """, connection))
        {
            insertJourney.Parameters.AddWithValue("j", journeyId);
            insertJourney.Parameters.AddWithValue("name", $"Bound journey {journeyId:N}");
            insertJourney.Parameters.AddWithValue("status", journeyStatus);
            insertJourney.Parameters.AddWithValue("actor", Guid.Empty);
            await insertJourney.ExecuteNonQueryAsync();
        }

        await using (var insertStage = new NpgsqlCommand(
            """
            INSERT INTO stages (stage_id, journey_id, sequence_number, name, created_at, updated_at)
            VALUES (@s, @j, 1, 'Stage', now(), now())
            """, connection))
        {
            insertStage.Parameters.AddWithValue("s", stageId);
            insertStage.Parameters.AddWithValue("j", journeyId);
            await insertStage.ExecuteNonQueryAsync();
        }

        await using (var insertTouchpoint = new NpgsqlCommand(
            """
            INSERT INTO touchpoints (touchpoint_id, stage_id, name, created_at, updated_at)
            VALUES (@t, @s, 'Touchpoint', now(), now())
            """, connection))
        {
            insertTouchpoint.Parameters.AddWithValue("t", touchpointId);
            insertTouchpoint.Parameters.AddWithValue("s", stageId);
            await insertTouchpoint.ExecuteNonQueryAsync();
        }

        await BindKpiToTouchpointAsync(touchpointId, kpiId, kpiType);
        return touchpointId;
    }

    /// <summary>Counts audit events of a given type for a specific actor (asserts data-model §8 event emission).</summary>
    public async Task<int> CountEventsAsync(Guid actorId, string eventType)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM event_log WHERE actor_id = @a AND event_type = @t", connection);
        command.Parameters.AddWithValue("a", actorId);
        command.Parameters.AddWithValue("t", eventType);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>Returns the most recent <c>new_value</c> jsonb (as text) for an actor's events of a type; null if none.</summary>
    public async Task<string?> LatestEventNewValueAsync(Guid actorId, string eventType)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT new_value::text FROM event_log
            WHERE actor_id = @a AND event_type = @t
            ORDER BY occurred_at_utc DESC LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("a", actorId);
        command.Parameters.AddWithValue("t", eventType);
        return await command.ExecuteScalarAsync() as string;
    }

    /// <summary>
    /// Inserts a <c>cxi_weights</c> membership row directly (no audit event), so cascade tests can set
    /// up a composite without the PUT-weights endpoint's own <c>settings.changed</c> event polluting
    /// the deactivation event count.
    /// </summary>
    public async Task SeedCxiWeightAsync(Guid cxiKpiId, Guid memberKpiId, int weight)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO cxi_weights (cxi_kpi_id, member_kpi_id, weight) VALUES (@c, @m, @w)", connection);
        command.Parameters.AddWithValue("c", cxiKpiId);
        command.Parameters.AddWithValue("m", memberKpiId);
        command.Parameters.AddWithValue("w", (short)weight);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Returns the member KPI ids currently weighted under a CXI composite.</summary>
    public async Task<IReadOnlyList<Guid>> ListCxiWeightMembersAsync(Guid cxiKpiId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT member_kpi_id FROM cxi_weights WHERE cxi_kpi_id = @c", connection);
        command.Parameters.AddWithValue("c", cxiKpiId);
        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>Removes every <c>cxi_weights</c> row for a CXI — cascade-test cleanup to keep the shared composite tidy.</summary>
    public async Task ClearCxiWeightsAsync(Guid cxiKpiId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "DELETE FROM cxi_weights WHERE cxi_kpi_id = @c", connection);
        command.Parameters.AddWithValue("c", cxiKpiId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Resets the singleton <c>organization_settings</c> row to a known baseline (no logo) so each
    /// US-6 test starts from a deterministic state despite the shared container (no per-test rollback).
    /// Uses the system actor and writes no audit event.
    /// </summary>
    public async Task ResetOrganizationAsync(string name = "My Organization", string industry = "Services")
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE organization_settings
               SET name = @name, industry = @industry, logo_blob_ref = NULL,
                   updated_at = now(), updated_by = '00000000-0000-0000-0000-000000000000'
            """, connection);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("industry", industry);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Resets the singleton <c>scoring_configs</c> row to the seeded tenant defaults (α=0.500, MOT=1.5,
    /// n_floor=100, flag_percentile=25, rolling_window_days=30) so each US-4 ScoringConfig test starts
    /// from a deterministic baseline despite the shared container (no per-test rollback). System actor,
    /// no audit event.
    /// </summary>
    public async Task ResetScoringConfigAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE scoring_configs
               SET alpha = 0.500, mot_multiplier = 1.5, n_floor = 100, flag_percentile = 25,
                   rolling_window_days = 30, updated_at = now(),
                   updated_by = '00000000-0000-0000-0000-000000000000'
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Sets a KPI's <c>show_on_dashboard</c> flag directly (to verify the deactivation cascade forces it off).</summary>
    public async Task SetShowOnDashboardAsync(Guid kpiId, bool value)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE kpi_definitions SET show_on_dashboard = @v WHERE id = @id", connection);
        command.Parameters.AddWithValue("v", value);
        command.Parameters.AddWithValue("id", kpiId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Computes the current valid TOTP code for a Base32 secret (matches the module's OTP.NET).</summary>
    public static string ComputeTotp(string base32Secret) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";
}
