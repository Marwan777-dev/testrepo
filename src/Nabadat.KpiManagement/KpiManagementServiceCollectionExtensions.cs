using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.KpiManagement.Application.Cxi;
using Nabadat.KpiManagement.Application.Cxi.Interfaces;
using Nabadat.KpiManagement.Application.Events;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Application.Perspectives;
using Nabadat.KpiManagement.Application.Perspectives.Interfaces;
using Nabadat.KpiManagement.Infrastructure.Persistence;
// Reuse M-10's per-connection search-path interceptor (aliased to avoid the TenantDbContext name
// clash across the modules' Infrastructure.Persistence namespaces).
using TenantSchemaConnectionInterceptor = Nabadat.UserManagement.Infrastructure.Persistence.TenantSchemaConnectionInterceptor;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Services;
using Nabadat.KpiManagement.Application.Kpis.Validators;
using Nabadat.KpiManagement.Application.Organization;
using Nabadat.KpiManagement.Application.Organization.Interfaces;

namespace Nabadat.KpiManagement;

/// <summary>
/// Dependency-injection registration for the M-06 KPI Management module. Call
/// <see cref="AddNabadatKpiManagement"/> from the host composition root (T025). Wiring lives ONLY
/// here (architecture-constitution Article 1A).
/// </summary>
public static class KpiManagementServiceCollectionExtensions
{
    /// <summary>
    /// Registers the M-06 EF context (behind <see cref="ITenantDbContext"/>), the per-entity
    /// services behind their ports, the published <see cref="IKpiConfigReader"/> (served by
    /// <see cref="KpiConfigReader"/>), and the <see cref="KpiEventPublisher"/>.
    /// <para>NOT YET REGISTERED (the types land in later tasks): the orchestration services
    /// (<c>KpiSaveService</c>, <c>KpiActivationCommandHandler</c>, …), the controllers, the SVG
    /// sanitiser, and the M-11 (<c>Nabadat.TenantAdministration</c>) Organization/Logo stores —
    /// the M-11 module does not exist yet (deferred).</para>
    /// </summary>
    public static IServiceCollection AddNabadatKpiManagement(this IServiceCollection services)
    {
        // EF Core over the tenant schema. The DbContext IS the unit of work (DB-08): the per-entity
        // services inject it through ITenantDbContext and call SaveChangesAsync — no repository
        // layer, no unit-of-work type. It maps onto the SQL baseline (KpiManagement_Baseline.sql)
        // and owns no EF migrations.
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("TenantDb")
                ?? throw new InvalidOperationException("ConnectionStrings:TenantDb is not configured.");

            // Per-request schema selection (AD-02 / DB-01): all tenants share one connection string
            // and the schema is bound per connection open by M-10's reused interceptor
            // (SET search_path TO tenant_{slug}).
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<TenantSchemaConnectionInterceptor>());
        });

        // The interceptor reads the scoped ICurrentTenant (registered by AddUserManagementModule,
        // which the host calls before this). TryAdd makes it a no-op when M-10 already registered it.
        services.TryAddScoped<TenantSchemaConnectionInterceptor>();

        // Expose the concrete context through its Application-layer port so the per-entity services
        // resolve to the same scoped EF context instance.
        services.TryAddScoped<ITenantDbContext>(sp => sp.GetRequiredService<TenantDbContext>());

        // One service per entity (DB-08). KpiDefinitionService is the internal entity port; the
        // published read contract IKpiConfigReader is served by a separate class, KpiConfigReader.
        services.AddScoped<KpiDefinitionService>();
        services.AddScoped<IKpiDefinitionService>(sp => sp.GetRequiredService<KpiDefinitionService>());
        services.AddScoped<IKpiConfigReader, KpiConfigReader>();
        services.AddScoped<IKpiThresholdService, KpiThresholdService>();
        services.AddScoped<IKpiPerspectiveService, KpiPerspectiveService>();
        services.AddScoped<ICxiWeightService, CxiWeightService>();

        // US-3: the CXI weights full-replace orchestrator (PUT /kpis/{cxi_id}/weights). The
        // relative-weight → effective-% maths (CxiWeightNormaliser), the activation gate
        // (CxiActivationRule), the membership rule, and the snapshot composer are static (no DI).
        services.AddScoped<CxiWeightUpdateService>();

        // M-17 audit publisher (EF-backed, shares the tenant DbContext so the audit row and its
        // business change commit in one transaction — data-model.md §8).
        services.AddScoped<KpiEventPublisher>();

        // US-2 validators (FluentValidation) + the create/edit orchestrator (T052–T057).
        // KpiNormalisationCalculator and TopNBoxWarningRule are static (no DI). KpiSaveService
        // depends on IValidator<KpiDefinitionInput>, so register the validator behind that port.
        services.AddScoped<IValidator<KpiDefinitionInput>, KpiDefinitionValidator>();
        services.AddScoped<KpiThresholdValidator>();
        services.AddScoped<KpiSaveService>();

        // FR-017 / FR-026 binding-usage probe over M-16's published IJourneyBindingQuery
        // (registered by AddCustomerJourneyManagementModule, which the host calls before this).
        services.AddScoped<KpiBindingUsageProbe>();

        // US-5: the FR-026 activate/deactivate orchestrator (PATCH /kpis/{id}/activation). The
        // cascade maths (KpiDeactivationSideEffects) is static (no DI).
        services.AddScoped<KpiActivationCommandHandler>();

        // US-6 (T133–T137): the Organization editing surface — all M-06-internal (the
        // organization_settings table + surface are M-06-owned, re-homed from the never-built M-11,
        // 2026-06-24). Stateless helpers are singletons; the store + save-service are scoped (they
        // ride the per-request ITenantDbContext / ICurrentTenant).
        services.AddSingleton<IIndustryEnumProvider, IndustryEnumProvider>();
        services.AddSingleton<LogoUploadValidator>();
        services.AddSingleton<SvgSanitiser>();
        services.AddSingleton<ILogoStore, LogoStore>();
        services.AddScoped<OrganizationSettingsValidator>();
        services.AddScoped<IOrganizationSettingsStore, OrganizationSettingsStore>();
        services.AddScoped<OrganizationSaveService>();

        // US-4 (T099–T102): the tenant Customer Journey ScoringConfig editing surface. The validator
        // + deriver are stateless; the update service validates then delegates the persist + event to
        // M-16's published IScoringConfigStore (registered by AddCustomerJourneyManagement) — M-06 never
        // touches the scoring_configs table directly (AD-01).
        services.AddScoped<Application.ScoringConfig.ScoringConfigValidator>();
        services.AddScoped<Application.ScoringConfig.ScoringConfigUpdateService>();

        return services;
    }
}
