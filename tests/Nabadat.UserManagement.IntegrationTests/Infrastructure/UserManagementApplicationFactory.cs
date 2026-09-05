using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.ControlPlane;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Nabadat.UserManagement.Infrastructure.Persistence;
using Npgsql;
using OtpNet;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Shared integration-test fixture for the M-10 module.
///
/// Boots a fresh Dockerised PostgreSQL via Testcontainers, applies the M-10
/// <c>_Baseline.sql</c> + <c>_ControlPlane.sql</c> migrations, and exposes the
/// running host through <see cref="WebApplicationFactory{TEntryPoint}"/> so endpoint
/// and scenario tests drive the real ASP.NET Core pipeline over HTTP.
///
/// Also provides the User-Story-1 seeding helpers (tenant users, with or without
/// MFA) and small query helpers (TOTP computation, audit-event counts) the auth
/// integration tests build on.
/// </summary>
public sealed class UserManagementApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
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
                // On-prem mode → LocalAesEncryptionService with a config-supplied key.
                ["ENABLE_MULTI_TENANT"] = "false",
                ["MfaEncryptionKey"] = MfaEncryptionKeyBase64,
            });
        });
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

    // ── Seeding ──────────────────────────────────────────────────────────────

    /// <summary>Seeds an MFA-enrolled, active user. The returned secret lets the test compute live TOTP codes.</summary>
    public async Task<SeededUser> SeedEnrolledUserAsync(
        string persona = "P-01",
        string password = "ValidP@ss1",
        UserStatus status = UserStatus.Active,
        DateTimeOffset? lockedUntilUtc = null,
        short failedAttemptCount = 0)
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        var context = sp.GetRequiredService<TenantDbContext>();
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
            Status = status,
            FailedAttemptCount = failedAttemptCount,
            LockedUntilUtc = lockedUntilUtc,
            CreatedAt = now,
            UpdatedAt = now,
        };
        context.TenantUsers.Add(user);
        await context.SaveChangesAsync();
        return new SeededUser(user.UserId, user.Username, password, secret);
    }

    /// <summary>Seeds a first-time user (no MFA) who must enroll before a session can be created.</summary>
    public async Task<SeededUser> SeedPendingEnrollmentUserAsync(string persona = "P-01", string password = "ValidP@ss1")
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        var context = sp.GetRequiredService<TenantDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var clock = sp.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow();

        var user = new TenantUser
        {
            UserId = Guid.NewGuid(),
            Username = UniqueEmail(),
            PasswordHash = hasher.Hash(password),
            IsMfaEnrolled = false,
            Persona = persona,
            Status = UserStatus.PendingEnrollment,
            CreatedAt = now,
            UpdatedAt = now,
        };
        context.TenantUsers.Add(user);
        await context.SaveChangesAsync();
        return new SeededUser(user.UserId, user.Username, password, null);
    }

    /// <summary>
    /// Seeds the 8 platform-default persona baselines for the host's tenant
    /// (<see cref="Guid.Empty"/> in tests — see <c>ConfiguredCurrentTenant</c>), so a
    /// newly created user is provisioned from its persona's baseline. Idempotent.
    /// </summary>
    public async Task SeedPersonaBaselinesAsync(Guid? tenantId = null)
    {
        using var scope = Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPersonaBaselineService>();
        await store.SeedDefaultsAsync(tenantId ?? Guid.Empty);
    }

    // ── Query / mutation helpers ─────────────────────────────────────────────

    /// <summary>Computes the current valid TOTP code for a Base32 secret (matches the module's OTP.NET).</summary>
    public static string ComputeTotp(string base32Secret) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    /// <summary>Counts audit events of a given type for a specific actor.</summary>
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

    /// <summary>Counts audit events of a given type for a specific affected entity.</summary>
    public async Task<int> CountEventsByEntityAsync(Guid entityId, string eventType)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM event_log WHERE entity_id = @e AND event_type = @t", connection);
        command.Parameters.AddWithValue("e", entityId);
        command.Parameters.AddWithValue("t", eventType);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>Counts all audit events of a given type (across actors/entities).</summary>
    public async Task<int> CountEventsByTypeAsync(string eventType)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM event_log WHERE event_type = @t", connection);
        command.Parameters.AddWithValue("t", eventType);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// Reads the <c>old_value</c> / <c>new_value</c> jsonb payloads of the latest event
    /// of a given type for an entity (as raw JSON strings; <c>null</c> when the column is
    /// null or no row exists). Lets tests assert FR-015 payload completeness end-to-end.
    /// </summary>
    public async Task<(string? OldValue, string? NewValue)> GetLatestEventValuesAsync(Guid entityId, string eventType)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT old_value, new_value FROM event_log WHERE entity_id = @e AND event_type = @t " +
            "ORDER BY occurred_at_utc DESC LIMIT 1", connection);
        command.Parameters.AddWithValue("e", entityId);
        command.Parameters.AddWithValue("t", eventType);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return (null, null);
        }

        var oldValue = reader.IsDBNull(0) ? null : reader.GetString(0);
        var newValue = reader.IsDBNull(1) ? null : reader.GetString(1);
        return (oldValue, newValue);
    }

    /// <summary>
    /// Pushes a password-reset rate-limit window into the past so the next request opens
    /// a fresh window. Mirrors the production hash (SHA-256 of the normalized email).
    /// </summary>
    public async Task ExpireRateLimitWindowAsync(string email)
    {
        var emailHash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE password_reset_rate_limit_records SET window_start_utc = @t WHERE email_hash = @h", connection);
        command.Parameters.AddWithValue("t", DateTimeOffset.UtcNow.AddHours(-1));
        command.Parameters.AddWithValue("h", emailHash);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Forces a locked user's cooldown into the past to test post-cooldown login.</summary>
    public async Task ExpireLockoutAsync(Guid userId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE tenant_users SET locked_until_utc = @t WHERE user_id = @id", connection);
        command.Parameters.AddWithValue("t", DateTimeOffset.UtcNow.AddMinutes(-1));
        command.Parameters.AddWithValue("id", userId);
        await command.ExecuteNonQueryAsync();
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";
}
