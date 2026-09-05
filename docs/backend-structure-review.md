# Backend Structure & Conventions Review — M-10

> Scope: **code structure, organization, and conventions** of the .NET backend
> (`src/Nabadat.Platform.M10`, `src/Nabadat.TenantAdmin`). This review is about
> *how the code is organized and written*, not runtime behavior/functionality.
>
> Status: proposed backlog. Each item is independently ticketable.
> Effort: **S** ≈ hours · **M** ≈ 1–2 days · **L** ≈ 3+ days.

---

## Recommended execution order

Ordered so that foundational moves land first and later items drop into the right
place (rather than being moved twice).

| # | ID | Item | Effort | Risk |
|---|----|------|--------|------|
| 1 | [BR-11](#br-11) | Split layers into class libraries (enforce Clean Architecture isolation) | L | Med |
| 2 | [BR-12](#br-12) | Project / assembly renaming convention | S | Low |
| 3 | [BR-13](#br-13) | Replace raw SQL with EF Core | L | High |
| 4 | [BR-08](#br-08) | `BaseRepository` for connection handling | M | Med |
| 5 | [BR-01](#br-01) | Central error handling | M | Med |
| 6 | [BR-02](#br-02) | Base controller class (remove repeated controller functions) | S | Low |
| 7 | [BR-07](#br-07) | Encapsulate duplicated helpers (`GenerateToken`, …) | S | Low |
| 8 | [BR-04](#br-04) | Enums/constants for personas & error codes | M | Low |
| 9 | [BR-05](#br-05) | Enum naming convention (`…Enum` suffix) | S | Low |
| 10 | [BR-09](#br-09) | Move app parameters to `appsettings` (IOptions) | M | Low |
| 11 | [BR-03](#br-03) | DTO mapping out of controllers | M | Low |
| 12 | [BR-10](#br-10) | Remove or wire up FluentValidation | S | Low |
| 13 | [BR-06](#br-06) | Enforce per-layer folder structure | S | Low |

**Two natural batches:**
- **Foundation (1–3):** project topology + data-access strategy. Do these first; they
  decide *where* everything else lives.
- **Cleanup (4–13):** dedup, single-source-of-truth, conventions. All land cleanly once
  the project topology exists.

---

## Architecture & topology

### <a id="br-11"></a>BR-11 — Layers are folders, not class libraries (no enforced isolation)

**Problem.** `Api/`, `Application/`, `Domain/`, `Infrastructure/` are folders inside one
project. Nothing stops a controller from `new`-ing a repository or `Domain` from
referencing Npgsql — the dependency direction of Clean Architecture is a *convention*,
not a *constraint*.

**Fix.** Split into separate class libraries so the compiler enforces the inward-only
dependency graph:

```
Nabadat.Platform.Common              → (kernel: error envelope, token helper, base types)
Nabadat.Platform.M10.Domain          → Common
Nabadat.Platform.M10.Application     → Domain, Common
Nabadat.Platform.M10.Infrastructure  → Application, Domain, Common
Nabadat.Platform.M10.Api             → Application, Domain, Common   (+ AspNetCore)
Nabadat.Platform.M10.Bootstrap       → all of the above  (composition root: AddM10Module)
```

Notes:
- A child `.csproj` cannot be physically nested inside another (SDK glob `**/*.cs`
  collides) — use **sibling folders** + **solution folders** for the visual tree.
- Keep `<RootNamespace>` per project so namespaces are unchanged → this is move-files +
  add-csproj, **not** a code rewrite.
- `AddM10Module` wires interfaces → Infrastructure concretes, so it belongs in
  `Bootstrap` (composition root), keeping `Api` free of an Infrastructure reference.
- Controllers are still discovered as `ApplicationPart` from the referenced `Api` assembly.

**Effort:** L · **Risk:** Med (build graph churn; no logic change)

---

### <a id="br-12"></a>BR-12 — Project / assembly naming convention

**Problem.** No documented scheme for project/assembly names, and types carry a redundant
`M10` prefix inside the already-`M10` namespace (`M10Event`, `M10UnitOfWork`,
`M10AuthService` → `Nabadat.Platform.M10.Application.Events.M10Event`).

**Effort:** S · **Risk:** Low

---

### <a id="br-13"></a>BR-13 — Replace raw SQL with EF Core

**Problem.** All repositories are hand-rolled `NpgsqlCommand` with manual column lists and
`Map(reader)` projections (e.g. `TenantUserRepository`, `PermissionRepository`,
`DataScopeRepository`). `Npgsql.EntityFrameworkCore.PostgreSQL` is **already referenced**
in the `.csproj` but no `DbContext`/`DbSet`/`UseNpgsql` exists — EF Core is pulled in and
unused.

**Fix.** Introduce a `DbContext` + entity configurations and migrate the repositories to
EF Core (or LINQ-to-entities). Preserve:
- the transactional unit-of-work boundary (the audit-write must stay atomic with the
  business write — currently `IM10UnitOfWork`),
- raw SQL only where genuinely needed (keyset pagination, advisory locks) via
  `FromSqlInterpolated`/`ExecuteSql`.

> Sequencing note: do **after** BR-08 (`BaseRepository`) is irrelevant if EF Core replaces
> the connection plumbing entirely — if BR-13 is approved, fold BR-08 into it rather than
> doing both. If raw SQL is retained for some repos, BR-08 still applies to those.

**Effort:** L · **Risk:** High (data-access rewrite; needs full integration-test pass)

---

## Cross-cutting: deduplication

### <a id="br-08"></a>BR-08 — Repeated `GetConnectionString` + connection handling → `BaseRepository`

**Problem.** `configuration.GetConnectionString("TenantDb") ?? throw new InvalidOperationException(...)`
is duplicated in **10 places** (8 repositories + `M10UnitOfWork` + `DevDataSeeder`), and
every repo re-implements the same "open my own connection vs join the caller's
transaction" pattern.

**Fix.** An `ITenantConnectionFactory` (resolves + validates the connection string once)
and a `BaseRepository` (or shared helper) for the open/transaction dance. (Superseded by
BR-13 if EF Core is adopted.)

**Effort:** M · **Risk:** Med

---

### <a id="br-01"></a>BR-01 — No central error handling

**Problem.** No exception-handling middleware / `IExceptionHandler`. Each of the 5
controllers maps domain exceptions → HTTP codes in its own `try/catch`, and the
`ApiErrorEnvelope` is built ad-hoc. The envelope's `tenant_id` field is never populated
and `correlation_id` is just `HttpContext.TraceIdentifier`.

**Fix.** A single `IExceptionHandler` (.NET 8+) or middleware that maps
`ForbiddenException → 403`, `KeyNotFoundException → 404`, `MfaValidationException → 422`,
etc., and emits the API-05 envelope once (populating `tenant_id` from `ICurrentTenant` and
a real correlation id). Controllers keep only the happy path.

**Effort:** M · **Risk:** Med

---

### <a id="br-02"></a>BR-02 — Repeated controller functions (base controller missing)

**Problem.** Two helpers are copy-pasted across controllers:
- `private ObjectResult Error(int status, string code, string message)` — in **all 5**
  controllers.
- `private Task<IActionResult> InvokeAsync(Func<SessionContext, …>)` (session→401 guard +
  `ForbiddenException`→403) — duplicated in `UsersController` and `AuditLogController`
  with subtly divergent `catch` tails.

**Fix.** `ApiControllerBase : ControllerBase` carrying the shared `Error(...)` and the
session-guard wrapper. With BR-01 in place, most of the wrapper's `catch` logic moves to
the global handler, leaving the base class thin.

**Effort:** S · **Risk:** Low

---

### <a id="br-07"></a>BR-07 — Duplicated helpers not encapsulated (`GenerateToken`, …)

**Problem.** `GenerateToken()`, `HashToken()`, and `Base64UrlEncode()` are byte-for-byte
duplicated in three services: `SessionService`, `UserManagementService`,
`PasswordResetService`. (Also: per-service private `M10Event` factory methods repeat the
same `CorrelationId = Guid.NewGuid()` / timestamp plumbing ~12 times.)

**Fix.** A single `SecureToken` helper (in `Nabadat.Platform.Common`) for token
generation/hashing, and an `IAuditEventFactory` that stamps time (via injected
`TimeProvider`) and correlation id, so services pass only meaningful fields.

**Effort:** S · **Risk:** Low

---

## Single source of truth

### <a id="br-04"></a>BR-04 — No enums/constants for personas & error codes

**Problem.** Magic strings everywhere:
- Personas `"P-01"`…`"P-08"` in 7+ files as inline literals, `const`s, and arrays — and
  the **full persona list is defined twice, identically** (`PersonaBaselineService` and
  `PersonaBaselineRepository`).
- 23 error-code string literals (`"auth.invalid_credentials"`, `"users.not_found"`, …)
  across the controllers, with no registry.
- Module ids (`"SurveyBuilder"`, …) duplicated between `DataLayerAuthorizationGuard` and
  `PersonaBaselineRepository`.

**Fix.** Single sources of truth in `Domain`:
- `Personas` — `const string` members + one canonical `All`/`CxManagers` set. (Prefer
  `const string` over a real enum: these cross DB/wire as `"P-01"`, avoiding the
  int-serialization trap.)
- `ErrorCodes` — the envelope codes.
- `ModuleIds` — module names + the canonical `CxDomainModules` set.

**Effort:** M · **Risk:** Low

---

### <a id="br-05"></a>BR-05 — Enum naming convention (`…Enum` suffix)

**Problem.** Existing enums do not follow the team's chosen `…Enum` suffix convention:
`CreateUserOutcome`, `CredentialOutcome`, `IdentityProviderType`, `PermissionMode`,
`UserStatus`.

**Fix.** Rename to the agreed convention (`UserStatusEnum`, `PermissionModeEnum`, …) and
record the rule in the conventions doc.

> Note for the decision-maker: this is the **opposite** of Microsoft's framework design
> guidelines (which recommend *no* `Enum` suffix). It's a valid team choice — just adopt
> it deliberately and apply it uniformly, since it diverges from the BCL norm.

**Effort:** S · **Risk:** Low

---

### <a id="br-09"></a>BR-09 — Hardcoded app parameters → `appsettings` (IOptions)

**Problem.** Policy/tunable values are private `const`s with no configuration path:
- lockout `MaxFailedAttempts = 5`, `LockoutDuration = 15min` (`AccountLockoutService`)
- rate limit `MaxRequests = 3`, `Window = 30min` (`PasswordResetRateLimiter`)
- session `DefaultSlidingTtlMinutes = 60`, `AbsoluteLifetime = 24h` (`SessionService`)
- reset-token lifetime **`30min` defined twice** (`UserManagementService` +
  `PasswordResetService`)
- token lengths, bcrypt work factor, pagination defaults (`50`/`200` in 3 places).

**Fix.** Bind an `M10Options` (sections: Lockout, RateLimit, Session, Tokens, Paging) from
`appsettings.json` via `IOptions<>`. Ops can tune without recompiling; duplicated values
collapse to one.

**Effort:** M · **Risk:** Low

---

## Mapping & validation

### <a id="br-03"></a>BR-03 — DTO mapping written in controllers

**Problem.** Entity→DTO mapping is hand-written inside controllers (`ToSummary`,
`ToSessionToken`, inline `new UserDetailResponse { … }`, plus `ParseJson` jsonb
re-hydration in `AuditLogController`). Mapping rules are scattered and bloat the controller.

**Fix.** Either co-locate mapping as `static` extension methods next to each DTO
(`UserSummaryResponse.From(TenantUser)`), or adopt **Mapster** (MIT). Avoid AutoMapper —
it went commercial in 2025. Recommendation: explicit extension methods (zero dependency,
debuggable) given the small DTO surface.

**Effort:** M · **Risk:** Low

---

### <a id="br-10"></a>BR-10 — FluentValidation imported but unused

**Problem.** `FluentValidation.AspNetCore` is referenced in the `.csproj`, but there are
**no** `AbstractValidator`/`IValidator<>` types — a dead dependency. Request validation is
instead done ad-hoc deep in services (e.g. `IsValidEmail` inside
`TenantAuthenticationService`), and the same rule can be enforced inconsistently in two
layers (page-size is *rejected* in `AuditLogController` but silently *clamped* in
`TenantUserRepository`).

**Fix.** Decide: either **wire it up** (validators per request DTO at the API boundary,
feeding the envelope's `details[]`) or **remove the package**. If kept, move scattered
validation (email format, page-size bounds) into validators so the rule lives once.

**Effort:** S · **Risk:** Low

---

## Governance

### <a id="br-06"></a>BR-06 — No enforced per-layer folder structure

**Problem.** Folder taxonomy is inconsistent. `Api/` is flat — controllers, interfaces
(`ISessionContextAccessor`, `ICurrentTenant`), middleware, and accessors all sit together;
only `Contracts/` is foldered. Interfaces live in three different conventions
(`Domain/Interfaces`, `Application/*/Interfaces`, and loose in `Api/`).

**Fix.** Adopt and document a taxonomy per layer, e.g.:
```
Api/  → Controllers/  Middleware/  Contracts/  Abstractions/  Enums/
```
Add an `.editorconfig` (also fixes inconsistent `using`-block grouping) and a short
"backend structure" rules section so new files are held to it in review. After BR-11 the
layer boundaries are project boundaries; this rule governs the *intra-layer* folders.

**Effort:** S · **Risk:** Low

---
