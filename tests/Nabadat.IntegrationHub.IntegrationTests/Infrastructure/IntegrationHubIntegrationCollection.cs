using Xunit;

namespace Nabadat.IntegrationHub.IntegrationTests.Infrastructure;

/// <summary>
/// Shares one <see cref="IntegrationHubApplicationFactory"/> — and therefore one Testcontainers PostgreSQL
/// instance with all four module baselines applied — across every M-13 integration test class, so the
/// container starts once per run instead of once per class.
///
/// <para>Because the fixture is shared and this lane does <b>not</b> roll back (writes are real rows), every
/// test must keep its channel names / channel IDs / API fields unique. The seeding helpers on the factory
/// generate unique values by default for exactly that reason.</para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationHubIntegrationCollection : ICollectionFixture<IntegrationHubApplicationFactory>
{
    public const string Name = "IntegrationHub Integration";
}
