using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.UserManagement.Api.Accessors;
using Nabadat.UserManagement.Api.Authentication;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.UserManagement.Api.Tenancy;
using Nabadat.UserManagement.Application.Auth;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Hierarchy;
using Nabadat.UserManagement.Application.Hierarchy.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Infrastructure.Audit;
using Nabadat.UserManagement.Infrastructure.Auth;
using Nabadat.UserManagement.Infrastructure.ControlPlane;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Nabadat.UserManagement.Infrastructure.Notifications;
using Nabadat.UserManagement.Infrastructure.Persistence;

namespace Nabadat.UserManagement;

/// <summary>
/// DI registration for the M-10 module. Called from the host's
/// <c>Program.cs</c> (<c>builder.Services.AddUserManagementModule(builder.Configuration)</c>).
///
/// Phase 2 (Foundational) registers the cross-cutting services that exist now —
/// time, crypto, the M-17 event publisher, the session-context accessor, and the
/// control-plane SSO config repository. Each user story extends this method with
/// its own repositories, services, and published-interface implementations.
/// </summary>
public static class UserManagementServiceCollectionExtensions
{
    /// <summary>Deployment flag (Section 10): when true the host serves many tenants by subdomain.</summary>
    public const string EnableMultiTenantFlag = "ENABLE_MULTI_TENANT";

    public static IServiceCollection AddUserManagementModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Time is injected, never read directly (testable via FakeTimeProvider).
        services.TryAddSingleton(TimeProvider.System);

        // EF Core over the tenant schema. The DbContext IS the unit of work (DB-08):
        // services inject it directly and call SaveChangesAsync — no repository / IUnitOfWork
        // layer. It maps onto the existing SQL-baseline schema and owns no EF migrations.
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("TenantDb")
                ?? throw new InvalidOperationException("ConnectionStrings:TenantDb is not configured.");

            // Per-request schema selection (AD-02 / DB-01). ALL tenants share one connection
            // string — and therefore one Npgsql pool — and the schema is bound per connection
            // open by TenantSchemaConnectionInterceptor (SET search_path TO tenant_{slug}),
            // reading the scoped ICurrentTenant. Baking the slug into the connection string
            // instead would fork the pool per tenant. Empty slug (single-tenant mode) →
            // interceptor no-ops → host's default schema. The (sp, options) overload registers
            // options as scoped, so resolving the scoped interceptor here is safe.
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<TenantSchemaConnectionInterceptor>());
        });

        // Scoped: reads the scoped ICurrentTenant to SET search_path per connection open.
        // Registered in both modes — in single-tenant mode the slug is empty and it no-ops.
        services.TryAddScoped<TenantSchemaConnectionInterceptor>();

        // Control-plane EF context (persona baselines + SSO configs). Separate database,
        // separate unit of work — never atomic with a tenant write (DB-08).
        services.AddDbContext<ControlPlaneDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("ControlPlaneDb")
                ?? throw new InvalidOperationException("ConnectionStrings:ControlPlaneDb is not configured.");
            options.UseNpgsql(connectionString);
        });

        // Expose each concrete context through its Application-layer interface so the
        // data-access services (which live in Application and depend on the abstraction)
        // resolve to the same scoped EF context instance.
        services.TryAddScoped<ITenantDbContext>(sp => sp.GetRequiredService<TenantDbContext>());
        services.TryAddScoped<IControlPlaneDbContext>(sp => sp.GetRequiredService<ControlPlaneDbContext>());

        // EF data-access layer. The unit of work + event writer share the scoped
        // TenantDbContext so a business write and its audit row commit in one transaction.
        services.TryAddScoped<IUserManagementEventPublisher, UserManagementEventPublisher>();
        services.TryAddScoped<ITenantUserService, TenantUserService>();
        services.TryAddScoped<IAuthSessionService, AuthSessionService>();
        services.TryAddScoped<IPasswordResetTokenService, PasswordResetTokenService>();
        services.TryAddScoped<IPermissionModuleAssignmentService, PermissionModuleAssignmentService>();
        services.TryAddScoped<IPersonaBaselineService>(
            sp => sp.GetRequiredService<Application.Permissions.PersonaBaselineService>());
        services.TryAddScoped<IDataScopeService, DataScopeService>();
        services.TryAddScoped<ICustomAuthorizationRuleService>(
            sp => sp.GetRequiredService<Application.Permissions.CustomAuthorizationRuleService>());

        // Crypto (stateless singletons).
        services.TryAddSingleton<IPasswordHasher, PasswordHasher>();
        services.TryAddSingleton<ITotpService, TotpService>();

        // MFA secret envelope encryption — implementation selected by deployment mode
        // (GP-02 / AD-05). SaaS uses a cloud KMS; on-prem (and local multi-tenant dev) uses
        // a config AES key. Cloud KMS is chosen only when a CloudProvider is explicitly set —
        // ENABLE_MULTI_TENANT alone no longer forces it, so multi-tenant can run locally
        // before the (Phase 1-unimplemented) KMS exists.
        services.TryAddSingleton<IMfaSecretEncryptionService>(static sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var cloudProvider = config["CloudProvider"];
            if (config.GetValue<bool>("ENABLE_MULTI_TENANT") && !string.IsNullOrWhiteSpace(cloudProvider))
            {
                return string.Equals(cloudProvider, "azure", StringComparison.OrdinalIgnoreCase)
                    ? new AzureKmsEncryptionService()
                    : new AwsKmsEncryptionService();
            }

            return new LocalAesEncryptionService(config);
        });

        // Request-scoped authenticated identity, populated by the PortalSession auth handler.
        services.TryAddScoped<ISessionContextAccessor, SessionContextAccessor>();

        // ASP.NET Core authentication/authorization. The PortalSession scheme validates the opaque
        // bearer session token (see PortalSessionAuthenticationHandler) and is the default scheme, so
        // UseAuthentication populates the principal + ISessionContextAccessor on every request, and
        // [Authorize] on a controller challenges with a 401 + API-05 envelope. Authorization policies
        // (persona/permission gates → 403) layer on top later without re-plumbing.
        services
            .AddAuthentication(PortalSessionDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, PortalSessionAuthenticationHandler>(
                PortalSessionDefaults.AuthenticationScheme, displayName: null, configureOptions: null);
        services.AddAuthorization();

        // Current tenant (AD-05, two deployment modes selected by ENABLE_MULTI_TENANT):
        //  • multi-tenant → request-scoped RequestCurrentTenant, populated per request from
        //    the subdomain by TenantResolutionMiddleware (AD-07 / API-02); slug → schema.
        //  • single-tenant → ConfiguredCurrentTenant (one tenant per host, Tenant:Id).
        // Both providers are registered and ICurrentTenant chooses between them at RESOLUTION
        // time from the (post-build) configuration — mirroring IMfaSecretEncryptionService
        // above. Reading the flag at registration time would bind the wrong provider under a
        // WebApplicationFactory test host, whose ConfigureAppConfiguration override only takes
        // effect during Build() (after this method runs). The real app reads the same value
        // before and after Build(), so its behavior is unchanged.
        services.TryAddSingleton<ITenantRegistry, ConfigurationTenantRegistry>();
        services.TryAddScoped<RequestCurrentTenant>();
        services.TryAddSingleton<ConfiguredCurrentTenant>();
        services.TryAddScoped<ICurrentTenant>(static sp =>
            sp.GetRequiredService<IConfiguration>().GetValue<bool>(EnableMultiTenantFlag)
                ? sp.GetRequiredService<RequestCurrentTenant>()
                : sp.GetRequiredService<ConfiguredCurrentTenant>());

        // Control-plane SSO config (forward-compatibility; no endpoint in Phase 1).
        services.TryAddScoped<IdentityProviderConfigService>();

        // IPersonaBaselineService (control-plane read/seed/edit) is registered above,
        // forwarded to the concrete PersonaBaselineService (the business + data-access merge).

        // --- User Story 1: Authentication (T042–T052) ---

        // Auth helpers.
        services.TryAddSingleton<IPasswordValidator, PasswordValidator>();
        // In-memory challenge store must be a singleton to persist across requests.
        services.TryAddSingleton<IMfaChallengeService, InMemoryMfaChallengeService>();
        services.TryAddScoped<IAccountLockout, AccountLockoutService>();
        services.TryAddScoped<IPasswordResetRateLimiter, PasswordResetRateLimiter>();
        services.TryAddScoped<ISessionService, SessionService>();

        // Published auth interface (consumed by the host middleware and other modules).
        services.TryAddScoped<IUserManagementAuthService, UserManagementAuthService>();

        // Application services consumed by AuthController.
        services.TryAddScoped<TenantAuthenticationService>();
        services.TryAddScoped<MfaChallengeValidator>();
        services.TryAddScoped<MfaEnrollmentService>();
        services.TryAddScoped<PasswordResetService>();

        // --- User Story 2: User & permission management (T076+) ---
        services.TryAddScoped<UserCreationPolicy>();
        services.TryAddScoped<UserManagementService>();
        services.TryAddScoped<DataLayerAuthorizationGuard>();
        services.TryAddScoped<PermissionAssignmentService>();
        services.TryAddScoped<Application.Permissions.PersonaBaselineService>();

        // Published permission-evaluation interface (AD-01) consumed by every module's action boundary.
        services.TryAddScoped<IUserManagementPermissionService, PermissionEvaluationService>();

        // --- User Story 3: data scope rules & hierarchy cascade (T101+) ---
        services.TryAddScoped<IOrganizationHierarchyService, OrganizationHierarchyService>();
        services.TryAddScoped<DataScopeRuleService>();
        services.TryAddScoped<HierarchyCascadeService>();
        services.TryAddScoped<M13ParameterContractAdapter>();
        services.TryAddScoped<Application.Permissions.CustomAuthorizationRuleService>();

        // M-09 Notifications is not present yet — fail-closed stub (password reset → 503).
        services.TryAddScoped<IM09NotificationService, UnavailableM09NotificationService>();

        // --- User Story 4: audit trail (T117+) ---
        // M-10 owns its audit cycle: it writes events to event_log and reads them back
        // through this reader (no external M-17 dependency; resolves gap-analysis I-02/I-04).
        services.TryAddScoped<IAuditLogReader, AuditLogReader>();

        return services;
    }
}
