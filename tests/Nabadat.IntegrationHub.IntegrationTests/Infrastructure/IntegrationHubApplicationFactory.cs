using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.IntegrationHub.Infrastructure.ChannelDispatch;
using Nabadat.IntegrationHub.Infrastructure.UserManagementIntegration;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Npgsql;
using OtpNet;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nabadat.IntegrationHub.IntegrationTests.Infrastructure;

/// <summary>
/// Shared integration-test fixture for the M-13 Integration Hub module (T018 — the first backend story to
/// need it owns it; every later story reuses and extends the seeding helpers, per CLAUDE.md Unit Test
/// Policy rule 12). Mirrors the M-01 <c>SurveyBuilderApplicationFactory</c> reference.
///
/// <para>Boots a fresh Dockerised PostgreSQL via Testcontainers and applies, in order, the M-10 tenant
/// baseline (<c>_Baseline.sql</c> — M-13's console endpoints are authenticated by M-10's bearer-session
/// middleware), the M-10 control-plane baseline (<c>_ControlPlane.sql</c>), the M-01 baseline
/// (<c>SurveyBuilder_Baseline.sql</c> — SCN-03 makes a <b>real</b> cross-module call into M-01's
/// <c>ISurveyRenderService</c>, so its tables must exist), and the M-13 baseline
/// (<c>IntegrationHub_Baseline.sql</c> — the 8 owned tables, the monthly partitions for
/// <c>integration_request_logs</c>, and the 23 seeded built-in parameters). M-10/M-01 flow to this
/// project's output via the host reference; M-13 via the module reference. <c>event_log</c> is
/// <c>CREATE TABLE IF NOT EXISTS</c> in every baseline, so they coexist.</para>
///
/// <para>Docker must be running for this fixture to start (per-story checkpoint only, never a per-task
/// gate).</para>
///
/// <para><b>No transaction rollback:</b> like the other integration lanes, writes here are real rows in a
/// real database. Keep seeded names/channel ids unique per test.</para>
/// </summary>
public sealed class IntegrationHubApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
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

    /// <summary>Connection string for the running Testcontainers PostgreSQL instance.</summary>
    public string ConnectionString => _postgres.GetConnectionString();

    // ── M-02 / M-04 stub adapters, for asserting the downstream hand-off ──────────
    // Registered as singletons by AddIntegrationHubModule specifically so their recorded calls survive
    // the request scope and can be read here after an HTTP round-trip. With no real M-02/M-04 in the
    // repo, these recordings are the only available proof the pipeline handed off correctly.

    /// <summary>The M-02 survey-resolution stub — always resolves <c>null</c>; inspect its recorded calls.</summary>
    public NullSurveyResolutionReader SurveyResolution =>
        Services.GetRequiredService<NullSurveyResolutionReader>();

    /// <summary>The M-02 dispatch stub (SCN-01/02). Assert <c>Calls</c> to prove exactly one hand-off — e.g. BR-18 idempotency.</summary>
    public NullSurveyDispatchGateway SurveyDispatch =>
        Services.GetRequiredService<NullSurveyDispatchGateway>();

    /// <summary>The M-04 response-ingestion stub (SCN-05). Assert <c>Calls</c> for the forwarded payload.</summary>
    public NullResponseIngestionGateway ResponseIngestion =>
        Services.GetRequiredService<NullResponseIngestionGateway>();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await ApplyMigrationAsync("_Baseline.sql");                  // M-10 (auth tables + event_log)
        await ApplyMigrationAsync("_ControlPlane.sql");              // M-10 control-plane
        await ApplyMigrationAsync("SurveyBuilder_Baseline.sql");     // M-01 (real SCN-03 call target)
        await ApplyMigrationAsync("IntegrationHub_Baseline.sql");    // M-13 (8 owned tables + 23 built-ins)
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
                ["ENABLE_MULTI_TENANT"] = "false",
                ["MfaEncryptionKey"] = MfaEncryptionKeyBase64,
                // Any absolute URI works: the handler below routes every request to the in-memory server
                // regardless of host. It just has to be non-empty, or DataScopeHttpClient refuses to send.
                ["UserManagement:BaseUrl"] = "http://user-management.test",
            });
        });

        // Point M-13's REAL outbound M-10 call (US2/T059) at this same in-memory server, so
        // POST /api/v1/authorization/scope/parameters genuinely executes M-10's M13ParameterContractAdapter —
        // validation, upsert and all — and the assertion can read the resulting
        // data_scope_parameter_definitions rows. Substituting a fake client here would prove only that M-13
        // called something, which is exactly what research.md §4.1 says NOT to settle for: M-10's side is real
        // and already built, so the cross-module contract is testable for real.
        //
        // Server.CreateHandler() is resolved lazily inside the factory lambda: the HttpClient is first built
        // during a request, by which time the host (and therefore Server) exists.
        builder.ConfigureTestServices(services =>
            services.AddHttpClient(DataScopeHttpClient.ClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler()));
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
    /// Seeds an MFA-enrolled user with the given persona, drives the real login → MFA-verify flow, and
    /// returns a bearer-authenticated client. Default persona is <c>P-07</c> (Tenant IT Administrator) —
    /// M-13's integration/credential/request-log owner. Pass <c>P-01</c> for the CX Manager's
    /// channel/parameter/mapping surfaces.
    /// </summary>
    public async Task<HttpClient> SignedInClientAsync(string persona = "P-07") =>
        (await SignedInWithActorAsync(persona)).Client;

    /// <summary>As <see cref="SignedInClientAsync"/> but also returns the seeded actor, for audit assertions by <c>actor_id</c>.</summary>
    public async Task<(HttpClient Client, SeededUser Actor)> SignedInWithActorAsync(string persona = "P-07")
    {
        var actor = await SeedEnrolledUserAsync(persona);
        var client = CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = actor.Username, password = actor.Password });
        login.EnsureSuccessStatusCode();
        var challengeId = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("challengeId").GetString();

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify", new { challengeId, totpCode = ComputeTotp(actor.Base32Secret) });
        verify.EnsureSuccessStatusCode();
        var token = (await verify.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("sessionToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, actor);
    }

    /// <summary>Seeds an MFA-enrolled, active tenant user with no module grants (grant them per test via <see cref="GrantModuleAsync"/>).</summary>
    public async Task<SeededUser> SeedEnrolledUserAsync(string persona = "P-07", string password = "ValidP@ss1")
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<ITenantUserService>();
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
        return new SeededUser(user.UserId, user.Username, password, secret);
    }

    /// <summary>
    /// Grants a user the given coarse modes on a permission module (replaces the user's assignment set).
    /// M-13's permission-module id and required modes are defined with its controllers (US9's Permissions
    /// Matrix), so tests pass whatever the endpoint under test gates on — nothing is hardcoded here.
    /// </summary>
    public async Task GrantModuleAsync(Guid userId, string moduleId, IReadOnlyList<string> modes)
    {
        using var scope = Services.CreateScope();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionModuleAssignmentService>();
        var now = DateTimeOffset.UtcNow;
        await permissions.ReplaceAssignmentsAsync(userId, new List<PermissionModuleAssignment>
        {
            new()
            {
                AssignmentId = Guid.NewGuid(),
                UserId = userId,
                ModuleId = moduleId,
                AllowedModes = modes,
                AssignedBy = userId,
                CreatedAt = now,
                UpdatedAt = now,
            },
        });
    }

    // ── M-13 seeding helpers (raw SQL — arranging state no endpoint exists for yet) ───────

    /// <summary>
    /// Inserts a service channel and returns its id. Pass <paramref name="channelIdLocked"/> to arrange the
    /// post-lock state BR-05 forbids editing — the API has no way to set the lock directly, since it is set
    /// by the channel's first 2xx request.
    /// </summary>
    public async Task<Guid> SeedServiceChannelAsync(
        string nameEn = "Seed channel",
        string nameAr = "قناة تجريبية",
        string? channelId = null,
        bool active = true,
        bool channelIdLocked = false)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO service_channels
                (id, name_en, name_ar, channel_id, active, channel_id_locked, created_at, updated_at)
            VALUES (@id, @nameEn, @nameAr, @channelId, @active, @locked, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("nameEn", nameEn);
        command.Parameters.AddWithValue("nameAr", nameAr);
        command.Parameters.AddWithValue("channelId", channelId ?? UniqueChannelId());
        command.Parameters.AddWithValue("active", active);
        command.Parameters.AddWithValue("locked", channelIdLocked);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Inserts a custom parameter and returns its id. <paramref name="dataType"/> takes the snake_case wire
    /// value (<c>text</c>, <c>list</c>, <c>date_time</c>, …) — the same literals the baseline's
    /// <c>ck_parameters_data_type</c> CHECK allows. <paramref name="mappingSupport"/> must obey BR-27, which
    /// the CHECK also enforces: <c>list</c> requires <c>true</c>; anything outside text/boolean/url requires
    /// <c>false</c>.
    /// </summary>
    public async Task<Guid> SeedCustomParameterAsync(
        string nameEn = "Seed parameter",
        string nameAr = "معيار تجريبي",
        string? apiField = null,
        string dataType = "text",
        bool enabled = true,
        bool mappingSupport = false,
        bool apiFieldLocked = false)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO parameters
                (id, name_en, name_ar, api_field, api_field_locked, data_type, origin, enabled,
                 mapping_support, created_at, updated_at)
            VALUES (@id, @nameEn, @nameAr, @apiField, @locked, @dataType, 'custom', @enabled,
                    @mappingSupport, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("nameEn", nameEn);
        command.Parameters.AddWithValue("nameAr", nameAr);
        command.Parameters.AddWithValue("apiField", apiField ?? UniqueApiField());
        command.Parameters.AddWithValue("locked", apiFieldLocked);
        command.Parameters.AddWithValue("dataType", dataType);
        command.Parameters.AddWithValue("enabled", enabled);
        command.Parameters.AddWithValue("mappingSupport", mappingSupport);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Resolves a parameter's id by its API field — the way to reach a seeded built-in (e.g. <c>mobile</c>).</summary>
    public async Task<Guid> GetParameterIdByApiFieldAsync(string apiField)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM parameters WHERE api_field = @f", connection);
        command.Parameters.AddWithValue("f", apiField);
        var result = await command.ExecuteScalarAsync();
        return result is Guid id
            ? id
            : throw new InvalidOperationException($"No parameter with api_field '{apiField}'.");
    }

    /// <summary>Counts parameters of an origin (<c>built_in</c> / <c>custom</c>) — asserts the BR-23 seed of 23 enabled built-ins.</summary>
    public async Task<int> CountParametersAsync(string origin)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM parameters WHERE origin = @o", connection);
        command.Parameters.AddWithValue("o", origin);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// Inserts a source-value → bilingual-display mapping row and returns its id. Two US2 behaviours depend on
    /// these rows even though mappings themselves are US6's story: a parameter only qualifies for the M-10
    /// data-scope push once it has an enumerable value set (research.md §4.1 — M-10 rejects an empty
    /// <c>allowedValues</c>), and SCR-05's "Mapped" link is driven by the count.
    /// </summary>
    public async Task<Guid> SeedParameterMappingAsync(
        Guid parameterId,
        string sourceValue,
        string displayEn,
        string displayAr = "قيمة")
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO parameter_mappings
                (id, parameter_id, source_value, display_en, display_ar, status, created_at, updated_at)
            VALUES (@id, @parameter, @source, @en, @ar, 'active', now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("parameter", parameterId);
        command.Parameters.AddWithValue("source", sourceValue);
        command.Parameters.AddWithValue("en", displayEn);
        command.Parameters.AddWithValue("ar", displayAr);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Reads back an M-10 data-scope parameter definition by name — the proof that M-13's <b>real</b> outbound
    /// call reached <c>M13ParameterContractAdapter</c> and was persisted. Returns <c>null</c> when M-10 holds no
    /// definition of that name.
    /// </summary>
    public async Task<(string Label, string[] AllowedValues, string SourceModule)?> GetDataScopeDefinitionAsync(
        string parameterName)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT label, allowed_values, source_module
            FROM data_scope_parameter_definitions
            WHERE parameter_name = @n
            """, connection);
        command.Parameters.AddWithValue("n", parameterName);
        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync()
            ? (reader.GetString(0), reader.GetFieldValue<string[]>(1), reader.GetString(2))
            : null;
    }

    /// <summary>Inserts a channel-contract row. <c>required</c> is only valid while <c>supported</c> (FR-S4-04, DB CHECK).</summary>
    public async Task SeedChannelParameterAssignmentAsync(
        Guid serviceChannelId, Guid parameterId, bool supported = true, bool required = false)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO channel_parameter_assignments (service_channel_id, parameter_id, supported, required)
            VALUES (@channel, @parameter, @supported, @required)
            ON CONFLICT (service_channel_id, parameter_id)
            DO UPDATE SET supported = EXCLUDED.supported, required = EXCLUDED.required
            """, connection);
        command.Parameters.AddWithValue("channel", serviceChannelId);
        command.Parameters.AddWithValue("parameter", parameterId);
        command.Parameters.AddWithValue("supported", supported);
        command.Parameters.AddWithValue("required", required);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Inserts an integration and returns its id. <paramref name="scenario"/> takes the snake_case wire
    /// value (<c>dispatch</c>, <c>redirect_link</c>, <c>json_render</c>, <c>iframe_embed</c>,
    /// <c>response_ingestion</c>).
    /// </summary>
    public async Task<Guid> SeedIntegrationAsync(
        Guid serviceChannelId,
        string name = "Seed integration",
        string scenario = "dispatch",
        bool active = true,
        Guid? createdBy = null)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO integrations
                (id, name, service_channel_id, scenario, active, created_by, created_at, updated_at)
            VALUES (@id, @name, @channel, @scenario, @active, @createdBy, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("channel", serviceChannelId);
        command.Parameters.AddWithValue("scenario", scenario);
        command.Parameters.AddWithValue("active", active);
        command.Parameters.AddWithValue("createdBy", createdBy ?? Guid.Empty);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Inserts a credential and returns its id. The stored <paramref name="secretHash"/> is a stand-in — the
    /// real generation path never persists plaintext (BR-16), so a test that needs a <i>usable</i> key must
    /// go through the generation endpoint instead of this seed.
    /// </summary>
    public async Task<Guid> SeedCredentialAsync(
        Guid integrationId,
        string mechanism = "api_key",
        string labelOrClientName = "Seed key",
        string secretHash = "seeded-hash",
        string status = "active")
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO credentials
                (id, integration_id, mechanism, label_or_client_name, secret_hash, status,
                 generated_at, revoked_at)
            VALUES (@id, @integration, @mechanism, @label, @hash, @status, now(),
                    CASE WHEN @status = 'revoked' THEN now() ELSE NULL END)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("integration", integrationId);
        command.Parameters.AddWithValue("mechanism", mechanism);
        command.Parameters.AddWithValue("label", labelOrClientName);
        command.Parameters.AddWithValue("hash", secretHash);
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Inserts a request-log row and returns its id — arranges the traffic SCR-01's stat tiles and SCR-08's
    /// filters read. <paramref name="timestamp"/> defaults to now; pass an older instant to exercise the
    /// window filters (it must land inside a provisioned monthly partition, or the DEFAULT one).
    /// </summary>
    public async Task<Guid> SeedRequestLogAsync(
        Guid? integrationId,
        int httpStatus = 202,
        string resultCode = "202",
        string method = "POST",
        string path = "/v1/survey-requests/SEED",
        string? scenario = "dispatch",
        int latencyMs = 42,
        string parametersReceived = "{}",
        string responseReturned = "{}",
        string? credentialLabel = null,
        string? rejectionStage = null,
        DateTimeOffset? timestamp = null)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO integration_request_logs
                (id, integration_id, timestamp, method, path, scenario, parameters_received,
                 response_returned, http_status, result_code, latency_ms, credential_label, rejection_stage)
            VALUES (@id, @integration, @timestamp, @method, @path, @scenario, @parameters::jsonb,
                    @response::jsonb, @status, @resultCode, @latency, @label, @stage)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("integration", (object?)integrationId ?? DBNull.Value);
        command.Parameters.AddWithValue("timestamp", timestamp ?? DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("method", method);
        command.Parameters.AddWithValue("path", path);
        command.Parameters.AddWithValue("scenario", (object?)scenario ?? DBNull.Value);
        command.Parameters.AddWithValue("parameters", parametersReceived);
        command.Parameters.AddWithValue("response", responseReturned);
        command.Parameters.AddWithValue("status", httpStatus);
        command.Parameters.AddWithValue("resultCode", resultCode);
        command.Parameters.AddWithValue("latency", latencyMs);
        command.Parameters.AddWithValue("label", (object?)credentialLabel ?? DBNull.Value);
        command.Parameters.AddWithValue("stage", (object?)rejectionStage ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Counts rows in a table matching an optional <c>WHERE</c> fragment — a general assertion escape hatch.</summary>
    public async Task<int> CountRowsAsync(string table, string? whereClause = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var sql = whereClause is null
            ? $"SELECT count(*) FROM {table}"
            : $"SELECT count(*) FROM {table} WHERE {whereClause}";
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>True if a row with the id still exists in the named table.</summary>
    public async Task<bool> RowExistsAsync(string table, Guid id)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT EXISTS (SELECT 1 FROM {table} WHERE id = @id)", connection);
        command.Parameters.AddWithValue("id", id);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Counts M-17 audit events of a given type for a specific actor (asserts the audit emission every M-13 write owes).</summary>
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

    /// <summary>Computes the current valid TOTP code for a Base32 secret (matches the module's OTP.NET).</summary>
    public static string ComputeTotp(string base32Secret) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>A unique channel id inside VR-F04's <c>[A-Za-z0-9-]</c>, ≤19-char envelope.</summary>
    private static string UniqueChannelId() => $"CH-{Guid.NewGuid():N}"[..19];

    /// <summary>A unique snake_case API field, per BR-11's format.</summary>
    private static string UniqueApiField() => $"p_{Guid.NewGuid():N}"[..18];
}
