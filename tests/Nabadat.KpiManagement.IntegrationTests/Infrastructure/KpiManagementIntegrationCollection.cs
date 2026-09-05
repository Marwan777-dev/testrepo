using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Shares one <see cref="KpiManagementApplicationFactory"/> (and its Testcontainers PostgreSQL)
/// across all M-06 integration test classes, so the container starts once per run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class KpiManagementIntegrationCollection : ICollectionFixture<KpiManagementApplicationFactory>
{
    public const string Name = "KpiManagement Integration";
}
