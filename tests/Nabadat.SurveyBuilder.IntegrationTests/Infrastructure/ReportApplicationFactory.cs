using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.SurveyBuilder.Application.Report;
using Nabadat.SurveyBuilder.Application.Report.Interfaces;
using Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Npgsql;
using OtpNet;
using Testcontainers.PostgreSql;
using Xunit;
using ModuleCurrentTenant = Nabadat.SurveyBuilder.Application.Interfaces.ICurrentTenant;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// Report fixture for the F13 Survey Report API tests (T248). Boots a Dockerised Postgres <b>and</b>
/// Elasticsearch, applies the M-01 baselines, pins the tenant id (so the seeded
/// <c>tenant_{tenantId}_responses</c> index matches what <see cref="ReportAggregator"/> reads), and
/// swaps the module's dev <see cref="UnavailableReportAggregator"/> for the real
/// <see cref="ReportAggregator"/> bound to the running ES cluster. Separate from the survey-builder
/// API collection because only the report/analytics tests need the ES cluster (mirrors
/// <see cref="RenderPlanApplicationFactory"/>).
/// <para>The responses index is created with an <b>explicit mapping</b> (keyword id/scope fields,
/// date timestamps, nested-object answers) so the aggregator's <c>term</c> + <c>range</c> filters
/// resolve — dynamic mapping would map <c>survey_id</c> as analysed text and break the term query.
/// Docker must be running (per-story checkpoint, never a per-task gate).</para>
/// </summary>
public sealed class ReportApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Pinned tenant id — the ES responses index and <see cref="ReportAggregator"/> both key off it.</summary>
    public static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

#pragma warning disable CS0618 // PostgreSqlBuilder() ctor deprecated upstream; still functional in Testcontainers 4.x.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("nabadat_tenant")
        .WithUsername("nabadat")
        .WithPassword("nabadat")
        .Build();
#pragma warning restore CS0618

    private readonly EsTestcontainer _elasticsearch = new();

    private static readonly string MfaEncryptionKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray());

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await _elasticsearch.InitializeAsync();
        await ApplyMigrationAsync("_Baseline.sql");                     // M-10 (auth tables + event_log)
        await ApplyMigrationAsync("_ControlPlane.sql");                 // M-10 control-plane
        await ApplyMigrationAsync("001_customer_journey_baseline.sql"); // M-16
        await ApplyMigrationAsync("KpiManagement_Baseline.sql");        // M-06
        await ApplyMigrationAsync("SurveyBuilder_Baseline.sql");        // M-01 (owned tables)
        await CreateResponsesIndexAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _elasticsearch.DisposeAsync();
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
                ["Tenant:Id"] = TenantId.ToString(),
            });
        });

        // Swap the dev empty aggregator for the real ES aggregator bound to the running cluster, so the
        // report reads the seeded tenant responses index.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IReportAggregator>();
            services.AddScoped<IReportAggregator>(sp => new ReportAggregator(
                _elasticsearch.Client,
                sp.GetRequiredService<ModuleCurrentTenant>(),
                sp.GetRequiredService<EsQueryBuilder>(),
                sp.GetRequiredService<ResponseWindowFilter>(),
                sp.GetRequiredService<VerbatimSampler>()));
        });
    }

    // ── Elasticsearch responses index ──────────────────────────────────────────

    private async Task CreateResponsesIndexAsync()
    {
        var index = EsTestcontainer.ResponsesIndex(TenantId);
        var answerProps = new Properties
        {
            { "question_id", new KeywordProperty() },
            { "kpi_family", new KeywordProperty() },
            { "numeric_value", new DoubleNumberProperty() },
            { "gauge_target", new DoubleNumberProperty() },
            { "option_label", new KeywordProperty() },
            { "option_labels", new KeywordProperty() },
            { "text", new TextProperty() },
        };

        var response = await _elasticsearch.Client.Indices.CreateAsync(index, c => c
            .Mappings(m => m.Properties(new Properties
            {
                { "response_id", new KeywordProperty() },
                { "survey_id", new KeywordProperty() },
                { "channel", new KeywordProperty() },
                { "submitted_at", new DateProperty() },
                { "sent_at", new DateProperty() },
                { "completed", new BooleanProperty() },
                { "completion_time_seconds", new IntegerNumberProperty() },
                { "touchpoint_id", new KeywordProperty() },
                { "answers", new ObjectProperty { Properties = answerProps } },
            })));

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException($"Failed to create responses index: {response.DebugInformation}");
        }
    }

    /// <summary>
    /// Indexes a response document into <c>tenant_{TenantId}_responses</c> in the shape
    /// <see cref="ReportAggregator"/> reads (snake_case fields; <paramref name="answers"/> are
    /// per-question answer maps).
    /// </summary>
    public Task SeedResponseAsync(
        Guid surveyId,
        DateTimeOffset submittedAt,
        DateTimeOffset sentAt,
        bool completed,
        int? completionTimeSeconds,
        string channel,
        string? touchpointId,
        IReadOnlyList<Dictionary<string, object?>> answers)
    {
        var responseId = Guid.NewGuid();
        var doc = new Dictionary<string, object?>
        {
            ["response_id"] = responseId.ToString(),
            ["survey_id"] = surveyId.ToString(),
            ["channel"] = channel,
            ["submitted_at"] = submittedAt,
            ["sent_at"] = sentAt,
            ["completed"] = completed,
            ["completion_time_seconds"] = completionTimeSeconds,
            ["touchpoint_id"] = touchpointId,
            ["answers"] = answers,
        };
        return _elasticsearch.SeedResponseAsync(TenantId, responseId.ToString(), doc);
    }

    /// <summary>Builds a single answer map for <see cref="SeedResponseAsync"/>.</summary>
    public static Dictionary<string, object?> Answer(
        Guid questionId,
        string? kpiFamily = null,
        decimal? numericValue = null,
        decimal? gaugeTarget = null,
        string? optionLabel = null,
        IReadOnlyList<string>? optionLabels = null,
        string? text = null) =>
        new()
        {
            ["question_id"] = questionId.ToString(),
            ["kpi_family"] = kpiFamily,
            ["numeric_value"] = numericValue,
            ["gauge_target"] = gaugeTarget,
            ["option_label"] = optionLabel,
            ["option_labels"] = optionLabels,
            ["text"] = text,
        };

    // ── Authenticated client (mirrors SurveyBuilderApplicationFactory) ──────────

    /// <summary>Seeds an MFA-enrolled user, drives login → MFA verify, and returns a bearer client.</summary>
    public async Task<HttpClient> SignedInClientAsync(string persona = "P-01")
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
        return client;
    }

    private async Task<SeededUser> SeedEnrolledUserAsync(string persona, string password = "ValidP@ss1")
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
            Username = $"user-{Guid.NewGuid():N}@example.com",
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

    private static string ComputeTotp(string base32Secret) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    // ── Postgres seeders (raw SQL — no endpoint creates these) ──────────────────

    /// <summary>Inserts an Active survey (optionally with an active period) and returns its id.</summary>
    public async Task<Guid> SeedActiveSurveyAsync(int? activePeriodDays = null)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var activePeriodJson = activePeriodDays is { } days ? $$"""{"days":{{days}},"hours":0}""" : null;
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO surveys
                (id, name_en, survey_type, status, active_period, owner_user_id,
                 created_at, created_by, updated_at, updated_by)
            VALUES (@id, 'Report survey', 'SeasonalRelational', 'Active', @active::jsonb, @owner,
                    now(), @owner, now(), @owner)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("active", (object?)activePeriodJson ?? DBNull.Value);
        command.Parameters.AddWithValue("owner", Guid.Empty);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Inserts a section under a survey and returns its id.</summary>
    public async Task<Guid> SeedSectionAsync(Guid surveyId, int order = 0)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """INSERT INTO sections (id, survey_id, name, "order", created_at, updated_at) VALUES (@id, @s, @n, @o, now(), now())""",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("s", surveyId);
        command.Parameters.AddWithValue("n", $"Section {order}");
        command.Parameters.AddWithValue("o", order);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Inserts a question of the given type/subtype under a section and returns its id. Pass the
    /// matching <paramref name="typePayloadJson"/> ($type discriminator) and, for a KPI question, a
    /// <paramref name="kpiCode"/> (the <c>ck_questions_kpi_code_present</c> CHECK requires it).
    /// </summary>
    public async Task<Guid> SeedQuestionAsync(
        Guid surveyId, Guid sectionId, string type, string subtype, int order,
        string typePayloadJson, string? kpiCode = null, string text = "Q")
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO questions
                (id, survey_id, section_id, type, subtype, text, kpi_code, type_payload, "order", created_at, updated_at)
            VALUES (@id, @survey, @section, @type, @subtype, @text, @kpi, @payload::jsonb, @o, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("survey", surveyId);
        command.Parameters.AddWithValue("section", sectionId);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("subtype", subtype);
        command.Parameters.AddWithValue("text", text);
        command.Parameters.AddWithValue("kpi", (object?)kpiCode ?? DBNull.Value);
        command.Parameters.AddWithValue("payload", typePayloadJson);
        command.Parameters.AddWithValue("o", order);
        await command.ExecuteNonQueryAsync();
        return id;
    }

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
}
