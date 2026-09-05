using FluentAssertions;
using Nabadat.UserManagement.Application.Tenancy;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Tenancy;

public sealed class TenantSlugTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("globex")]
    [InlineData("a1")]
    [InlineData("ac-me")]
    [InlineData("tnt2")]
    public void IsValid_returns_true_when_slug_is_a_clean_dns_label(string slug) =>
        TenantSlug.IsValid(slug).Should().BeTrue();

    [Theory]
    [InlineData("")]                 // empty
    [InlineData("-acme")]            // leading hyphen
    [InlineData("acme-")]            // trailing hyphen
    [InlineData("ac me")]            // space
    [InlineData("ac;me")]            // statement separator
    [InlineData("ac_me")]            // underscore is not a DNS label char
    [InlineData("acme\"")]           // quote — search_path injection vector
    [InlineData("AcMe")]             // uppercase (slugs are canonicalised to lowercase)
    public void IsValid_returns_false_when_slug_is_malformed_or_unsafe(string slug) =>
        TenantSlug.IsValid(slug).Should().BeFalse();

    [Fact]
    public void IsValid_returns_false_when_slug_exceeds_63_characters() =>
        TenantSlug.IsValid(new string('a', 64)).Should().BeFalse();

    [Fact]
    public void SchemaName_prefixes_the_slug_with_tenant_() =>
        TenantSlug.SchemaName("acme").Should().Be("tenant_acme");
}
