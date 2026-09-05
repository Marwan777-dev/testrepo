using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Infrastructure.ControlPlane;
using Nabadat.UserManagement.Infrastructure.Persistence;
using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Validates that both EF models build with the Npgsql provider — catches entity-config
/// and value-converter mistakes (column maps, jsonb, <c>varchar[]</c> arrays, enum
/// conversions) without needing a database. The real schema-match against the SQL
/// baselines is exercised by the service / endpoint integration tests on Testcontainers.
/// No container fixture: building the model needs the provider, not a connection.
/// </summary>
public sealed class DbContextModelTests
{
    [Fact]
    public void TenantDbContext_model_builds_with_all_entities()
    {
        using var context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);

        context.Model.GetEntityTypes().Should().NotBeEmpty();
    }

    [Fact]
    public void ControlPlaneDbContext_model_builds_with_all_entities()
    {
        using var context = new ControlPlaneDbContext(
            new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);

        context.Model.GetEntityTypes().Should().NotBeEmpty();
    }
}
