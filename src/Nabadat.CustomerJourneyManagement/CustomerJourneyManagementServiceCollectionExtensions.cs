using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.CustomerJourneyManagement.Application.Bindings;
using Nabadat.CustomerJourneyManagement.Application.Bindings.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Detection;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Journeys;
using Nabadat.CustomerJourneyManagement.Application.KpiBindings;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Limits;
using Nabadat.CustomerJourneyManagement.Application.Personas;
using Nabadat.CustomerJourneyManagement.Application.Reports;
using Nabadat.CustomerJourneyManagement.Application.Scores;
using Nabadat.CustomerJourneyManagement.Application.Scoring;
using Nabadat.CustomerJourneyManagement.Application.Stages;
using Nabadat.CustomerJourneyManagement.Application.Touchpoints;
using Nabadat.CustomerJourneyManagement.Application.Versioning;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Infrastructure.ControlPlane;
using Nabadat.CustomerJourneyManagement.Infrastructure.Persistence;
using Nabadat.Platform.Contracts.M16;
// Reuse M-10's per-connection search-path interceptor (aliased to avoid the TenantDbContext name
// clash between the two Infrastructure.Persistence namespaces).
using TenantSchemaConnectionInterceptor = Nabadat.UserManagement.Infrastructure.Persistence.TenantSchemaConnectionInterceptor;

namespace Nabadat.CustomerJourneyManagement;

/// <summary>
/// Dependency-injection registration for the M-16 Customer Journey Mapping module.
/// Call <see cref="AddCustomerJourneyManagementModule"/> from the host's composition root (T006).
/// </summary>
public static class CustomerJourneyManagementServiceCollectionExtensions
{
    /// <summary>
    /// Registers M-16's published interfaces in the DI container.
    /// All three are registered as <c>Scoped</c> per
    /// <c>contracts/published-interfaces.md</c>; consumers (M-06, M-07) receive them
    /// through constructor injection and never instantiate M-16 concrete types.
    /// </summary>
    public static IServiceCollection AddCustomerJourneyManagementModule(this IServiceCollection services)
    {
        // Published interface registrations. The real services replace the T005 stubs as
        // each story lands:
        //   IJourneyConfigReader   → JourneyConfigReaderService   (T049, US-2 — DONE)
        //   IReportContractReader  → ReportContractReaderService  (T089, US-4 — DONE)
        //   IJourneyScoreProvider  → JourneyScoreProviderService  (T069, US-3 — DONE)
        // T014b additionally registers ReportContractService here as Scoped.
        // T049: direct-schema reader for M-06 (no cache; constructs JourneyConfigDto fresh).
        services.AddScoped<IJourneyConfigReader, JourneyConfigReaderService>();
        // T089: deserializes the pre-built report_contracts.contract_payload jsonb back to
        // ReportContractDto via IReportContractDataService (single + active-batch reads). M-07 calls
        // this and never touches M-16 tables directly.
        services.AddScoped<IReportContractReader, ReportContractReaderService>();
        // T069: real provider — reads config (T049) → delegates to M-06 → upserts journey_scores +
        // publishes journey.score.updated in one tx. Its IJourneyScoreDataService dependency is now
        // registered by T070 (below); its IM06ScoringService resolves to the throwing placeholder
        // until M-06 lands, so a score refresh fails loudly rather than fabricating a score.
        services.AddScoped<IJourneyScoreProvider, JourneyScoreProviderService>();
        // T020 (Feature 003): binding-usage probe for M-06's FR-026 deactivation confirmation /
        // FR-017 scale-change detection. Keyed on the logical kpi_bindings.kpi_id reference.
        services.AddScoped<IJourneyBindingQuery, JourneyBindingQueryService>();

        // EF Core over the tenant schema. The DbContext IS the unit of work (DB-08): the data-access
        // services inject it through ITenantDbContext and call SaveChangesAsync — there is no
        // repository layer and no ITransactionRunner anymore. It maps onto the existing SQL-baseline
        // schema (001_customer_journey_baseline.sql) and owns no EF migrations.
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("TenantDb")
                ?? throw new InvalidOperationException("ConnectionStrings:TenantDb is not configured.");

            // Per-request schema selection (AD-02 / DB-01): all tenants share one connection string —
            // and one Npgsql pool — and the schema is bound per connection open by M-10's reused
            // TenantSchemaConnectionInterceptor (SET search_path TO tenant_{slug}). In single-tenant
            // mode the slug is empty and the interceptor no-ops onto the host's default schema.
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<TenantSchemaConnectionInterceptor>());
        });

        // Control-plane EF context — M-16 owns no control-plane tables today (all data is
        // tenant-scoped), so it maps nothing; wired for convention parity with M-10 (the second of
        // two context ports) and as the seam for any future M-16 control-plane table. Separate
        // database, separate unit of work — never atomic with a tenant write (DB-08).
        services.AddDbContext<ControlPlaneDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("ControlPlaneDb")
                ?? throw new InvalidOperationException("ConnectionStrings:ControlPlaneDb is not configured.");
            options.UseNpgsql(connectionString);
        });

        // The interceptor reads the scoped ICurrentTenant (registered by AddUserManagementModule,
        // which the host calls before this). TryAdd makes it a no-op when M-10 already registered it.
        services.TryAddScoped<TenantSchemaConnectionInterceptor>();

        // Expose each concrete context through its Application-layer port so the data-access services
        // (which depend on the abstraction) resolve to the same scoped EF context instance.
        services.TryAddScoped<ITenantDbContext>(sp => sp.GetRequiredService<TenantDbContext>());
        services.TryAddScoped<IControlPlaneDbContext>(sp => sp.GetRequiredService<ControlPlaneDbContext>());

        // T013: M-17 audit-event publisher. EF-backed → Scoped (shares the tenant DbContext so the
        // audit row and its business change commit in one SaveChangesAsync / transaction, FR-015).
        services.TryAddScoped<IM17EventPublisher, M17EventPublisher>();

        // T014b/T087: report-contract rebuilder. Registered as a concrete Scoped type (no published
        // interface — it is M-16-internal). Injected by KpiBindingService (T047, US-2) and
        // DetectionConfigService (T085, US-4), which call RebuildContractAsync on their own tx.
        // T087 replaced the no-op stub with the full builder: its ctor needs IJourneyConfigReader
        // (T049), IDetectionDataService (T086), TimeProvider, and IReportContractDataService — whose
        // concrete adapter landed in T088 (below), so the upsert path is now fully wired (no longer
        // dormant).
        services.AddScoped<ReportContractService>();

        // T084: most-specific-wins detection threshold resolver (touchpoint > stage > journey
        // default). Scoped — no per-request state, but it depends on Scoped repositories. Its
        // ITouchpointDataService dependency is already registered (T023); its IDetectionDataService
        // dependency lands in T086, so this registration is dormant until then (the host has no
        // ValidateOnBuild, mirroring how JourneyVersionService was registered ahead of T068).
        services.AddScoped<DetectionOverrideResolver>();

        // T085: journey detection-config save service (validate → upsert config + full-replace
        // overrides + journey.detection_config.updated event + report-contract rebuild, all in one tx).
        // Scoped, consumed by DetectionController (T090). Its IStageDataService/ITouchpointDataService/
        // ITenantDbContext/IM17EventPublisher/ReportContractService deps are already registered; its
        // IDetectionDataService dep lands in T086, so (like the resolver above) the registration is
        // dormant until then.
        services.AddScoped<DetectionConfigService>();

        // T023: tenant-schema persistence adapters (raw Npgsql). Scoped — they hold no
        // per-request state but match the data-access lifetime convention; consumed by the
        // US-1 services (JourneyService/StageService/TouchpointService, T024–T026).
        services.AddScoped<IJourneyDataService, JourneyDataService>();
        services.AddScoped<IStageDataService, StageDataService>();
        services.AddScoped<ITouchpointDataService, TouchpointDataService>();

        // T065: tenant-schema persona adapter (raw Npgsql, personas + journey_persona_bindings join).
        // Scoped — matches the data-access lifetime convention; backs PersonaStatusTransitionService
        // (T063) and PersonaService (T064): CRUD, the binding-count archive guard, the Active-only
        // selector filter, and bind/unbind on the caller's transaction.
        services.AddScoped<IPersonaDataService, PersonaDataService>();

        // T086: tenant-schema detection adapter (raw Npgsql) over detection_configs (one row per
        // journey → INSERT … ON CONFLICT (journey_id) DO UPDATE upsert) and its
        // detection_threshold_overrides children (full-replace save on the caller's tx, mirroring
        // the KPI-binding pattern). Scoped — matches the data-access lifetime convention. Backs the
        // DetectionOverrideResolver (T084) and DetectionConfigService (T085) registered above, which
        // were dormant until this concrete adapter landed (their other deps were already registered).
        services.AddScoped<IDetectionDataService, DetectionDataService>();

        // T088: tenant-schema report-contract adapter (raw Npgsql) over report_contracts (one row per
        // journey → INSERT … ON CONFLICT (journey_id) DO UPDATE upsert of the opaque contract_payload
        // jsonb; report_contract_id + created_at survive rebuilds). Scoped — matches the data-access
        // lifetime convention. Backs ReportContractService (T087), whose RebuildContractAsync upserts
        // on the caller's tx (FR-015) — activating the previously-dormant upsert path of that
        // registration. Also backs ReportContractReaderService (T089) — both its single-journey read
        // (GetByJourneyAsync) and its active-batch read (ListByActiveJourneysAsync).
        services.AddScoped<IReportContractDataService, ReportContractDataService>();

        // T024: Journey service for CRUD operations. Scoped, consumed by JourneysController (T028).
        services.AddScoped<JourneyService>();

        // T022: Journey lifecycle state machine. Scoped, consumed by JourneysController (T028).
        services.AddScoped<JourneyStatusTransitionService>();

        // T063: Persona lifecycle state machine (Draft → Active ↔ Inactive → Archived; Archived
        // terminal). Scoped, consumed by PersonasController (T071). Guards archive against active
        // journey bindings and publishes persona.status.changed in the same tx.
        services.AddScoped<PersonaStatusTransitionService>();

        // T064: Persona CRUD + journey-binding guard. Scoped, consumed by PersonasController (T071).
        // Creates/updates personas (persona.created/persona.updated in one tx), exposes the Active-only
        // binding selector, and binds only Active personas (journey.invalid_persona otherwise). Status
        // transitions are delegated to PersonaStatusTransitionService, not handled here.
        services.AddScoped<PersonaService>();

        // T066: pure-logic snapshot serializer. Stateless → Singleton (no deps, no per-request state);
        // injected into JourneyVersionService to freeze the journey tree into a self-contained blob.
        services.AddSingleton<JourneySnapshotSerializer>();

        // T067: tenant-schema journey-tree reader behind the IJourneySnapshotBuilder seam (raw Npgsql,
        // six reads on one connection). Scoped — matches the data-access lifetime convention; backs the
        // publish path of JourneyVersionService. Integration-tested at the US-3 checkpoint (T074/T076).
        services.AddScoped<IJourneySnapshotBuilder, JourneySnapshotBuilder>();

        // T068: tenant-schema journey_versions adapter (raw Npgsql; immutable inserts + reads, no UPDATE).
        // Scoped — matches the data-access lifetime convention; backs JourneyVersionService publish
        // (CreateAsync on the caller's tx + GetMaxVersionNumberAsync) and read (GetByVersionNumberAsync /
        // keyset-paginated ListByJourneyAsync, newest-first). Integration-tested at the US-3 checkpoint
        // (T074/T076).
        services.AddScoped<IVersionDataService, VersionDataService>();

        // T067: journey version publish/read orchestration. Scoped, consumed by JourneyVersionsController
        // (T072). Builds + serializes the snapshot, writes the journey_versions row + journey.version.published
        // event in one tx, and reads stored snapshots verbatim. Its IVersionDataService dependency is
        // registered by T068.
        services.AddScoped<JourneyVersionService>();

        // T021: Journey name uniqueness validator. Scoped, consumed by JourneyService.
        services.AddScoped<IJourneyNameUniquenessValidator, JourneyNameUniquenessValidator>();

        // Feature 003: the bindable-KPI catalogue port. The standalone default returns M-16's own
        // platform-standard reference types + kpi_type_definitions (no M-06 kpi_id), keeping the port
        // resolvable wherever M-16 runs without M-06 (its integration tests). The deployed host
        // OVERRIDES this registration with an adapter backed by M-06's IKpiConfigReader, so the
        // touchpoint KPI dropdown shows the tenant's active KPI-Management KPIs and saves link kpi_id.
        services.AddScoped<IActiveKpiCatalogReader, PlatformStandardKpiCatalogReader>();

        // T045: KPI weight validator (sum=100.00m, no duplicates, per-weight range, known types).
        // Scoped, consumed by KpiBindingService (T047). "Known type" = a key in the active bindable
        // catalogue (IActiveKpiCatalogReader).
        services.AddScoped<IKpiWeightValidator, KpiWeightValidator>();

        // T046: tenant-schema repository for kpi_type_definitions (raw Npgsql). Scoped — matches
        // the data-access lifetime convention; backs KpiWeightValidator unknown-type resolution
        // (T045) and the KpiTypesController create/list endpoints (T052, US-2).
        services.AddScoped<IKpiTypeDataService, KpiTypeDataService>();

        // T048a (US-2 Amendment): EF data-access for the scoring_configs SINGLETON (one row per
        // tenant — SRS §4.2.9 / §11.7, Q11). Scoped — matches the data-access lifetime convention;
        // backs ScoringConfigService and the JourneySnapshotBuilder tenant-scoring read.
        services.AddScoped<IScoringConfigDataService, ScoringConfigDataService>();

        // T070: tenant-schema repository for journey_scores (raw Npgsql, one row per journey →
        // INSERT … ON CONFLICT (journey_id) DO UPDATE upsert; write-only, never read here). Scoped —
        // matches the data-access lifetime convention; backs JourneyScoreProviderService (T069),
        // which calls UpsertAsync on its own transaction so the score row and the
        // journey.score.updated event commit atomically (FR-015). Closes the DI gap T069 opened.
        services.AddScoped<IJourneyScoreDataService, JourneyScoreDataService>();

        // T047: touchpoint KPI-binding full-replace save service. Scoped, consumed by
        // TouchpointsController (T050). Validates via IKpiWeightValidator (T045), persists the
        // authoritative set + journey.kpi_bindings.updated event + report-contract rebuild in one tx.
        services.AddScoped<KpiBindingService>();

        // T048a/T048b (US-2 Amendment): tenant-level scoring-config service. Validates + upserts the
        // single scoring_configs row + journey.scoring_config.updated event in one tx (FR-015). Also
        // implements the published IScoringConfigStore (M-06 reads tenant scoring through it; feature
        // 003's Settings → Customer Journey page writes through it). Register the concrete once and
        // alias the published port to the SAME scoped instance.
        services.AddScoped<ScoringConfigService>();
        services.AddScoped<IScoringConfigStore>(sp => sp.GetRequiredService<ScoringConfigService>());

        // T052: KPI-type catalog service. Scoped, consumed by KpiTypesController. Lists tenant-defined
        // types via IKpiTypeDataService (T046) and owns the create validation + key-conflict guard
        // (platform-standard keys + existing tenant keys); the platform-standard catalog itself is
        // static reference data on the service. No M-17 event / transaction (single insert).
        services.AddScoped<KpiTypeService>();

        // T029: Stage service (add / update / delete / reorder). Scoped, consumed by StagesController.
        // StageReorderService is a stateless static helper — no registration needed.
        services.AddScoped<StageService>();

        // T026/T030: Touchpoint service (add / update / delete / get). Scoped, consumed by
        // TouchpointsController.
        services.AddScoped<TouchpointService>();

        // T027/T029: per-tenant journey limit resolution. The enforcer (Scoped, per-request, no cache
        // per AD-03) calls M-11 and falls back to platform defaults (20 stages / 30 touchpoints) on
        // failure. M-11 is not present in this tree, so its consumer port resolves to a placeholder
        // that throws — which the enforcer catches and maps to the documented default fallback. When
        // M-11 lands, replace PlaceholderM11TenantService with the real adapter (no enforcer change).
        services.AddScoped<IJourneyLimitProvider, JourneyLimitEnforcer>();
        services.TryAddSingleton<IM11TenantService, PlaceholderM11TenantService>();

        // T069: M-16's in-module consumer port for the M-06 scoring engine. M-06 is not present in
        // this tree (only M-10, M-16, and the host live under src/), so — exactly like M-11 — a
        // throwing placeholder stands in until the real adapter lands. Unlike M-11 there is no
        // fallback: a missing scoring engine must surface as a failed score refresh, never silent
        // data, so JourneyScoreProviderService lets the throw propagate. Dormant in this tree (no
        // consumer resolves IJourneyScoreProvider). Replace with the real M-06 adapter when it lands.
        services.TryAddSingleton<IM06ScoringService, PlaceholderM06ScoringService>();

        return services;
    }
}

/// <summary>Stub for <see cref="IJourneyConfigReader"/>; real impl lands in T049 (US-2).</summary>
internal sealed class NotImplementedJourneyConfigReader : IJourneyConfigReader
{
    public Task<JourneyConfigDto?> GetJourneyConfigAsync(Guid journeyId, CancellationToken ct = default)
        => throw new NotImplementedException("JourneyConfigReaderService is implemented in T049 (US-2).");

    public Task<IReadOnlyList<JourneyConfigDto>> GetActiveJourneyConfigsAsync(CancellationToken ct = default)
        => throw new NotImplementedException("JourneyConfigReaderService is implemented in T049 (US-2).");
}

/// <summary>
/// Legacy T005 stub for <see cref="IReportContractReader"/>; superseded by
/// <c>ReportContractReaderService</c> in T089 (US-4), which is the registered implementation.
/// Retained (unused) alongside the other published-interface stubs.
/// </summary>
internal sealed class NotImplementedReportContractReader : IReportContractReader
{
    public Task<ReportContractDto?> GetReportContractAsync(Guid journeyId, CancellationToken ct = default)
        => throw new NotImplementedException("ReportContractReaderService is implemented in T089 (US-4).");

    public Task<IReadOnlyList<ReportContractDto>> GetActiveReportContractsAsync(CancellationToken ct = default)
        => throw new NotImplementedException("ReportContractReaderService is implemented in T089 (US-4).");
}

/// <summary>Stub for <see cref="IJourneyScoreProvider"/>; real impl lands in T069 (US-3).</summary>
internal sealed class NotImplementedJourneyScoreProvider : IJourneyScoreProvider
{
    public Task<JourneyScoreResultDto?> GetScoresAsync(Guid journeyId, CancellationToken ct = default)
        => throw new NotImplementedException("JourneyScoreProviderService is implemented in T069 (US-3).");
}

/// <summary>
/// Placeholder <see cref="IM11TenantService"/> for deployments where M-11 is not present (the case in
/// this working tree — only M-10, M-16, and the host exist under <c>src/</c>). It throws to signal an
/// unavailable upstream, exactly as a real M-11 outage would; <see cref="JourneyLimitEnforcer"/>
/// catches that and falls back to the platform-default limits (20 stages / 30 touchpoints per stage),
/// so journey edits proceed unblocked. Replace with the real M-11 adapter when that module lands.
/// </summary>
internal sealed class PlaceholderM11TenantService : IM11TenantService
{
    public Task<JourneyLimitsDto> GetJourneyLimitsAsync(CancellationToken ct = default)
        => throw new InvalidOperationException(
            "M-11 TenantService is not present in this deployment; JourneyLimitEnforcer applies platform-default limits.");
}

/// <summary>
/// Placeholder <see cref="IM06ScoringService"/> for deployments where M-06 is not present (the case
/// in this working tree). It throws to signal an unavailable scoring engine — exactly what a real
/// M-06 outage would do. Unlike <see cref="PlaceholderM11TenantService"/> there is no fallback:
/// <c>JourneyScoreProviderService</c> deliberately lets the throw propagate so a missing engine
/// surfaces as a failed score refresh rather than a silently empty score. Replace with the real
/// M-06 adapter when that module lands (no service change required).
/// </summary>
internal sealed class PlaceholderM06ScoringService : IM06ScoringService
{
    public Task<JourneyScoreResultDto> ComputeJourneyScoreAsync(JourneyConfigDto config, CancellationToken ct = default)
        => throw new InvalidOperationException(
            "M-06 scoring engine is not present in this deployment; journey score computation is unavailable.");
}
