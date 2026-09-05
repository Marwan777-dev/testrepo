// Nabadat TenantAdmin host — modular monolith entry point.
// M-10 (User & Role Management) and future modules register their services and
// controllers here.

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.TenantAdmin;
using Nabadat.TenantAdmin.Theming;
using Nabadat.UserManagement;
using Nabadat.UserManagement.Api.Middleware;
using Nabadat.CustomerJourneyManagement;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;
using Nabadat.KpiManagement;
using Nabadat.SurveyBuilder;
using Nabadat.IntegrationHub;
using Nabadat.TenantAdmin.Development;
using Nabadat.TenantAdmin.Integration;

var builder = WebApplication.CreateBuilder(args);

// Controllers from referenced modules (e.g. Nabadat.UserManagement) are discovered
// as ApplicationParts because the host references those assemblies.
builder.Services.AddControllers();

// M-10 module registration (T030).
builder.Services.AddUserManagementModule(builder.Configuration);

// M-16 module registration (T006): registers the Customer Journey Mapping module's
// published interfaces (currently NotImplementedException stubs from T005).
builder.Services.AddCustomerJourneyManagementModule();

// M-06 module registration (T025): EF context + per-entity services + IKpiConfigReader +
// KpiEventPublisher. Orchestration services, controllers, the SVG sanitiser, and the M-11
// Organization/Logo stores are registered by later tasks (M-11 deferred).
builder.Services.AddNabadatKpiManagement();

// Feature 003 — complete the M-06 ↔ M-16 KPI integration. M-16 ships a standalone default
// IActiveKpiCatalogReader (its own platform/tenant types); here, where both modules are present,
// replace it with the M-06-backed adapter so the touchpoint KPI dropdown lists the tenant's active
// KPI-Management KPIs and touchpoint bindings persist their kpi_id link.
builder.Services.Replace(ServiceDescriptor.Scoped<IActiveKpiCatalogReader, M06ActiveKpiCatalogReader>());

// M-01 module registration (feature 004, T006): Survey & Form Builder. The extension method
// body is the T007 skeleton (marker comments only) — per-story tasks populate its DI bindings.
builder.Services.AddSurveyBuilderModule(builder.Configuration);

// M-13 module registration (feature 006, T012): Integration Hub. Registers the EF tenant context and
// the Null* default adapters for the three M-13-owned M-02/M-04 ports; per-story tasks populate the
// rest of its DI bindings in place of the TODO(USn) markers inside the extension method.
builder.Services.AddIntegrationHubModule(builder.Configuration);

// Tenant theming: the brand seed is served from tenant-themes.json (subdomain → colors) with an
// in-code default. Singleton — the file is read once at construction and cached.
builder.Services.AddSingleton<TenantThemeProvider>();

// Rate limiting for the anonymous, pre-auth theming endpoint (GET /api/theme/current).
// A per-client-IP fixed window so a single caller can't flood the public endpoint while
// genuine first-paint loads (one per page load) stay well under the limit. Rejected
// requests return 429; the SPA treats any non-2xx as "keep the default theme".
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ThemeRateLimit.Policy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

// Multi-tenant mode (AD-05): subdomain → tenant_{slug} schema, resolved per request.
var multiTenant = app.Configuration.GetValue<bool>(UserManagementServiceCollectionExtensions.EnableMultiTenantFlag);

// Seed the browser-E2E auth fixtures into the tenant DB (idempotent, dev-only).
if (app.Environment.IsDevelopment())
{
    // Provision each tenant_{slug} schema (+ control-plane) before seeding rows into them.
    if (multiTenant)
    {
        await DevTenantSchemaBootstrapper.EnsureSchemasAsync(app.Services);
    }

    await DevDataSeeder.SeedAsync(app.Services);
}
else
{
    // Production: apply schema migrations and seed initial data when config flags are set.
    // Set MIGRATE_ON_STARTUP=true and SEED_ON_STARTUP=true in appsettings.Production.json
    // for the first deployment. After the app starts successfully, set both to false.
    await ProductionSetupRunner.RunAsync(app);
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Tenant resolution (AD-07 / API-02) MUST precede authentication: the bearer token is
// validated against the tenant schema, so the schema must be bound first.
if (multiTenant)
{
    // The subdomain ({slug}.<host>) is terminated by a reverse proxy in production and by
    // the Vite dev proxy locally; both forward the original host in X-Forwarded-Host.
    // Apply it to Request.Host BEFORE tenant resolution so the slug is recoverable.
    var forwardedHeaders = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedHost };
    if (app.Environment.IsDevelopment())
    {
        // Dev: the local proxy is the only forwarder — trust it without an allow-list.
        // Production MUST instead populate KnownProxies/KnownNetworks (a spoofable Host
        // header is a tenant-isolation risk — GP-04).
        forwardedHeaders.KnownNetworks.Clear();
        forwardedHeaders.KnownProxies.Clear();
    }

    app.UseForwardedHeaders(forwardedHeaders);
    app.UseTenantResolution();
}

// M-01 (Survey & Form Builder) middleware — scoped to M-01 routes (T026): after correlation-id +
// tenant-context (above), runs error-envelope → idempotency-key → etag, before authentication so
// the API-05 envelope also wraps auth failures on M-01 routes.
app.UseSurveyBuilderModule();

// M-10 bearer-token authentication: the PortalSession scheme validates the opaque session
// token, sets the principal, and populates the request-scoped SessionContext. UseAuthorization
// enforces [Authorize] (401 + API-05 envelope via the handler) for protected controllers.
app.UseAuthentication();
app.UseAuthorization();

// Enforce the named rate-limit policies (currently the anonymous theming endpoint).
app.UseRateLimiter();

app.MapControllers();

// Development-only E2E fixture endpoints (single-account resets, no auth).
if (app.Environment.IsDevelopment())
{
    app.MapDevFixtureEndpoints();
}

// Lightweight liveness endpoint for smoke checks.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// SPA fallback — any route not matched by a controller or health check serves
// index.html so React Router handles client-side navigation.
app.MapFallbackToFile("index.html");

app.Run();

// Exposed so Microsoft.AspNetCore.Mvc.Testing's WebApplicationFactory<Program>
// (used by the integration-test UserManagementApplicationFactory) can reference the entry point.
public partial class Program { }
