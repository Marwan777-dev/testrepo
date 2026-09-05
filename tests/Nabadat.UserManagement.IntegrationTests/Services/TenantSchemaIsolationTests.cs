using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Persistence;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Services;

/// <summary>
/// Proves <c>TenantSchemaConnectionInterceptor</c> isolates tenants while they share ONE
/// connection string / Npgsql pool: a write made under one resolved tenant lands only in
/// that tenant's <c>tenant_{slug}</c> schema, and a different tenant's scope — reusing the
/// same pool — cannot see it. This is the end-to-end proof of the slug→schema binding that
/// the unit tests cover only at the SQL-text level (GP-04 / DB-02 isolation).
/// </summary>
public sealed class TenantSchemaIsolationTests : IClassFixture<MultiTenantApplicationFactory>
{
    private readonly MultiTenantApplicationFactory _factory;

    public TenantSchemaIsolationTests(MultiTenantApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task TenantDbContext_writes_are_visible_only_in_the_resolved_tenants_schema_when_sharing_one_pool()
    {
        var alphaUserId = Guid.NewGuid();

        // Write a user under tenant "alpha".
        using (var alpha = _factory.CreateTenantScope(MultiTenantApplicationFactory.AlphaSlug, MultiTenantApplicationFactory.AlphaId))
        {
            var context = alpha.ServiceProvider.GetRequiredService<TenantDbContext>();
            context.TenantUsers.Add(NewUser(alphaUserId));
            await context.SaveChangesAsync();
        }

        // Tenant "beta", reusing the shared pool, must NOT see alpha's row.
        using (var beta = _factory.CreateTenantScope(MultiTenantApplicationFactory.BetaSlug, MultiTenantApplicationFactory.BetaId))
        {
            var context = beta.ServiceProvider.GetRequiredService<TenantDbContext>();
            (await context.TenantUsers.AnyAsync(u => u.UserId == alphaUserId)).Should().BeFalse();
        }

        // A fresh "alpha" scope (same pool again) still sees it.
        using (var alphaAgain = _factory.CreateTenantScope(MultiTenantApplicationFactory.AlphaSlug, MultiTenantApplicationFactory.AlphaId))
        {
            var context = alphaAgain.ServiceProvider.GetRequiredService<TenantDbContext>();
            (await context.TenantUsers.AnyAsync(u => u.UserId == alphaUserId)).Should().BeTrue();
        }
    }

    private static TenantUser NewUser(Guid id)
    {
        var now = DateTimeOffset.UtcNow;
        return new TenantUser
        {
            UserId = id,
            Username = $"user-{id:N}@example.com",
            PasswordHash = "x",
            Persona = "P-01",
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
