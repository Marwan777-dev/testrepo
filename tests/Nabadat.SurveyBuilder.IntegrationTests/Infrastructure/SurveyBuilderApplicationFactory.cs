using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Npgsql;
using OtpNet;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// Shared integration-test fixture for the M-01 Survey &amp; Form Builder module (T030 — first M-01
/// integration story owns it; later stories reuse + extend the seeding helpers, per CLAUDE.md Unit
/// Test Policy rule 12). Mirrors the M-06 <c>KpiManagementApplicationFactory</c> reference.
///
/// <para>Boots a fresh Dockerised PostgreSQL via Testcontainers and applies, in order, the M-10
/// tenant baseline (<c>_Baseline.sql</c> — M-01 endpoints are authenticated by M-10's bearer-session
/// middleware), the M-10 control-plane baseline (<c>_ControlPlane.sql</c>), the M-16 journey baseline
/// (<c>001_customer_journey_baseline.sql</c> — for journey/stage/touchpoint binding validation), the
/// M-06 baseline (<c>KpiManagement_Baseline.sql</c> — KPI catalogue validation), and the M-01
/// baseline (<c>SurveyBuilder_Baseline.sql</c> — the 9 owned tables). M-10/M-16/M-06 flow to output
/// via the host reference; M-01 via the module reference. <c>event_log</c> is
/// <c>CREATE TABLE IF NOT EXISTS</c> in every baseline, so they coexist.</para>
///
/// <para>Docker must be running for this fixture to start (US1+ per-story checkpoint, never a
/// per-task gate).</para>
/// </summary>
public sealed class SurveyBuilderApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
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

    /// <summary>
    /// Records the M-09 reviewer broadcasts fired during a test (the module default drops them). Assert
    /// against it after a submit; filter by the survey id in the deep link. See <see cref="ConfigureWebHost"/>.
    /// </summary>
    public CapturingNotificationDispatcher Notifications { get; } = new();

    /// <summary>
    /// Per-user grant control for the tests (denies by default). Call <see cref="StubPermissionChecker.AllowGrant"/>
    /// to exercise the FR-15.5 self-publish path. See <see cref="ConfigureWebHost"/>.
    /// </summary>
    public StubPermissionChecker Permissions { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await ApplyMigrationAsync("_Baseline.sql");                     // M-10 (auth tables + event_log)
        await ApplyMigrationAsync("_ControlPlane.sql");                 // M-10 control-plane
        await ApplyMigrationAsync("001_customer_journey_baseline.sql"); // M-16 (journey binding validation)
        await ApplyMigrationAsync("KpiManagement_Baseline.sql");        // M-06 (KPI catalogue validation)
        await ApplyMigrationAsync("SurveyBuilder_Baseline.sql");        // M-01 (9 owned tables)
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
            });
        });

        // Swap the M-01 cross-module port placeholders (NoOp / deny-all) for observable test doubles so
        // integration tests can assert audit emission, the M-09 reviewer broadcast, and the FR-15.5 grant.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventLogWriter>();
            services.AddScoped<IEventLogWriter, DbEventLogWriter>();
            services.RemoveAll<INotificationDispatcher>();
            services.AddSingleton<INotificationDispatcher>(Notifications);
            services.RemoveAll<IPermissionChecker>();
            services.AddSingleton<IPermissionChecker>(Permissions);
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

    /// <summary>Seeds an MFA-enrolled user with the given persona, drives login → MFA verify, and returns a bearer client.</summary>
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
            "/api/v1/auth/mfa/verify", new { challengeId, totpCode = ComputeTotp(actor.Base32Secret) });
        verify.EnsureSuccessStatusCode();
        var token = (await verify.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("sessionToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, actor);
    }

    /// <summary>Seeds an MFA-enrolled, active tenant user with no module grants (grant them per test via <see cref="GrantModuleAsync"/>).</summary>
    public async Task<SeededUser> SeedEnrolledUserAsync(string persona = "P-01", string password = "ValidP@ss1")
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
    /// M-01's permission-module id + required modes are defined with its controllers (US1); tests pass
    /// whatever the endpoint under test gates on, so no M-01 module is hardcoded in the fixture.
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

    // ── M-01 seeding helpers (raw SQL — the tables no UI/endpoint has created yet) ───────────────

    /// <summary>Inserts a Draft survey directly and returns its id. Non-defaulted columns only; the rest take their DDL defaults.</summary>
    public Task<Guid> SeedDraftSurveyAsync(string nameEn = "Seed survey", Guid? ownerId = null) =>
        SeedSurveyAsync(nameEn, "Draft", ownerId);

    /// <summary>Inserts an Active survey directly (bypasses the Publish gate — this is a seed, not the API) and returns its id.</summary>
    public Task<Guid> SeedActiveSurveyAsync(string nameEn = "Seed active survey", Guid? ownerId = null) =>
        SeedSurveyAsync(nameEn, "Active", ownerId);

    private async Task<Guid> SeedSurveyAsync(string nameEn, string status, Guid? ownerId)
    {
        var id = Guid.NewGuid();
        var owner = ownerId ?? Guid.Empty;
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO surveys
                (id, name_en, survey_type, status, owner_user_id,
                 created_at, created_by, updated_at, updated_by)
            VALUES (@id, @name, 'SeasonalRelational', @status, @owner,
                    now(), @owner, now(), @owner)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", nameEn);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("owner", owner);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Sets a seeded survey's <c>layout</c> (and optionally <c>shuffle</c>) directly — the F9 routing
    /// endpoints require <c>layout = 'question'</c>, which the plain survey seed does not set. Kept as a
    /// raw-SQL seed (not the API) so a test can arrange the pre-toggle state, including a shuffled survey
    /// whose shuffle the enable-routing path must turn off.
    /// </summary>
    public async Task SetSurveyLayoutAsync(Guid surveyId, string layout, bool shuffle = false)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE surveys SET layout = @layout, shuffle = @shuffle WHERE id = @id", connection);
        command.Parameters.AddWithValue("layout", layout);
        command.Parameters.AddWithValue("shuffle", shuffle);
        command.Parameters.AddWithValue("id", surveyId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Inserts a section under a survey and returns its id.</summary>
    public async Task<Guid> SeedSectionAsync(Guid surveyId, string name = "Section", int order = 0)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO sections (id, survey_id, name, "order", created_at, updated_at)
            VALUES (@id, @survey, @name, @order, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("survey", surveyId);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("order", order);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Inserts a question under a section and returns its id. Pass <paramref name="setId"/> to place
    /// it inside a Questions Set (a set member still carries the enclosing <c>section_id</c>).
    /// </summary>
    public async Task<Guid> SeedQuestionAsync(
        Guid surveyId, Guid sectionId, string type = "Scale", string subtype = "Stars",
        string text = "How was it?", int order = 0, Guid? setId = null)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO questions
                (id, survey_id, section_id, set_id, type, subtype, text, type_payload, "order", created_at, updated_at)
            VALUES (@id, @survey, @section, @set, @type, @subtype, @text, '{"$type":"scale","pointCount":5}'::jsonb, @order, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("survey", surveyId);
        command.Parameters.AddWithValue("section", sectionId);
        command.Parameters.AddWithValue("set", (object?)setId ?? DBNull.Value);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("subtype", subtype);
        command.Parameters.AddWithValue("text", text);
        command.Parameters.AddWithValue("order", order);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Inserts a Questions Set under a section and returns its id (F10, data-model §2.3).</summary>
    public async Task<Guid> SeedQuestionsSetAsync(
        Guid sectionId, string selectionMode = "random", int count = 0, int order = 0, string title = "Set")
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO questions_sets
                (id, section_id, title, selection_mode, count, "order", created_at, updated_at)
            VALUES (@id, @section, @title, @mode, @count, @order, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("section", sectionId);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("mode", selectionMode);
        command.Parameters.AddWithValue("count", count);
        command.Parameters.AddWithValue("order", order);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Inserts a routing override row (source answer → target) so a move-into-set can be shown to strip it (FR-9.5).</summary>
    public async Task SeedRoutingAsync(Guid surveyId, Guid sourceQuestionId, string answerKey = "1", Guid? targetQuestionId = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO routing_maps
                (id, survey_id, source_question_id, answer_key, target_question_id, created_at, updated_at)
            VALUES (@id, @survey, @source, @key, @target, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("survey", surveyId);
        command.Parameters.AddWithValue("source", sourceQuestionId);
        command.Parameters.AddWithValue("key", answerKey);
        command.Parameters.AddWithValue("target", (object?)targetQuestionId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Reads a question's current placement (<c>section_id</c>, <c>set_id</c>, <c>order</c>) — asserts a move persisted.</summary>
    public async Task<(Guid SectionId, Guid? SetId, int Order)> GetQuestionPlacementAsync(Guid questionId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """SELECT section_id, set_id, "order" FROM questions WHERE id = @id""", connection);
        command.Parameters.AddWithValue("id", questionId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException($"Question {questionId} not found.");
        }

        var setId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
        return (reader.GetGuid(0), setId, reader.GetInt32(2));
    }

    /// <summary>Counts routing rows referencing a question as source OR target (FR-9.5 strip assertion).</summary>
    public async Task<int> CountRoutingForQuestionAsync(Guid questionId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM routing_maps WHERE source_question_id = @id OR target_question_id = @id", connection);
        command.Parameters.AddWithValue("id", questionId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// Reads the raw persisted translation-key names for a (survey, locale) bundle straight from
    /// <c>survey_translations.keys</c>. Used to prove the FR-2.8 purge actually scrubbed storage on
    /// delete — a GET on the translations endpoint can't, because the source extractor stops emitting a
    /// deleted question's key regardless of whether the stored row was scrubbed. Returns an empty set
    /// when no bundle row exists for the locale.
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetTranslationKeyNamesAsync(Guid surveyId, string locale)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT keys::text FROM survey_translations WHERE survey_id = @survey AND locale = @locale", connection);
        command.Parameters.AddWithValue("survey", surveyId);
        command.Parameters.AddWithValue("locale", locale);
        var json = (string?)await command.ExecuteScalarAsync();
        if (string.IsNullOrEmpty(json))
        {
            return Array.Empty<string>();
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
    }

    /// <summary>Inserts a Customized template (created_by required, sectors empty per the DDL CHECK) and returns its id.</summary>
    public async Task<Guid> SeedTemplateAsync(string nameEn = "Seed template", Guid? createdBy = null)
    {
        var id = Guid.NewGuid();
        var actor = createdBy ?? Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO templates (id, class, name_en, created_by, created_at, updated_at)
            VALUES (@id, 'Customized', @name, @actor, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", nameEn);
        command.Parameters.AddWithValue("actor", actor);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Inserts a Customized template carrying <paramref name="tags"/> (F6 tag search) and returns its id.</summary>
    public async Task<Guid> SeedCustomizedTemplateWithTagsAsync(string nameEn, string[] tags, Guid? createdBy = null)
    {
        var id = Guid.NewGuid();
        var actor = createdBy ?? Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO templates (id, class, name_en, tags, created_by, created_at, updated_at)
            VALUES (@id, 'Customized', @name, @tags, @actor, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", nameEn);
        command.Parameters.AddWithValue("tags", tags);
        command.Parameters.AddWithValue("actor", actor);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Inserts a locked BuiltIn template (created_by/updated_by NULL, no tags, sectors set) and returns its id.</summary>
    public async Task<Guid> SeedBuiltInTemplateAsync(string nameEn = "Built-in template", string[]? sectors = null)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO templates (id, class, name_en, sectors, created_at, updated_at)
            VALUES (@id, 'BuiltIn', @name, @sectors, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", nameEn);
        command.Parameters.AddWithValue("sectors", sectors ?? Array.Empty<string>());
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Inserts a Draft survey bound to a journey (survey_type Transactional per BR-3.3) with the given
    /// <paramref name="themeMode"/>, and returns its id. Used by the Templates save/instantiate tests
    /// which must verify the journey binding + appearance carry through the snapshot (FR-7.4).
    /// </summary>
    public async Task<Guid> SeedJourneyBoundSurveyAsync(
        Guid journeyId, string nameEn = "Journey-bound survey", Guid? ownerId = null, string themeMode = "Inherited")
    {
        var id = Guid.NewGuid();
        var owner = ownerId ?? Guid.Empty;
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO surveys
                (id, name_en, survey_type, bound_journey_id, status, theme_mode, owner_user_id,
                 created_at, created_by, updated_at, updated_by)
            VALUES (@id, @name, 'Transactional', @journey, 'Draft', @themeMode, @owner,
                    now(), @owner, now(), @owner)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", nameEn);
        command.Parameters.AddWithValue("journey", journeyId);
        command.Parameters.AddWithValue("themeMode", themeMode);
        command.Parameters.AddWithValue("owner", owner);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Inserts a KPI question bound to a stage → touchpoint (FR-8.4) and returns its id.</summary>
    public async Task<Guid> SeedKpiQuestionAsync(
        Guid surveyId, Guid sectionId, string kpiCode, Guid stageId, Guid touchpointId,
        string text = "How satisfied were you?", int order = 0)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO questions
                (id, survey_id, section_id, type, subtype, text, kpi_code, bound_journey_on,
                 stage_id, touchpoint_id, type_payload, "order", created_at, updated_at)
            VALUES (@id, @survey, @section, 'KPI', 'None', @text, @kpi, true,
                    @stage, @touchpoint, '{"$type":"kpi"}'::jsonb, @order, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("survey", surveyId);
        command.Parameters.AddWithValue("section", sectionId);
        command.Parameters.AddWithValue("text", text);
        command.Parameters.AddWithValue("kpi", kpiCode);
        command.Parameters.AddWithValue("stage", stageId);
        command.Parameters.AddWithValue("touchpoint", touchpointId);
        command.Parameters.AddWithValue("order", order);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Inserts a Customize-mode theme row for a survey (appearance carry-through assertion).</summary>
    public async Task SeedThemeAsync(Guid surveyId, string primaryColor)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO themes (id, survey_id, primary_color, created_at, updated_at)
            VALUES (@id, @survey, @color, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("survey", surveyId);
        command.Parameters.AddWithValue("color", primaryColor);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Reads a survey's <c>bound_journey_id</c> (null when unbound).</summary>
    public async Task<Guid?> GetSurveyBoundJourneyAsync(Guid surveyId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT bound_journey_id FROM surveys WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", surveyId);
        var result = await command.ExecuteScalarAsync();
        return result is Guid g ? g : (Guid?)null;
    }

    /// <summary>Reads the (kpi_code, stage_id, touchpoint_id) binding of every KPI question in a survey.</summary>
    public async Task<IReadOnlyList<(string? KpiCode, Guid? StageId, Guid? TouchpointId)>> GetKpiBindingsForSurveyAsync(Guid surveyId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """SELECT kpi_code, stage_id, touchpoint_id FROM questions WHERE survey_id = @id AND type = 'KPI' ORDER BY "order" """, connection);
        command.Parameters.AddWithValue("id", surveyId);
        var bindings = new List<(string?, Guid?, Guid?)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var kpi = reader.IsDBNull(0) ? null : reader.GetString(0);
            var stage = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
            var touchpoint = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
            bindings.Add((kpi, stage, touchpoint));
        }

        return bindings;
    }

    /// <summary>Reads the ids of every question in a survey, ordered by <c>order</c> (used to remap translation keys after instantiate).</summary>
    public async Task<IReadOnlyList<Guid>> GetQuestionIdsForSurveyAsync(Guid surveyId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """SELECT id FROM questions WHERE survey_id = @id ORDER BY "order" """, connection);
        command.Parameters.AddWithValue("id", surveyId);
        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>Reads a survey's theme <c>primary_color</c> (null when it has no theme row).</summary>
    public async Task<string?> GetThemePrimaryColorAsync(Guid surveyId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT primary_color FROM themes WHERE survey_id = @id", connection);
        command.Parameters.AddWithValue("id", surveyId);
        var result = await command.ExecuteScalarAsync();
        return result as string;
    }

    /// <summary>True if the template row still exists (BR-7.1 delete assertions).</summary>
    public Task<bool> TemplateExistsAsync(Guid id) => RowExistsAsync("templates", id);

    /// <summary>True if the survey row still exists (BR-7.1 no-cascade-to-instantiated assertions).</summary>
    public Task<bool> SurveyExistsAsync(Guid id) => RowExistsAsync("surveys", id);

    /// <summary>
    /// Seeding a response is NOT supported by this fixture: responses live in M-04's
    /// (<c>Nabadat.ResponseCollection</c>) tables, which do not exist in this repo yet — the M-04
    /// dependency is tracked in coordination-log.md C-01 / TODO-M01-001. Destructive Return-to-Draft
    /// tests that need pre-existing responses are blocked on M-04 shipping <c>IResponsePurgeService</c>.
    /// </summary>
    public Task SeedResponseAsync(Guid surveyId) =>
        throw new NotSupportedException(
            "SeedResponse is unavailable until M-04 (Nabadat.ResponseCollection) ships its responses " +
            "schema + IResponsePurgeService — see coordination-log.md C-01 / TODO-M01-001.");

    /// <summary>True if a row with the id still exists in the named table (cascade-delete assertions).</summary>
    public async Task<bool> RowExistsAsync(string table, Guid id)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT EXISTS (SELECT 1 FROM {table} WHERE id = @id)", connection);
        command.Parameters.AddWithValue("id", id);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    public Task<bool> SectionExistsAsync(Guid id) => RowExistsAsync("sections", id);

    public Task<bool> QuestionsSetExistsAsync(Guid id) => RowExistsAsync("questions_sets", id);

    public Task<bool> QuestionExistsAsync(Guid id) => RowExistsAsync("questions", id);

    /// <summary>Counts audit events of a given type for a specific actor (asserts data-model §7 event emission).</summary>
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
}
