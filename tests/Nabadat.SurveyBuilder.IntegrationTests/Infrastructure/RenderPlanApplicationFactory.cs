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
using Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using ModuleCurrentTenant = Nabadat.SurveyBuilder.Application.Interfaces.ICurrentTenant;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// Render-plan fixture for the FR-10.4 low-response ordering tests (T158/T159). Boots a Dockerised
/// Postgres <b>and</b> Elasticsearch, applies the M-01 baselines, pins the tenant id (so the seeded
/// <c>tenant_{tenantId}_analytics</c> index matches what <see cref="ResponseCountReader"/> reads),
/// and swaps the module's dev <c>UnavailableResponseCountReader</c> for the real
/// <see cref="ResponseCountReader"/> bound to the running ES cluster.
///
/// <para>The FR-10.4 algorithm is exercised both through the AD-01 published
/// <see cref="ISurveyRenderService"/> (the in-process seam M-02/M-04 consume, resolved from
/// <see cref="WebApplicationFactory{TEntryPoint}.Services"/> via <see cref="InScopeAsync{T}"/>) and,
/// since T150, through the diagnostic <c>GET …/render-plan</c> HTTP route now wired to that service
/// (<see cref="SignedInClientAsync"/> supplies the MFA-gated bearer client; TODO-M01-019 resolved).</para>
/// </summary>
public sealed class RenderPlanApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Pinned tenant id — the ES analytics index and <see cref="ResponseCountReader"/> both key off it.</summary>
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

        // Swap the dev empty-projection reader for the real ES reader bound to the running cluster,
        // so low-response ordering reads the seeded tenant analytics index.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IResponseCountReader>();
            services.AddScoped<IResponseCountReader>(sp =>
                new ResponseCountReader(_elasticsearch.Client, sp.GetRequiredService<ModuleCurrentTenant>()));
        });
    }

    /// <summary>Resolves the published render service from the host container within a fresh request scope.</summary>
    public async Task<T> InScopeAsync<T>(Func<ISurveyRenderService, Task<T>> use)
    {
        using var scope = Services.CreateScope();
        var render = scope.ServiceProvider.GetRequiredService<ISurveyRenderService>();
        return await use(render);
    }

    // ── Authenticated client (mirrors SurveyBuilderApplicationFactory; the HTTP render-plan route is
    //    [Authorize], so the diagnostic-endpoint test needs a real MFA-gated bearer session) ─────────

    /// <summary>Seeds an MFA-enrolled user with the given persona, drives login → MFA verify, and returns a bearer client.</summary>
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
            "/api/v1/auth/mfa/verify",
            new { challengeId, totpCode = SurveyBuilderApplicationFactory.ComputeTotp(actor.Base32Secret) });
        verify.EnsureSuccessStatusCode();
        var token = (await verify.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("sessionToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<SeededUser> SeedEnrolledUserAsync(string persona = "P-01", string password = "ValidP@ss1")
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

    // ── Seeders (raw SQL — no UI creates these; the render service reads them back) ───────────────

    public async Task<Guid> SeedActiveSurveyAsync(string shuffleMode = "random")
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO surveys
                (id, name_en, survey_type, status, layout, shuffle_mode, owner_user_id,
                 created_at, created_by, updated_at, updated_by)
            VALUES (@id, 'Render survey', 'SeasonalRelational', 'Active', 'section', @mode, @owner,
                    now(), @owner, now(), @owner)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("mode", shuffleMode);
        command.Parameters.AddWithValue("owner", Guid.Empty);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    public async Task<Guid> SeedSectionAsync(Guid surveyId, int order)
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

    public async Task<Guid> SeedSetAsync(Guid sectionId, string selectionMode, int count, int order)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """INSERT INTO questions_sets (id, section_id, title, selection_mode, count, "order", created_at, updated_at) VALUES (@id, @s, 'Set', @m, @c, @o, now(), now())""",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("s", sectionId);
        command.Parameters.AddWithValue("m", selectionMode);
        command.Parameters.AddWithValue("c", count);
        command.Parameters.AddWithValue("o", order);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    public async Task<Guid> SeedQuestionAsync(Guid surveyId, Guid sectionId, Guid? setId, int order)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO questions (id, survey_id, section_id, set_id, type, subtype, text, type_payload, "order", created_at, updated_at)
            VALUES (@id, @survey, @section, @set, 'Scale', 'Stars', 'Q', '{"$type":"scale","pointCount":5}'::jsonb, @o, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("survey", surveyId);
        command.Parameters.AddWithValue("section", sectionId);
        command.Parameters.AddWithValue("set", (object?)setId ?? DBNull.Value);
        command.Parameters.AddWithValue("o", order);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>Indexes a <c>question_response_counts</c> doc into the tenant analytics index (M-04's projection shape).</summary>
    public Task SeedResponseCountAsync(Guid questionId, long count) =>
        _elasticsearch.SeedAnalyticsAsync(
            TenantId,
            questionId.ToString(),
            new Dictionary<string, object> { ["question_id"] = questionId.ToString(), ["count"] = count });
}
