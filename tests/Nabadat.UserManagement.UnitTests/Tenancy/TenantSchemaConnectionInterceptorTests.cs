using FluentAssertions;
using Nabadat.UserManagement.Infrastructure.Persistence;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Tenancy;

public sealed class TenantSchemaConnectionInterceptorTests
{
    [Fact]
    public void BuildSetSearchPathSql_returns_null_when_resolved_with_empty_slug_so_default_schema_is_used() =>
        TenantSchemaConnectionInterceptor.BuildSetSearchPathSql(string.Empty, isResolved: true).Should().BeNull();

    [Fact]
    public void BuildSetSearchPathSql_quotes_the_tenant_schema_when_slug_is_valid() =>
        TenantSchemaConnectionInterceptor.BuildSetSearchPathSql("acme", isResolved: true)
            .Should().Be("SET search_path TO \"tenant_acme\"");

    [Fact]
    public void BuildSetSearchPathSql_throws_when_slug_is_unsafe_so_injection_cannot_reach_the_connection()
    {
        var act = () => TenantSchemaConnectionInterceptor.BuildSetSearchPathSql("acme\"; drop schema public", isResolved: true);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuildSetSearchPathSql_throws_when_tenant_is_unresolved_so_it_never_falls_back_to_the_default_schema()
    {
        var act = () => TenantSchemaConnectionInterceptor.BuildSetSearchPathSql(string.Empty, isResolved: false);

        act.Should().Throw<InvalidOperationException>();
    }
}
