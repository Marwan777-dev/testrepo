using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Nabadat.UserManagement.Api.Accessors;
using Nabadat.UserManagement.Api.Middleware;
using Nabadat.UserManagement.Api.Tenancy;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Tenancy;

public sealed class TenantResolutionMiddlewareTests
{
    private const string RegisteredSlug = "gac";

    [Fact]
    public async Task InvokeAsync_resolves_tenant_and_calls_next_when_subdomain_is_a_registered_tenant()
    {
        var result = await InvokeAsync("gac.nabadat.io", "/api/v1/users");

        result.NextCalled.Should().BeTrue();
        result.Tenant.IsResolved.Should().BeTrue();
        result.Tenant.Slug.Should().Be("gac");
        result.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_returns_404_and_does_not_call_next_when_subdomain_is_not_a_registered_tenant()
    {
        var result = await InvokeAsync("ghost.nabadat.io", "/api/v1/users");

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        result.ErrorCode.Should().Be("tenant.not_found");
        result.NextCalled.Should().BeFalse();
        result.Tenant.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_returns_400_when_host_has_no_subdomain()
    {
        var result = await InvokeAsync("localhost", "/api/v1/users");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.ErrorCode.Should().Be("tenant.subdomain_missing");
        result.NextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_returns_400_when_host_is_an_ip_literal()
    {
        var result = await InvokeAsync("127.0.0.1", "/api/v1/users");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.ErrorCode.Should().Be("tenant.subdomain_missing");
    }

    [Fact]
    public async Task InvokeAsync_returns_400_when_subdomain_is_the_reserved_www_label()
    {
        var result = await InvokeAsync("www.nabadat.io", "/api/v1/users");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.ErrorCode.Should().Be("tenant.subdomain_missing");
    }

    [Fact]
    public async Task InvokeAsync_returns_400_when_subdomain_has_unsafe_characters()
    {
        var result = await InvokeAsync("ac_me.nabadat.io", "/api/v1/users");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.ErrorCode.Should().Be("tenant.subdomain_missing");
        result.NextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_bypasses_resolution_for_platform_routes_even_without_a_subdomain()
    {
        var result = await InvokeAsync("localhost", "/health");

        result.NextCalled.Should().BeTrue();
        result.Tenant.IsResolved.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_does_not_bypass_paths_that_only_share_a_prefix_with_a_platform_route()
    {
        // "/healthz" must NOT be treated as the "/health" bypass route.
        var result = await InvokeAsync("localhost", "/healthz");

        result.NextCalled.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.ErrorCode.Should().Be("tenant.subdomain_missing");
    }

    private static async Task<InvocationResult> InvokeAsync(string host, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        var tenant = new RequestCurrentTenant();
        var registry = new FakeTenantRegistry(RegisteredSlug);

        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, tenant, registry);

        string? errorCode = null;
        if (context.Response.Body.Length > 0)
        {
            context.Response.Body.Position = 0;
            using var doc = await JsonDocument.ParseAsync(context.Response.Body);
            errorCode = doc.RootElement.GetProperty("error").GetProperty("code").GetString();
        }

        return new InvocationResult(context.Response.StatusCode, errorCode, nextCalled, tenant);
    }

    private sealed record InvocationResult(int StatusCode, string? ErrorCode, bool NextCalled, RequestCurrentTenant Tenant);

    private sealed class FakeTenantRegistry : ITenantRegistry
    {
        private readonly Dictionary<string, TenantInfo> _tenants = new(StringComparer.OrdinalIgnoreCase);

        public FakeTenantRegistry(params string[] slugs)
        {
            foreach (var slug in slugs)
            {
                _tenants[slug] = new TenantInfo { Id = Guid.NewGuid(), DisplayName = slug };
            }
        }

        public IReadOnlyDictionary<string, TenantInfo> All => _tenants;

        public bool TryResolve(string slug, out TenantInfo info) => _tenants.TryGetValue(slug, out info!);
    }
}
