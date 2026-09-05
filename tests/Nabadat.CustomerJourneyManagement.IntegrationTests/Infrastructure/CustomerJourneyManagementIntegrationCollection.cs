using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Shares one <see cref="CustomerJourneyManagementApplicationFactory"/> (and its Testcontainers PostgreSQL)
/// across all M-16 integration test classes, so the container starts once per run.
/// Tests within the collection run sequentially; each uses unique inputs (journey
/// names, seeded users) to stay independent without truncating shared tables.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CustomerJourneyManagementIntegrationCollection : ICollectionFixture<CustomerJourneyManagementApplicationFactory>
{
    public const string Name = "M16 Integration";
}
