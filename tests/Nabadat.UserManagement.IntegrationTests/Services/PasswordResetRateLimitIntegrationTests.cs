using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Nabadat.UserManagement.Infrastructure.Persistence;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Services;

/// <summary>
/// Integration coverage for <c>PasswordResetRateLimiter</c> after its EF Core migration
/// (the reference slice for the EF flow). Drives the real limiter — resolved from DI, a
/// fresh scope per call as in production — against the Testcontainers Postgres, and reads
/// state back through <see cref="TenantDbContext"/>. Verifies the sliding window
/// (3 per 30 min), the 4th-request rejection + audit event, and the window reset. EF-bound
/// behaviour lives in the integration lane per the CLAUDE.md Unit Test Policy / DB-08.
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class PasswordResetRateLimitIntegrationTests
{
    private readonly UserManagementApplicationFactory _factory;

    public PasswordResetRateLimitIntegrationTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task EnsureWithinLimit_allows_the_first_three_requests_when_inside_one_window()
    {
        var email = UniqueEmail();

        await RunLimiterAsync(email);
        await RunLimiterAsync(email);
        await RunLimiterAsync(email);

        (await GetRequestCountAsync(email)).Should().Be(3);
    }

    [Fact]
    public async Task EnsureWithinLimit_rejects_the_fourth_request_and_emits_one_audit_event_when_window_is_full()
    {
        var email = UniqueEmail();
        var eventsBefore = await _factory.CountEventsByTypeAsync("password.reset.rate_limited");

        await RunLimiterAsync(email);
        await RunLimiterAsync(email);
        await RunLimiterAsync(email);

        var act = () => RunLimiterAsync(email);

        await act.Should().ThrowAsync<PasswordResetRateLimitExceededException>();
        // Exactly one new audit event landed (M-17 event_log row co-committed with no
        // business state change). UserManagement integration tests share one collection → run
        // sequentially, so the before/after delta is reliable.
        var eventsAfter = await _factory.CountEventsByTypeAsync("password.reset.rate_limited");
        (eventsAfter - eventsBefore).Should().Be(1);
    }

    [Fact]
    public async Task EnsureWithinLimit_opens_a_fresh_window_when_the_previous_window_has_elapsed()
    {
        var email = UniqueEmail();
        await RunLimiterAsync(email);
        await RunLimiterAsync(email);
        await RunLimiterAsync(email);

        // Push the window into the past, then a further request must be allowed and the
        // counter resets to 1 (new window).
        await _factory.ExpireRateLimitWindowAsync(email);

        var act = () => RunLimiterAsync(email);

        await act.Should().NotThrowAsync();
        (await GetRequestCountAsync(email)).Should().Be(1);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Resolves the limiter from a fresh scope (one DbContext per request) and runs it.</summary>
    private async Task RunLimiterAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var limiter = scope.ServiceProvider.GetRequiredService<IPasswordResetRateLimiter>();
        await limiter.EnsureWithinLimitAsync(email);
    }

    /// <summary>Reads the persisted window's request count via EF (0 when no row exists).</summary>
    private async Task<int> GetRequestCountAsync(string email)
    {
        var emailHash = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var record = await db.PasswordResetRateLimits
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.EmailHash == emailHash);
        return record?.RequestCount ?? 0;
    }

    private static string UniqueEmail() => $"reset-{Guid.NewGuid():N}@example.com";
}
