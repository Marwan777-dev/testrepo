using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nabadat.SurveyBuilder.Api.Accessors;
using Nabadat.SurveyBuilder.Api.Filters;
using Nabadat.SurveyBuilder.Application.Appearance;
using Nabadat.SurveyBuilder.Application.Appearance.Interfaces;
using Nabadat.SurveyBuilder.Application.HtmlSanitisation;
using Nabadat.SurveyBuilder.Application.HtmlSanitisation.Interfaces;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Preview;
using Nabadat.SurveyBuilder.Application.Questions;
using Nabadat.SurveyBuilder.Application.Questions.Interfaces;
using Nabadat.SurveyBuilder.Application.QuestionsSets;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Interfaces;
using Nabadat.SurveyBuilder.Application.RenderPlan;
using Nabadat.SurveyBuilder.Application.Report;
using Nabadat.SurveyBuilder.Application.Report.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Application.Sections;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Application.Translations;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Infrastructure.CrossModule;
using Nabadat.SurveyBuilder.Infrastructure.Elasticsearch;
using Nabadat.SurveyBuilder.Infrastructure.HtmlSanitisation;
using Nabadat.SurveyBuilder.Infrastructure.Idempotency;
using Nabadat.SurveyBuilder.Infrastructure.Persistence;
using Nabadat.SurveyBuilder.Infrastructure.Persistence.Stores;
using TenantSchemaConnectionInterceptor = Nabadat.UserManagement.Infrastructure.Persistence.TenantSchemaConnectionInterceptor;

namespace Nabadat.SurveyBuilder;

/// <summary>
/// Dependency-injection registration for the M-01 Survey &amp; Form Builder module. Call
/// <see cref="AddSurveyBuilderModule"/> from the host composition root (T006). Per
/// architecture-constitution Article 1A, module wiring lives ONLY here — no other file in
/// this module touches the <see cref="IServiceCollection"/>.
/// </summary>
public static class SurveyBuilderServiceCollectionExtensions
{
    /// <summary>
    /// Registers the M-01 module's EF context, sanitiser policy, middleware, and the per-entity
    /// services behind their ports. Controllers under <c>Api/</c> are discovered as an
    /// ApplicationPart by the host (which calls <c>AddControllers()</c>), so they need no explicit
    /// registration here.
    /// <para>This is the T007 skeleton: every sub-domain block below is a marker comment so later
    /// tasks add their registrations in-place without reordering. It is intentionally empty of
    /// bindings today — the ports and services it will register do not exist yet.</para>
    /// </summary>
    public static IServiceCollection AddSurveyBuilderModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- EF context + persistence (T009–T012) ---
        // The DbContext IS the unit of work (DB-08): data-access stores inject it via
        // ITenantDbContext. Maps onto _Baseline.sql; owns no EF migrations. Per-request schema
        // selection (AD-02/DB-01) via M-10's reused TenantSchemaConnectionInterceptor.
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("TenantDb")
                ?? throw new InvalidOperationException("ConnectionStrings:TenantDb is not configured.");
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<TenantSchemaConnectionInterceptor>());
        });
        services.TryAddScoped<TenantSchemaConnectionInterceptor>();
        services.TryAddScoped<ITenantDbContext>(sp => sp.GetRequiredService<TenantDbContext>());
        services.TryAddSingleton(TimeProvider.System);

        // --- Middleware pipeline (T023–T026) ---
        //   Services the M-01 middleware resolve. The pipeline ORDER is wired by
        //   UseSurveyBuilderModule (SurveyBuilderApplicationBuilderExtensions) in the host,
        //   since ordering needs an IApplicationBuilder, not IServiceCollection.
        //   - ICurrentETag: request-scoped ETag carrier read/written by EtagMiddleware (T023).
        //   - IIdempotencyStore: 24h replay store for Idempotency-Key (T024). Singleton in-memory
        //     backing today (single-instance); swap for a distributed impl in production.
        services.AddMemoryCache();
        services.AddScoped<ICurrentETag, CurrentETag>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        // M-01's ICurrentTenant (consumed by ApiErrorEnvelopeMiddleware) bridges to the host's
        // M-10 tenant accessor. Without it every /api/v1/surveys request fails DI resolution → 500.
        services.TryAddScoped<Application.Interfaces.ICurrentTenant, Api.Accessors.HostCurrentTenantAdapter>();

        // --- HTML sanitiser (T027–T029) ---
        //   IHtmlSanitiser → GannsHtmlSanitiserAdapter (stateless → singleton); the active
        //   allowlist SanitiserPolicyVersion.V1 is registered so callers inject the default policy.
        services.AddSingleton(SanitiserPolicyVersion.V1);
        services.AddSingleton<IHtmlSanitiser, GannsHtmlSanitiserAdapter>();

        // --- Data-access stores (T063–T066) ---
        services.AddScoped<ISurveyStore, SurveyStore>();
        services.AddScoped<ISectionStore, SectionStore>();
        services.AddScoped<IQuestionStore, QuestionStore>();
        services.AddScoped<IThemeStore, ThemeStore>();
        services.AddScoped<IRoutingMapStore, RoutingMapStore>();
        services.AddScoped<IQuestionsSetStore, QuestionsSetStore>();
        services.AddScoped<ITranslationStore, TranslationStore>();

        // --- Surveys sub-domain (US1, T067–T074) ---
        services.AddScoped<SurveyValidator>();
        services.AddScoped<SurveyTypeSyncService>();
        services.AddScoped<StatusTransitionPolicy>();
        services.AddScoped<PublishGateService>();
        services.AddScoped<RulesCountProjection>();
        services.AddScoped<DestructiveReturnToDraftService>();
        services.AddScoped<SurveyLifecycleService>();
        services.AddScoped<SurveyCommandService>();

        // --- Approval workflow sub-domain (US2, T113–T118) ---
        services.AddScoped<ApprovalStateMachine>();
        services.AddScoped<PublishAuthorizationService>();
        services.AddScoped<ReviewNotificationBuilder>();
        services.AddScoped<AuditEventFactory>();
        services.AddScoped<ApprovalWorkflowService>();
        services.AddScoped<EditLockPolicy>();

        // --- Questions sub-domain (US1, T075–T079) ---
        services.AddScoped<QuestionValidator>();
        services.AddScoped<KpiBindingValidator>();
        services.AddScoped<KpiBindingChangePolicy>();
        services.AddScoped<CommentFieldFlagPolicy>();
        services.AddScoped<SentimentFlagPolicy>();
        services.AddScoped<QuestionCommandService>();
        services.AddScoped<QuestionDeletionService>();
        services.AddScoped<QuestionMoveService>();

        // --- Sections sub-domain (US3, T137–T138 + SectionCommandService) ---
        services.AddScoped<SectionValidator>();
        services.AddScoped<SectionDeletionGuard>();
        services.AddScoped<SectionCommandService>();
        services.AddScoped<SectionCascadeService>();

        // --- Questions Sets sub-domain (US3, T139 + T141) ---
        services.AddScoped<QuestionsSetValidator>();
        services.AddScoped<QuestionsSetService>();
        services.AddScoped<LowResponseOrderingService>();

        // --- Routing sub-domain (US4, T171–T175) ---
        services.AddScoped<Application.Routing.RoutingEligibilityService>();
        services.AddScoped<Application.Routing.LayoutRoutingCoupler>();
        services.AddScoped<Application.Routing.RoutingConflictDetector>();
        services.AddScoped<Application.Routing.RoutingDefaultTargeter>();
        services.AddScoped<Application.Routing.RoutingConfigurationService>();

        // --- Render-plan / published readers (US3, T143/T144/T146; AD-01) ---
        services.AddScoped<SurveyDefinitionAssembler>();
        services.AddScoped<ISurveyRenderService, SurveyRenderService>();
        services.AddScoped<IActiveSurveyReader, ActiveSurveyReader>();

        // --- Templates sub-domain (US5, T190–T195) ---
        services.AddScoped<Application.Templates.Interfaces.ITemplateStore, Infrastructure.Persistence.Stores.TemplateStore>();
        services.AddScoped<Application.Templates.TemplateAuthorizationService>();
        services.AddScoped<Application.Templates.TemplateSearchService>();
        services.AddScoped<Application.Templates.TemplateCommandService>();

        // --- Translations sub-domain (US6, T211–T213 + TranslationBundleBuilder) ---
        services.AddScoped<TranslatableStringExtractor>();
        services.AddScoped<LocaleFallbackPolicy>();
        services.AddScoped<TranslationBundleBuilder>();
        services.AddScoped<TranslationBundleService>();

        // --- Appearance sub-domain (US1, T080) ---
        services.AddScoped<AppearanceService>();

        // --- Preview sub-domain (US7, T221) ---
        services.AddScoped<PreviewPayloadBuilder>();

        // --- Report sub-domain (US8, T233–T242) ---
        // Pure calculators + the composing service; the ES aggregator port is bound in the
        // Elasticsearch block below (real adapter when a cluster is configured, empty stub otherwise).
        services.AddScoped<PeriodResolver>();
        services.AddScoped<HeadlineCsatCalculator>();
        services.AddScoped<PerQuestionViewSelector>();
        services.AddScoped<VerbatimSampler>();
        services.AddScoped<ResponseWindowFilter>();
        services.AddScoped<EsQueryBuilder>();
        services.AddScoped<ReportService>();

        // --- Analytics sub-domain (US9, T255–T261) ---
        // Pure calculators + the composing service; the ES aggregator port is bound in the
        // Elasticsearch block below. PeriodResolver is shared with the Report block above.
        services.AddScoped<Application.Analytics.FunnelCalculator>();
        services.AddScoped<Application.Analytics.PeriodDeltaCalculator>();
        services.AddScoped<Application.Analytics.ChannelBreakdownCalculator>();
        services.AddScoped<Application.Analytics.TrendGranularityResolver>();
        services.AddScoped<Application.Analytics.AnalyticsService>();

        // --- API action filters (T082) ---
        services.AddScoped<EditLockFilter>();
        services.AddScoped<PublishGateFilter>();

        // --- Cross-module port placeholders (T020 pending) ---
        // Registered so the module composes for dev/E2E; each is swapped for the real owning-module
        // adapter in the host when that module lands. See TODO-M01-001/006/011.
        services.TryAddScoped<IChannelSurveyRulesReader, DevChannelSurveyRulesReader>();
        services.TryAddScoped<ITenantDesignGuidelinesReader, DevTenantDesignGuidelinesReader>();
        services.TryAddScoped<IResponsePurgeService, UnavailableResponsePurgeService>();
        services.TryAddScoped<IJourneyReader, UnavailableJourneyReader>();
        services.TryAddScoped<IKpiCatalogReader, UnavailableKpiCatalogReader>();
        services.TryAddScoped<IEventLogWriter, NoOpEventLogWriter>();
        // US2 ports (TODO-M01-014): M-10 grant check + M-09 reviewer broadcast. Deny-all / no-op in
        // dev so the approval flow composes; swapped for the real M-10/M-09 adapters in the host.
        services.TryAddScoped<IPermissionChecker, DenyAllPermissionChecker>();
        services.TryAddScoped<INotificationDispatcher, NoOpNotificationDispatcher>();

        // US3 port: the FR-2.8 translation-key purge is now backed by the real EF TranslationStore
        // (registered above with the data-access stores); the interim DeferredTranslationStore no-op
        // was removed when the Translations sub-domain shipped (US6, TODO-M01-003).
        // The response-count reader (FR-10.4) reads M-04's ES analytics projection; with no
        // Elasticsearch configured (dev/E2E) it degrades to an empty projection.
        // The Report aggregator (US8, T239) reads the same cluster (AD-04). Both readers bind to the
        // real ES adapter only when a cluster is configured; otherwise they degrade to empty results
        // so the module composes for dev/E2E (TODO-M01-023).
        // Canonical key is Elasticsearch:Uri; Elasticsearch:Url is accepted as an alias so an
        // existing "Url" config binds too. Basic-auth creds + dev self-signed-cert trust let the
        // module reach the default security-enabled Elasticsearch install over HTTPS.
        var elasticsearchUri = configuration["Elasticsearch:Uri"] ?? configuration["Elasticsearch:Url"];
        if (!string.IsNullOrWhiteSpace(elasticsearchUri))
        {
            var esUsername = configuration["Elasticsearch:Username"];
            var esPassword = configuration["Elasticsearch:Password"];
            var trustSelfSigned = string.Equals(
                configuration["Elasticsearch:TrustSelfSignedCertificate"], "true", StringComparison.OrdinalIgnoreCase);
            services.TryAddSingleton(
                EsClientFactory.Create(elasticsearchUri, esUsername, esPassword, trustSelfSigned));
            services.TryAddScoped<IResponseCountReader, ResponseCountReader>();
            services.TryAddScoped<IReportAggregator, ReportAggregator>();
            services.TryAddScoped<Application.Analytics.Interfaces.IAnalyticsAggregator, AnalyticsAggregator>();
        }
        else
        {
            services.TryAddScoped<IResponseCountReader, UnavailableResponseCountReader>();
            services.TryAddScoped<IReportAggregator, UnavailableReportAggregator>();
            services.TryAddScoped<Application.Analytics.Interfaces.IAnalyticsAggregator, UnavailableAnalyticsAggregator>();
        }

        return services;
    }
}
