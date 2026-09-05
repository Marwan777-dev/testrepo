using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Npgsql;
using OtpNet;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Shared integration-test fixture for the M-16 Customer Journey Mapping module (T014).
///
/// Boots a fresh Dockerised PostgreSQL via Testcontainers and applies, in order, the
/// M-10 tenant baseline (<c>_Baseline.sql</c>), the M-10 control-plane baseline
/// (<c>_ControlPlane.sql</c>), and the M-16 journey baseline
/// (<c>001_customer_journey_baseline.sql</c>) — all three are copied to this project's output
/// <c>Migrations/</c> folder transitively (M-10 via the host project reference, M-16 via
/// the module reference). The M-10 schema is required because M-16's endpoints are
/// authenticated by M-10's bearer-session middleware, so a test must seed a tenant user
/// and drive the real login → MFA-verify flow to obtain a session token. M-16's
/// <c>event_log</c> is declared <c>CREATE TABLE IF NOT EXISTS</c>, so the M-10 and M-16
/// baselines coexist without conflict.
///
/// Per CLAUDE.md §Unit Test Policy (rule 12), the first M-16 story owns this fixture;
/// later stories reuse it and extend the seeding helpers.
/// </summary>
public sealed class CustomerJourneyManagementApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
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
        await ApplyMigrationAsync("_Baseline.sql");
        await ApplyMigrationAsync("_ControlPlane.sql");
        await ApplyMigrationAsync("001_customer_journey_baseline.sql");
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
                // On-prem mode → LocalAesEncryptionService with a config-supplied key, so the
                // seeded MFA users have a working secret-encryption service for login.
                ["ENABLE_MULTI_TENANT"] = "false",
                ["MfaEncryptionKey"] = MfaEncryptionKeyBase64,
            });
        });

        // The host wires the M-06-backed IActiveKpiCatalogReader (Feature 003), which reads M-06's
        // kpi_definitions. This M-16 fixture provisions only the M-10 + M-16 baselines (no M-06 schema),
        // so restore the in-module default reader (platform-standard reference types + kpi_type_definitions)
        // — the M-16 binding/validation suite is exercised in isolation, exactly as before the integration.
        builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Scoped<IActiveKpiCatalogReader, PlatformStandardKpiCatalogReader>()));
    }

    /// <summary>
    /// Applies a named raw-SQL migration from the output <c>Migrations/</c> folder. A
    /// missing file is a no-op so the harness still builds before a migration lands.
    /// </summary>
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
    /// Seeds an MFA-enrolled active user with the given persona, drives login → MFA
    /// verify, and returns an <see cref="HttpClient"/> whose default Authorization header
    /// carries the resulting bearer session token. M-16 endpoints require a valid session
    /// but do not yet enforce persona authorization, so any persona yields a usable client.
    /// </summary>
    public async Task<HttpClient> SignedInClientAsync(string persona = "P-01") =>
        (await SignedInWithActorAsync(persona)).Client;

    /// <summary>
    /// Same as <see cref="SignedInClientAsync"/> but also returns the seeded actor, so a
    /// test can assert audit events by <c>actor_id</c> (the journey's <c>created_by</c>).
    /// </summary>
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

    /// <summary>Computes the current valid TOTP code for a Base32 secret (matches the module's OTP.NET).</summary>
    public static string ComputeTotp(string base32Secret) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    /// <summary>Counts audit events of a given type for a specific actor (asserts FR-015 event emission).</summary>
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

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";
}
