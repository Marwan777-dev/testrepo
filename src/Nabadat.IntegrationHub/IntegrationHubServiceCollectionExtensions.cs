using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.IntegrationHub.Application.Channels;
using Nabadat.IntegrationHub.Application.Channels.Interfaces;
using Nabadat.IntegrationHub.Application.Interfaces;
using Nabadat.IntegrationHub.Application.Parameters;
using Nabadat.IntegrationHub.Application.Parameters.Interfaces;
using Nabadat.IntegrationHub.Domain.Interfaces;
using Nabadat.IntegrationHub.Infrastructure.ChannelDispatch;
using Nabadat.IntegrationHub.Infrastructure.CrossModule;
using Nabadat.IntegrationHub.Infrastructure.Persistence;
using Nabadat.IntegrationHub.Infrastructure.UserManagementIntegration;
// Reuse M-10's per-connection search-path interceptor (aliased to avoid the TenantDbContext name
// clash across the modules' Infrastructure.Persistence namespaces).
using TenantSchemaConnectionInterceptor = Nabadat.UserManagement.Infrastructure.Persistence.TenantSchemaConnectionInterceptor;

namespace Nabadat.IntegrationHub;

/// <summary>
/// Dependency-injection registration for the M-13 Integration Hub module. Call
/// <see cref="AddIntegrationHubModule"/> from the host composition root. Per architecture-constitution
/// Article 1A, module wiring lives ONLY here — no other file in this module touches the
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class IntegrationHubServiceCollectionExtensions
{
    /// <summary>
    /// Registers the M-13 EF context (behind <see cref="ITenantDbContext"/>) and the default bindings for
    /// the three M-13-owned cross-module ports. Controllers under <c>Api/</c> are discovered as an
    /// ApplicationPart by the host (which calls <c>AddControllers()</c>), so they need no registration here.
    ///
    /// <para>Built up story by story: the US1 Channels block is live (T036); each remaining per-story block
    /// below is still a <c>// TODO(USn)</c> marker, so later tasks add their registrations in place without
    /// reordering.</para>
    /// </summary>
    public static IServiceCollection AddIntegrationHubModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // --- EF context + persistence (T009-T011) ---
        // The DbContext IS the unit of work (DB-08): the per-aggregate services inject it via
        // ITenantDbContext. Maps onto IntegrationHub_Baseline.sql; owns no EF migrations. Per-request
        // schema selection (AD-02/DB-01) via M-10's reused TenantSchemaConnectionInterceptor.
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("TenantDb")
                ?? throw new InvalidOperationException("ConnectionStrings:TenantDb is not configured.");
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<TenantSchemaConnectionInterceptor>());
        });

        // The interceptor reads the scoped ICurrentTenant registered by AddUserManagementModule, which the
        // host calls before this. TryAdd makes it a no-op when M-10 already registered it.
        services.TryAddScoped<TenantSchemaConnectionInterceptor>();

        // Expose the concrete context through its Application-layer port so every M-13 service resolves to
        // the same scoped EF context instance.
        services.TryAddScoped<ITenantDbContext>(sp => sp.GetRequiredService<TenantDbContext>());

        // Injected clock (DB-08 rule 7) — the inbound pipeline's request-log timestamps, credential
        // generated/revoked stamps, and the 7-day unmapped-value window are all computed through it so
        // tests can drive them with FakeTimeProvider. No DateTime.UtcNow anywhere in this module.
        services.TryAddSingleton(TimeProvider.System);

        // --- Cross-module port defaults (T013-T016) ---
        // M-02 (survey resolution/dispatch) and M-04 (response ingestion) do not exist in this repo yet, so
        // each M-13-owned port binds to a Null* adapter. Registered as SINGLETONS on purpose: the adapters
        // hold no per-request state, and their recorded-call queues must outlive the request scope for the
        // US4 integration tests to assert against them. Swapping in the real adapter when M-02/M-04 ship is
        // a one-line change here — no consumer edits (coordination-log.md C-01/C-02).
        //
        // Each concrete type is registered as itself as well as behind its port, so a test can resolve the
        // adapter and read Calls without casting through the interface.
        services.TryAddSingleton<NullSurveyResolutionReader>();
        services.TryAddSingleton<ISurveyResolutionReader>(sp => sp.GetRequiredService<NullSurveyResolutionReader>());
        services.TryAddSingleton<NullSurveyDispatchGateway>();
        services.TryAddSingleton<ISurveyDispatchGateway>(sp => sp.GetRequiredService<NullSurveyDispatchGateway>());
        services.TryAddSingleton<NullResponseIngestionGateway>();
        services.TryAddSingleton<IResponseIngestionGateway>(sp => sp.GetRequiredService<NullResponseIngestionGateway>());

        // NOTE: IParameterCatalogReader (T017) is deliberately NOT registered. It is a published forward
        // contract with no consumer and no implementation yet (research.md §4.7); its implementing class
        // lands with the first M-14/15/16 consumer.

        // --- US1: Channels sub-domain (T036) ---
        // The five rules are stateless, dependency-free, and thread-safe, so they are singletons; the
        // aggregate service is scoped because it holds the request-scoped ITenantDbContext. The rules are
        // registered as their concrete types (not behind interfaces) on purpose: they are internal
        // composition parts of ServiceChannelService with no second implementation and no mock seam of their
        // own — the unit tests instantiate them directly. IServiceChannelService is the seam.
        services.TryAddSingleton<ChannelIdSanitizer>();
        services.TryAddSingleton<ChannelNameValidator>();
        services.TryAddSingleton<ChannelIdUniquenessValidator>();
        services.TryAddSingleton<ChannelIdLockGuard>();
        services.TryAddSingleton<ParameterContractDependencyRule>();
        services.TryAddScoped<IServiceChannelService, ServiceChannelService>();

        // --- US2: Parameters sub-domain (T060) ---
        // Same shape as the US1 block: the rules are stateless and dependency-free, so they are singletons
        // registered as their concrete types (they are internal composition parts of ParameterService, not mock
        // seams — the unit tests instantiate them directly); IParameterService is the seam.
        //
        // ApiFieldNameSuggester is registered even though ParameterService does not consume it: the auto-suggest
        // runs client-side as the user types (FR-S6-02), and this registration is what lets a future server-side
        // caller — or a diagnostic endpoint — resolve the same rule rather than re-deriving it.
        services.TryAddSingleton<ApiFieldNameSuggester>();
        services.TryAddSingleton<ParameterNameValidator>();
        services.TryAddSingleton<ApiFieldNameUniquenessValidator>();
        services.TryAddSingleton<ApiFieldNameLockGuard>();
        services.TryAddSingleton<RangeConfigValidator>();
        services.TryAddSingleton<ParameterDisableImpactScanner>();
        services.TryAddSingleton<BuiltInParameterGuard>();
        services.TryAddScoped<IParameterService, ParameterService>();

        // BR-10's external half. No module can answer "who references parameter P?" yet — M-10 publishes only the
        // forward per-user scope read, and M-14/15/16 do not exist — so the port binds to the empty reader and the
        // impact warning covers channel contracts only. TODO-M13-005; swapping in a real adapter is a one-line
        // change here with no consumer edits.
        services.TryAddScoped<IExternalParameterReferenceReader, NullExternalParameterReferenceReader>();

        // The REAL M-10 data-scope integration (T059, research.md §4.1) — not a stub: M-10's
        // M13ParameterContractAdapter is already built and waiting for M-13 to be the caller.
        //
        // The base address is configuration-driven and deliberately NOT defaulted: in a single-host deployment it
        // points back at this same host, in a split deployment at M-10's service, and guessing either would
        // produce a silently-wrong push. When it is unset the client throws, DataScopeContractPublisher catches and
        // logs, and the tenant's own catalogue is unaffected — a missing projection that is visible in the log,
        // rather than a console write that fails for a downstream reason.
        // Named explicitly (rather than relying on the type-name default) so the integration lane can
        // reconfigure this exact client — it points the transport at the in-memory test server to exercise the
        // real M-10 endpoint without a second process.
        services.AddHttpClient<IDataScopeContractClient, DataScopeHttpClient>(
            DataScopeHttpClient.ClientName,
            client =>
            {
                var baseUrl = configuration["UserManagement:BaseUrl"];
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
                }
            });

        services.TryAddScoped<DataScopeContractPublisher>();

        // TODO(US3/US8/US10): Integrations sub-domain — IntegrationNameValidator, ScenarioSelectionRule,
        //            ApiKeyGenerationService, OAuthClientGenerationService, CredentialRevocationService,
        //            WizardDraftDiscardPolicy, OAuthScopeEnforcer, the status toggles, and
        //            IIntegrationService/IntegrationService (T076-T084).

        // TODO(US4): Requests sub-domain — the ordered validation pipeline (RequestValidationPipeline,
        //            ResultCodeMapper, ChannelContractRequiredFieldChecker, ParameterTypeValidator + the 13
        //            per-type validators, UnregisteredParameterStore, IdempotencyKeyResolver,
        //            AllowedOriginsWhitelistStore, SurveyLinkExpiryCalculator) plus the REAL M-01 adapter
        //            (RealSurveyRenderServiceAdapter over ISurveyRenderService) (T102-T112).

        // TODO(US5): Monitoring sub-domain — IntegrationHealthTileCalculator, ErrorRateColourResolver,
        //            IntegrationListFilter, RequestLogFilterCombinator, PiiMaskingFormatter,
        //            RejectedRequestDetailProjection, IRequestLogService/RequestLogService (T123-T132).

        // TODO(US6/US7): Mappings sub-domain — MappingSourceValueUniquenessValidator,
        //            UnmappedValueQueueService, MappingResolver, MappingEnabledParameterFilter,
        //            IParameterMappingService/ParameterMappingService, and the Excel
        //            exporter/import-validator/import-mode-applier + capacity guards (T156-T162, T175-T182).

        // TODO(US9): Permissions sub-domain — PermissionKeyResolver, CrossPersonaViewGuard,
        //            AuditEventEmitter (T143-T146).

        return services;
    }
}
