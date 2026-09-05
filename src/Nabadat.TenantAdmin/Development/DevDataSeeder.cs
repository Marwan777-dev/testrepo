using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nabadat.UserManagement;
using Nabadat.UserManagement.Api.Accessors;
using Nabadat.UserManagement.Api.Tenancy;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.ControlPlane;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Nabadat.UserManagement.Infrastructure.Persistence;

namespace Nabadat.TenantAdmin.Development;

/// <summary>
/// Development-only seeder that guarantees the browser-E2E auth fixtures
/// (<c>tests/Nabadat.E2ETests</c>) exist and are in a known-good state on
/// every host startup. It is idempotent and self-healing: re-running clears any
/// lockout / failed-attempt state, re-enrolls (or de-enrolls) MFA, wipes the
/// reset-token + rate-limit tables, and re-issues a fresh single-use reset token —
/// so the stateful auth tests (which lock accounts, enroll MFA, consume tokens, and
/// exhaust rate-limit windows) recover simply by restarting the backend.
///
/// The credentials, TOTP secret, and raw reset token below MUST match the E2E
/// project's <c>appsettings.local.json</c> (and its <c>.example</c>). The TOTP secret
/// is encrypted with the SAME <see cref="IMfaSecretEncryptionService"/> the running
/// host uses to decrypt at verify time, so codes computed from the plaintext secret
/// validate. Runs ONLY in the Development environment (gated in <c>Program.cs</c>).
/// </summary>
public static class DevDataSeeder
{
    // AUTH-1 (login happy path) + AUTH-3 (invalid code): active, MFA-enrolled user.
    public const string ActiveEmail = "e2e-active@dev.local";
    public const string ActivePassword = "Admin123!";

    /// <summary>Plaintext Base32 TOTP secret for <see cref="ActiveEmail"/>. 32 chars = 160-bit.</summary>
    public const string ActiveTotpSecret = "DI2ESN5X42JHHVTQQ3AJSUSIWUV42T33";

    // Convenience dev login: a P-01 with the full module set, seeded CREATE-ONLY (see the
    // block in SeedTenantAsync). Created once, then never overwritten — so resetting or
    // re-enrolling its MFA persists across restarts. On a fresh DB it starts MFA-enrolled
    // with ActiveDevTotpSecret (login works immediately); reset it (de-enroll) if you'd
    // rather enroll your own authenticator.
    public const string ActiveDevEmail = "active@dev.local";
    public const string ActiveDevPassword = "Admin123!";

    /// <summary>Default Base32 TOTP secret, applied only when active@dev.local is first created. 32 chars = 160-bit.</summary>
    public const string ActiveDevTotpSecret = "MX6JURH67SXUTKB5RECBJ4MTYY7U7FTF";

    // Developer's own admin account. Not created here (its credentials live wherever
    // it was provisioned); the seeder only grants it the full module set if it exists,
    // so the M-10 management UI is reachable when signed in as it.
    public const string AdminEmail = "admin@dev.local";

    // AUTH-2 (MFA enrollment): a user that has NOT enrolled an MFA factor.
    public const string EnrollEmail = "e2e-enroll@dev.local";
    public const string EnrollPassword = "Admin123!";

    // AUTH-4 (password-reset): the test mints a fresh single-use token for this user
    // via the dev fixture endpoint each run (see DevFixtureEndpoints). Kept separate
    // from the active login user so redeeming the token (which changes this user's
    // password) never disturbs the AUTH-1/AUTH-3 credentials.
    public const string ResetEmail = "e2e-reset@dev.local";
    public const string ResetPassword = "Admin123!";

    // USR-3 (T095): an active, MFA-enrolled P-07 (Tenant Administrator). Granted only
    // the two non-CX modules a P-07 may administer, so the User Management UI is
    // reachable but CX-domain permission rows render disabled (FR-007).
    public const string P07Email = "e2e-p07@dev.local";
    public const string P07Password = "Admin123!";

    /// <summary>Plaintext Base32 TOTP secret for <see cref="P07Email"/>. 32 chars = 160-bit.</summary>
    public const string P07TotpSecret = "KRMFA52VG5DTQOJXHE3TQ7BXO5UWK3TF";

    // Active, MFA-enrolled P-03 (read-only persona) with NO module grants. Reused by two
    // negative tests: JOUR-2 (M-16 T041) — P-03 cannot author customer journeys (P-01/P-02
    // only), so the "Customer Journeys" sidebar entry must stay hidden; and USR-4 / PB-2
    // (T096) — a non-manager persona must be denied the User Management / Persona Baselines
    // routes (which gate on the UserManagement module).
    public const string P03Email = "e2e-p03@dev.local";
    public const string P03Password = "Admin123!";

    /// <summary>Plaintext Base32 TOTP secret for <see cref="P03Email"/>. 32 chars = 160-bit.</summary>
    public const string P03TotpSecret = "P3R2QK7XV4ND6MZAJ5TWHE3SUYBC2F4G";

    // Active, MFA-enrolled P-02 (CX Analyst). Granted the full CX module set (like P-01) so the
    // Customer Journeys + Personas surfaces are fully reachable — the ONLY difference from P-01 is
    // the persona code. M-16 US-3 (T080) drives this account to prove the persona-based authority
    // split: P-02 may author journeys but CANNOT publish journey versions or manage personas
    // (those controls gate on persona === "P-01"). Used by the PersonaVersionTests PV-5/PV-7 denials.
    public const string P02Email = "e2e-p02@dev.local";
    public const string P02Password = "Admin123!";

    /// <summary>Plaintext Base32 TOTP secret for <see cref="P02Email"/>. 32 chars = 160-bit.</summary>
    public const string P02TotpSecret = "M2VVVL6XK4QHN5BZ7T3JWERA6YGDS2PC";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var configuration = services.GetRequiredService<IConfiguration>();

        if (configuration.GetValue<bool>(UserManagementServiceCollectionExtensions.EnableMultiTenantFlag))
        {
            // Seed every registered tenant's OWN schema. Each tenant gets a fresh DI scope
            // with RequestCurrentTenant resolved up front, so the scope's TenantDbContext
            // binds to tenant_{slug} and the rows land in the right schema (GP-04 isolation).
            var registry = services.GetRequiredService<ITenantRegistry>();
            foreach (var (slug, info) in registry.All)
            {
                using var tenantScope = services.CreateScope();
                tenantScope.ServiceProvider.GetRequiredService<RequestCurrentTenant>().Resolve(info.Id, slug);
                await SeedTenantAsync(tenantScope.ServiceProvider, info.Id, ct);
            }

            return;
        }

        // Single-tenant mode: one schema, tenant id from Tenant:Id.
        using var singleScope = services.CreateScope();
        var configuredTenantId = Guid.TryParse(configuration["Tenant:Id"], out var id) ? id : Guid.Empty;
        await SeedTenantAsync(singleScope.ServiceProvider, configuredTenantId, ct);
    }

    private static async Task SeedTenantAsync(IServiceProvider sp, Guid tenantId, CancellationToken ct)
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DevDataSeeder));
        var context = sp.GetRequiredService<TenantDbContext>();
        var users = sp.GetRequiredService<ITenantUserService>();
        var permissions = sp.GetRequiredService<IPermissionModuleAssignmentService>();
        var personaBaselines = sp.GetRequiredService<IPersonaBaselineService>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var encryption = sp.GetRequiredService<IMfaSecretEncryptionService>();
        var clock = sp.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow();

        // Reset-flow tables are wiped first (dev clean slate): clears any prior
        // tokens, the reset user's previously-redeemed token, and exhausted
        // rate-limit windows so AUTH-4/AUTH-5 start fresh after a restart.
        await context.PasswordResetTokens.ExecuteDeleteAsync(ct);
        await context.PasswordResetRateLimits.ExecuteDeleteAsync(ct);

        // 1) Active, MFA-enrolled user (AUTH-1, AUTH-3).
        var encrypted = await encryption.EncryptAsync(ActiveTotpSecret, ct);
        var activeUserId = await UpsertUserAsync(users, context, new TenantUser
        {
            Username = ActiveEmail,
            PasswordHash = hasher.Hash(ActivePassword),
            IsMfaEnrolled = true,
            MfaSecretEncrypted = encrypted.Cipher,
            MfaSecretKeyRef = encrypted.KeyRef,
            LastUsedTotpStep = null, // clear anti-replay so a freshly computed code validates
            Persona = "P-01",
            Status = UserStatus.Active,
            FailedAttemptCount = 0,
            LockedUntilUtc = null,
            CreatedAt = now,
            UpdatedAt = now,
        }, ct);

        // The active user is P-01 (CX Program Manager) — grant the full DOC-02 module
        // set so the M-10 management UI (User Management, Persona Baselines) is reachable
        // in dev. The login snapshot is built from these assignments, so without them the
        // permission-gated nav stays hidden.
        await permissions.ReplaceAssignmentsAsync(activeUserId, FullModuleAssignments(activeUserId, now), ct: ct);
        await context.SaveChangesAsync(ct);

        // Grant the developer's own admin account (if present) the full module set too —
        // non-destructive: only its permission assignments are touched, never its credentials.
        var admin = await users.GetByUsernameAsync(AdminEmail, ct);
        if (admin is not null)
        {
            await permissions.ReplaceAssignmentsAsync(admin.UserId, FullModuleAssignments(admin.UserId, now), ct: ct);
        await context.SaveChangesAsync(ct);
            logger.LogInformation("[DEV] Granted full module permissions to {Admin}.", AdminEmail);
        }
        else
        {
            logger.LogWarning("[DEV] {Admin} not found — no permissions granted (create the account first).", AdminEmail);
        }

        // 2) Not-yet-enrolled user (AUTH-2). Reset to pending each startup so the
        //    enrollment test — which permanently enrolls this user — can re-run.
        await UpsertEnrollUserAsync(users, context, hasher, now, ct);

        // 3) Reset user (AUTH-4) — the reset-token owner. The test mints a fresh token
        //    for it at run time via /api/v1/dev/fixtures/issue-reset-token.
        await UpsertUserAsync(users, context, new TenantUser
        {
            Username = ResetEmail,
            PasswordHash = hasher.Hash(ResetPassword),
            IsMfaEnrolled = false,
            Persona = "P-01",
            Status = UserStatus.Active,
            FailedAttemptCount = 0,
            LockedUntilUtc = null,
            CreatedAt = now,
            UpdatedAt = now,
        }, ct);

        // 4) Active, MFA-enrolled P-07 (USR-3). Granted only the non-CX modules a Tenant
        //    Administrator may administer, so the User Management UI is reachable while
        //    CX-domain permission rows stay disabled in the editor.
        var p07Encrypted = await encryption.EncryptAsync(P07TotpSecret, ct);
        var p07UserId = await UpsertUserAsync(users, context, new TenantUser
        {
            Username = P07Email,
            PasswordHash = hasher.Hash(P07Password),
            IsMfaEnrolled = true,
            MfaSecretEncrypted = p07Encrypted.Cipher,
            MfaSecretKeyRef = p07Encrypted.KeyRef,
            LastUsedTotpStep = null,
            Persona = "P-07",
            Status = UserStatus.Active,
            FailedAttemptCount = 0,
            LockedUntilUtc = null,
            CreatedAt = now,
            UpdatedAt = now,
        }, ct);
        await permissions.ReplaceAssignmentsAsync(p07UserId, ModuleAssignments(p07UserId, now, P07ModuleIds), ct: ct);
        await context.SaveChangesAsync(ct);

        // 5) Active, MFA-enrolled P-03 (read-only persona) with NO module assignments. Serves two
        //    negative tests: JOUR-2 (M-16) — the persona-gated "Customer Journeys" sidebar entry
        //    must stay hidden (P-03 has no journey-authoring access; P-01/P-02 only); and USR-4 /
        //    PB-2 — a non-manager persona must be denied the UserManagement-gated routes.
        var p03Encrypted = await encryption.EncryptAsync(P03TotpSecret, ct);
        await UpsertUserAsync(users, context, new TenantUser
        {
            Username = P03Email,
            PasswordHash = hasher.Hash(P03Password),
            IsMfaEnrolled = true,
            MfaSecretEncrypted = p03Encrypted.Cipher,
            MfaSecretKeyRef = p03Encrypted.KeyRef,
            LastUsedTotpStep = null,
            Persona = "P-03",
            Status = UserStatus.Active,
            FailedAttemptCount = 0,
            LockedUntilUtc = null,
            CreatedAt = now,
            UpdatedAt = now,
        }, ct);

        // 6) Active, MFA-enrolled P-02 (CX Analyst). Granted the CX module set so the Customer
        //    Journeys + Personas pages stay reachable (M-16 US-3 / T080 proves the P-01-vs-P-02
        //    authority split — P-02 authors journeys but the publish-version + persona-management
        //    controls, gated on persona === "P-01", stay hidden). EXCEPTION: KpiConfiguration is
        //    granted View only (not Manage/Full), so the M-06 [RequirePermission] gate makes KPI
        //    writes 403 while reads succeed — the analyst read-only contract (FR-009 / US-7).
        var p02Encrypted = await encryption.EncryptAsync(P02TotpSecret, ct);
        var p02UserId = await UpsertUserAsync(users, context, new TenantUser
        {
            Username = P02Email,
            PasswordHash = hasher.Hash(P02Password),
            IsMfaEnrolled = true,
            MfaSecretEncrypted = p02Encrypted.Cipher,
            MfaSecretKeyRef = p02Encrypted.KeyRef,
            LastUsedTotpStep = null,
            Persona = "P-02",
            Status = UserStatus.Active,
            FailedAttemptCount = 0,
            LockedUntilUtc = null,
            CreatedAt = now,
            UpdatedAt = now,
        }, ct);
        await permissions.ReplaceAssignmentsAsync(p02UserId, AnalystModuleAssignments(p02UserId, now), ct: ct);

        // 7) Convenience dev login (active@dev.local): seeded CREATE-ONLY — unlike the e2e-*
        //    fixtures it is created on first run and then NEVER overwritten, so any MFA
        //    reset / re-enrollment you do on it survives backend restarts. On a fresh DB it
        //    is created already MFA-enrolled with the deterministic ActiveDevTotpSecret so
        //    login works out-of-the-box; once it exists the seeder leaves its MFA state alone.
        if (await users.GetByUsernameAsync(ActiveDevEmail, ct) is null)
        {
            var activeDevEncrypted = await encryption.EncryptAsync(ActiveDevTotpSecret, ct);
            var activeDevUserId = await UpsertUserAsync(users, context, new TenantUser
            {
                Username = ActiveDevEmail,
                PasswordHash = hasher.Hash(ActiveDevPassword),
                IsMfaEnrolled = true,
                MfaSecretEncrypted = activeDevEncrypted.Cipher,
                MfaSecretKeyRef = activeDevEncrypted.KeyRef,
                LastUsedTotpStep = null, // clear anti-replay so a freshly computed code validates
                Persona = "P-01",
                Status = UserStatus.Active,
                FailedAttemptCount = 0,
                LockedUntilUtc = null,
                CreatedAt = now,
                UpdatedAt = now,
            }, ct);
            await permissions.ReplaceAssignmentsAsync(activeDevUserId, FullModuleAssignments(activeDevUserId, now), ct: ct);
            await context.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "[DEV] E2E auth fixtures seeded: {Active}, {Enroll}, {Reset}, {P07}, {P03}, {P02}, {ActiveDev}).",
            ActiveEmail, EnrollEmail, ResetEmail, P07Email, P03Email, P02Email, ActiveDevEmail);

        // Seed the 8 persona baselines (P-01..P-08) for this tenant so the Persona
        // Baselines page has data. Tolerant: a missing/un-migrated control-plane DB
        // logs a warning rather than crashing dev startup. tenantId is the value
        // ICurrentTenant resolves for this tenant's controllers.
        if (tenantId != Guid.Empty)
        {
            try
            {
                await personaBaselines.SeedDefaultsAsync(tenantId, ct);
                logger.LogInformation("[DEV] Persona baselines seeded for tenant {TenantId}.", tenantId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[DEV] Persona baseline seeding skipped (control-plane DB unavailable or un-migrated).");
            }
        }
        else
        {
            logger.LogWarning("[DEV] Tenant:Id not configured — persona baselines not seeded.");
        }
    }

    /// <summary>
    /// Re-seeds ONLY the AUTH-2 enrollment account back to its pending-enrollment
    /// state (de-enrolled MFA, no lockout/attempt state). Exposed so the dev fixture
    /// endpoint can call it between runs — the enrollment test permanently enrolls
    /// this user, so it needs the account reset to re-run without a backend restart.
    /// </summary>
    public static async Task SeedEnrollUserAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var configuration = services.GetRequiredService<IConfiguration>();

        // Multi-tenant: reset the enrollment fixture in EVERY registered tenant's schema,
        // each in a scope whose RequestCurrentTenant is resolved up front so the scope's
        // TenantDbContext binds to tenant_{slug}. Required because the schema interceptor now
        // fails closed on an unresolved tenant (AD-07 / GP-04) — a fresh, unresolved scope
        // would otherwise throw. Mirrors SeedAsync's per-tenant loop.
        if (configuration.GetValue<bool>(UserManagementServiceCollectionExtensions.EnableMultiTenantFlag))
        {
            var registry = services.GetRequiredService<ITenantRegistry>();
            foreach (var (slug, info) in registry.All)
            {
                using var tenantScope = services.CreateScope();
                tenantScope.ServiceProvider.GetRequiredService<RequestCurrentTenant>().Resolve(info.Id, slug);
                await ReseedEnrollInScopeAsync(tenantScope.ServiceProvider, ct);
            }

            return;
        }

        using var singleScope = services.CreateScope();
        await ReseedEnrollInScopeAsync(singleScope.ServiceProvider, ct);
    }

    private static async Task ReseedEnrollInScopeAsync(IServiceProvider sp, CancellationToken ct)
    {
        var context = sp.GetRequiredService<TenantDbContext>();
        var users = sp.GetRequiredService<ITenantUserService>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var clock = sp.GetRequiredService<TimeProvider>();
        await UpsertEnrollUserAsync(users, context, hasher, clock.GetUtcNow(), ct);
    }

    /// <summary>Upserts the enrollment fixture user in its pending-enrollment state.</summary>
    private static Task UpsertEnrollUserAsync(
        ITenantUserService users, TenantDbContext context, IPasswordHasher hasher, DateTimeOffset now, CancellationToken ct) =>
        UpsertUserAsync(users, context, new TenantUser
        {
            Username = EnrollEmail,
            PasswordHash = hasher.Hash(EnrollPassword),
            IsMfaEnrolled = false,
            MfaSecretEncrypted = null,
            MfaSecretKeyRef = null,
            LastUsedTotpStep = null,
            Persona = "P-01",
            Status = UserStatus.PendingEnrollment,
            FailedAttemptCount = 0,
            LockedUntilUtc = null,
            CreatedAt = now,
            UpdatedAt = now,
        }, ct);

    /// <summary>The 9 DOC-02 modules at full access — the P-01 baseline used for the dev user.</summary>
    private static readonly string[] AllModuleIds =
    [
        "SurveyBuilder", "ChannelManagement", "AudienceManagement", "AnalyticsAndReporting",
        "CaseManagement", "AlertsAndNotifications", "KpiConfiguration", "UserManagement", "TenantConfiguration",
    ];

    /// <summary>The two non-CX modules a P-07 (Tenant Administrator) may administer.</summary>
    private static readonly string[] P07ModuleIds = ["UserManagement", "TenantConfiguration"];

    private static IReadOnlyList<PermissionModuleAssignment> FullModuleAssignments(Guid userId, DateTimeOffset now) =>
        ModuleAssignments(userId, now, AllModuleIds);

    /// <summary>
    /// The CX-Analyst (P-02) grant: every module at full access EXCEPT <c>KpiConfiguration</c>, which
    /// is View-only — so the M-06 [RequirePermission] gate denies KPI writes (no Manage) while reads
    /// succeed (FR-009 / US-7). Other modules stay full so the journey/persona surfaces remain
    /// reachable (M-16 gates its writes on persona === "P-01", not on these modes).
    /// </summary>
    private static IReadOnlyList<PermissionModuleAssignment> AnalystModuleAssignments(Guid userId, DateTimeOffset now) =>
        AllModuleIds.Select(id => new PermissionModuleAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = userId,
            ModuleId = id,
            AllowedModes = id == "KpiConfiguration" ? ["View"] : ["View", "Manage", "Full"],
            AssignedBy = userId,
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();

    private static IReadOnlyList<PermissionModuleAssignment> ModuleAssignments(
        Guid userId, DateTimeOffset now, IEnumerable<string> moduleIds) =>
        moduleIds.Select(id => new PermissionModuleAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = userId,
            ModuleId = id,
            AllowedModes = ["View", "Manage", "Full"],
            AssignedBy = userId,
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();

    /// <summary>Inserts the user, or updates the existing row (preserving its id), so seeding is idempotent.</summary>
    private static async Task<Guid> UpsertUserAsync(
        ITenantUserService users, TenantDbContext context, TenantUser desired, CancellationToken ct)
    {
        var existing = await users.GetByUsernameAsync(desired.Username, ct);
        if (existing is null)
        {
            desired.UserId = Guid.NewGuid();
            await users.AddAsync(desired, ct);
            await context.SaveChangesAsync(ct);
            return desired.UserId;
        }

        desired.UserId = existing.UserId;
        desired.CreatedAt = existing.CreatedAt;
        await users.UpdateAsync(desired, ct);
        await context.SaveChangesAsync(ct);
        return existing.UserId;
    }
}
