using Xunit;

namespace Nabadat.UserManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Shares one <see cref="UserManagementApplicationFactory"/> (and its Testcontainers PostgreSQL)
/// across all M-10 integration test classes, so the container starts once per run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class UserManagementIntegrationCollection : ICollectionFixture<UserManagementApplicationFactory>
{
    public const string Name = "UserManagement Integration";
}
