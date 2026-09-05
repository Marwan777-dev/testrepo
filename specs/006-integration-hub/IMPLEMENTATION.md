# Implementation Record — M-13 Integration Hub (006-integration-hub)

One consolidated record for this feature: a section per implemented task, documenting what was
built, **why**, the pattern chosen, and how long it took. Sections are appended in task order.

> **Coverage note.** This file was started during the **Phase 4 (US2)** pass on 2026-07-30.
> Phases 1–3 (T001–T042) predate it and are documented only by their annotations in
> [`tasks.md`](tasks.md) and their commit messages (`02a5239`, `2abbeb3`, `493b7da`, `4e60a0f`).
> Backfilling them is a separate task, not attempted here.

---

# Phase 4 — User Story 2: Manage the Parameter Catalogue

**Scope of this pass:** backend only. The user scoped the frontend out (`"without front end"`), so
T061–T064 (SCR-05/06 pages) and T067 (the E2E browser lane) are deferred — see the notes in
`tasks.md`. Commits: `3bd0227` (red baseline), `314fcb5` (implementation).

**Timing granularity:** shell timestamps were captured at the pass boundaries (run start, first test
task, red checkpoint, end), not before/after each individual task. Per-task minutes below are
apportioned within the measured windows and marked accordingly.

---

## T043–T048 — Unit tests for the six US2 rules (written FIRST, must fail)

**Time to implement: ~13 minutes** (measured window 17:05:37 → 17:09:22 for authoring + the red run;
the six files were written in one batch).

### Files created

| File | What it pins |
|---|---|
| `tests/…/Parameters/ApiFieldNameSuggesterTests.cs` | 11 cases — the `snake_case` auto-suggest (FR-S6-02, AC-S6-02) |
| `tests/…/Parameters/ApiFieldNameUniquenessValidatorTests.cs` | 15 cases — VR-F06 across built-in + custom + enabled + disabled |
| `tests/…/Parameters/ApiFieldNameLockGuardTests.cs` | 12 cases — BR-11's lock-on-first-use, built-ins always locked |
| `tests/…/Parameters/RangeConfigValidatorTests.cs` | 15 cases — VR-F07's min < max, both bounds required |
| `tests/…/Parameters/ParameterDisableImpactScannerTests.cs` | 9 cases — BR-10's multi-source reference scan for Dialog D-6 |
| `tests/…/Parameters/BuiltInParameterGuardTests.cs` | 11 cases — BR-09 / `[PO-G27]` delete/rename/retype guard |

### Why these, and why first

The Unit Test Policy requires the tests to exist and **fail for the right reason** before any
production type is written, so `git show <red-commit>` is an audit of what was asserted before the
implementation could shape it. Each file's class-level doc comment states the **contract** the
implementer must satisfy (type name, folder, method signature, purity) — that is what makes the
tests a specification rather than a description written after the fact.

Three decisions inside the tests are load-bearing rather than stylistic:

- **`ApiFieldNameUniquenessValidatorTests` expresses "including disabled" as the *caller's*
  contract.** spec.md's required case reads `Validate(existingFields, field, includeDisabled=true)`,
  but a boolean flag would let a caller pass `false` and quietly break VR-F06. Instead the validator
  is pure and receives the tenant's *whole* field list, so it structurally cannot filter on
  `enabled`. The end-to-end proof that the caller passes everything lives in T065.
- **`ApiFieldNameSuggesterTests` asserts the output always satisfies the DB CHECK.** A suggestion the
  database would reject (`2nd_visit`, `t__caf`) is worse than no suggestion — the user is shown a
  value they cannot save. The regex assertion is what forces the leading-digit strip and the
  underscore collapse into the implementation.
- **`BuiltInParameterGuardTests` includes a field-set guard on the `ParameterAction` enum.** Without
  it, adding an action would silently fall through the `switch` into "allowed" for built-ins. The
  test makes that a compile-visible decision.

### Pattern

Mirrors the US1 (`Channels/`) test layout exactly: `<Subject>_<expected>_when_<condition>` naming,
FluentAssertions, one class per file, static validator instances (they are stateless). Verbatim
assertion of normative console copy (`"Minimum must be less than Maximum"`, `"This API field name is
already in use"`) so a reworded message is a deliberate change, not an accident.

**Alternative considered:** asserting only codes, not messages. Rejected — spec.md ships that copy as
normative, and a code-only test would let the shipped wording drift silently.

---

## T049 — Red Checkpoint 🔴

**Time to implement: ~2 minutes** (part of the 17:05:37 → 17:09:22 window).

`dotnet test tests/Nabadat.IntegrationHub.UnitTests` → **compile failure**, the valid red state per
Unit Test Policy rule 7 when no production type exists:

- `CS0234` — "The type or namespace name 'Parameters' does not exist in the namespace
  'Nabadat.IntegrationHub.Application'" × 6 (one per test file)
- `CS0246` — `ApiFieldNameSuggester`, `ApiFieldNameUniquenessValidator`, `ApiFieldNameLockGuard`,
  `RangeConfigValidator`, `ParameterDisableImpactScanner`, `BuiltInParameterGuard`, `ParameterAction`
- `CS0103` — `ParameterAction` × 6 (the `[Theory]` `InlineData` attributes)

Committed as `3bd0227` **before** any implementation file was opened.

---

## T050 — Parameter DTOs

**Time to implement: ~12 minutes** (apportioned within the 17:09 → 19:47 implementation window).

### Files created

`Application/Parameters/Dtos/` — `ParameterCreateCommand`, `ParameterPatchCommand`, `ParameterDto`,
`ParameterPage`, `ParameterListFilter`, `ParameterOriginCounts`, `ParameterSaveResult`,
`DataScopeContractPayload`, `DataScopeParameterContract`.

The task named two types; seven more were needed because each carries a distinct concern that would
otherwise be smuggled into another type:

- **`ParameterPatchCommand` — every member nullable, `null` = "not submitted".** This is the single
  most load-bearing shape in US2. Three rules depend on being able to distinguish *omitted* from
  *changed*: a locked parameter's read-only form omits `api_field` (BR-11's guard must not read that
  as a rename), a built-in's read-only select omits `data_type` (BR-09's guard is only consulted on a
  real retype), and SCR-05's inline toggle sends nothing but `enabled`. A PUT-shaped command would
  turn every omission into a "change to null" and make all three rules fire spuriously.
- **`ParameterSaveResult` — three outcomes, not two.** Besides success and failure it carries
  `RequiresDisableConfirmation` + `References`, because BR-10's impact warning is a *third* outcome:
  the request succeeded in the sense that nothing is wrong, but the change was deliberately withheld.
  Modelling it as a failure would have forced a 4xx, which misrepresents a warning.
- **`ParameterOriginCounts` separate from `ParameterPage.Items`.** AC-S5-01 requires the tab counts to
  stay **global** while the item list is filtered. Keeping them in a distinct type makes it obvious at
  the call site that the two are computed from different queries.
- **`DataScopeContractPayload` / `DataScopeParameterContract` mirror M-10's records rather than
  referencing them.** The JSON is the published contract; binding M-13's compilation to M-10's
  internal `M13ParameterPayload` type would couple the two modules to each other's refactors for no
  benefit, and would break the day they deploy as separate processes.

### Pattern

Positional `record`s for commands (immutable, value-compared, terse), `record` with `init` properties
for the API contracts (so `[JsonPropertyName]` can hang off each member). Matches the US1 DTO layout.

---

## T051 — `ApiFieldNameSuggester`

**Time to implement: ~10 minutes.**

**File:** `Application/Parameters/ApiFieldNameSuggester.cs`

**What/why:** Derives the `snake_case` API field from the EN name as the user types. Runs on every
keystroke, so it is a single `StringBuilder` pass with no regex and no allocation beyond the builder.

**Function-level rationale:**

- `Suggest(string?)` — applies lowercase → whitespace-to-`_` → strip-invalid in one loop, then trims
  leading non-letters and trailing underscores. The trimming exists purely to guarantee the output
  satisfies `ck_parameters_api_field_format` (`^[a-z][a-z0-9_]*$`); without it "2nd Visit" would
  suggest `2nd_visit`, which the database rejects.
- `Append(builder, separator)` — collapses whitespace runs so `"Average  Handling"` yields
  `average_handling`, not `average__handling`. A private helper rather than a post-pass regex because
  it costs nothing inside the loop already being run.

**Deliberately absent:** transliteration. `"Été"` → `"t"`, not `"ete"`. That is the SRS's ratified
rule, and the suggestion stays editable until BR-11's lock, so a non-Latin name simply gets an empty
suggestion and the user types the key.

---

## T052 — `ApiFieldNameUniquenessValidator`

**Time to implement: ~8 minutes** (including one correction, below).

**File:** `Application/Parameters/ApiFieldNameUniquenessValidator.cs`

**What/why:** VR-F06 — required, `snake_case`, unique per tenant across built-in + custom + enabled +
disabled. Pure: the caller supplies the full existing-field list.

**Correction made during the green pass:** the first implementation *short-circuited* — it returned
the format error and never reached the uniqueness check, so `Validate(["wait_time"], "WAIT_TIME")`
reported only `validation.api_field_format`. The test failed. The fix was to make the two checks
**accumulate**, which is what `ParameterValidationResult`'s own doc comment already promised
("failures accumulate so SCR-06 can render every inline error in a single pass"). The alternative —
relaxing the test to expect the format code — was rejected: it would have hidden a real UX defect
where the user fixes the casing only to be rejected again for a reason they were never told.

**Pattern:** a compiled `Regex` mirroring the DB CHECK, kept adjacent to it in a comment so drift is
visible. `StringComparison.OrdinalIgnoreCase` for the collision test even though the format rule
already forces lower case, so reordering the two checks can never open a hole.

---

## T053 — `ApiFieldNameLockGuard`

**Time to implement: ~7 minutes.**

**File:** `Application/Parameters/ApiFieldNameLockGuard.cs`

**What/why:** BR-11 — the wire key is renameable until the first request carries it, then locked
forever. Deliberately shaped like US1's `ChannelIdLockGuard` so the module's two lock rules read the
same.

**Function-level rationale:**

- `IsLocked(parameter, hasReceivedRequest)` — ORs **three** sources: the persisted
  `ApiFieldLocked` flag, the caller's live traffic probe, and `Origin == BuiltIn`. The probe is
  defence in depth for a parameter with traffic whose flag was never written; the origin check means
  BR-09 does not depend on one boolean column staying correct.
- `ValidateApiFieldChange(parameter, hasReceivedRequest, requested)` — treats `null` and
  value-equal as "no change", which is what lets a locked parameter still save a label edit or a flag
  change. A case-only difference **is** a change (the request pipeline matches the key exactly), so
  `WAIT_TIME` on a locked `wait_time` is rejected.

**Pattern:** pure guard + caller-resolved probe. Keeps the rule unit-testable with no database while
keeping enforcement server-side — a stale client rendering the field editable cannot get around it.

---

## T054 — `RangeConfigValidator`

**Time to implement: ~6 minutes.**

**File:** `Application/Parameters/RangeConfigValidator.cs`

**What/why:** VR-F07 — for `DataType.Range`, Minimum and Maximum are required and `min < max`
strictly (equal bounds describe an empty range).

**The non-obvious half:** the validator also enforces the *inverse* — a **non**-Range type carrying
range configuration is `validation.range_not_applicable`. That mirrors
`ck_parameters_range_only_for_range`. Without it, a client that switches Range → List while leaving
the card populated would hit a raw database constraint violation (a 500) instead of an inline error.

**Pattern:** accumulating errors, one `ParameterValidationError` per failure with its own wire field
name, so the drawer attaches each message to the right input.

---

## T055 — `ParameterDisableImpactScanner`

**Time to implement: ~11 minutes.**

**Files:** `ParameterDisableImpactScanner.cs`, `ParameterReference.cs`, `ParameterReferenceSource.cs`,
`ParameterReferenceKind.cs`

**What/why:** BR-10's reference list for Dialog D-6, spanning three consumer families.

**The architectural decision:** the scanner is **pure**, and takes its candidates as three separate
arguments. BR-10's scan crosses module boundaries — M-13's own `channel_parameter_assignments` plus
M-10 data-scope filters (CMC-06) and M-14/15/16 rules (CMC-07), which are identifier-only references
with **no cross-module foreign key** (Article 4.1). There is no single query that can join them.
`ParameterService` reads each source through its own port and hands the results in; the scanner does
the filtering, kind-stamping, ordering and de-duplication.

**Function-level rationale:**

- `Scan(parameterId, channelContracts, scopeFilters, ruleBuilders)` — the `Kind` is stamped by
  *which argument* a candidate arrived in, which is why `ParameterReferenceSource` carries no kind of
  its own: a caller structurally cannot mislabel a channel contract as a scope filter.
- `Collect(...)` — drops blank names (they would render as empty bullets in D-6) and de-duplicates by
  `(kind, name)` so a parameter reachable through two rows is listed once. Same name under two
  different kinds is kept — those are genuinely two references.
- The `Guid.Empty` early return stops a caller that failed to resolve the parameter from matching
  every unassociated reference.

---

## T056 — `BuiltInParameterGuard`

**Time to implement: ~7 minutes.**

**Files:** `BuiltInParameterGuard.cs`, `ParameterAction.cs`,
`Exceptions/BuiltInParameterViolationException.cs`

**What/why:** BR-09 / `[PO-G27]` — built-ins may only be enabled, disabled, relabelled, and have
their usage flags changed; **no** parameter of either origin may be hard-deleted.

**Why it throws instead of returning a result** — the one place in this sub-domain that breaks the
accumulating-validator pattern, on purpose. These are not correctable field errors: there is no input
to attach a message to and nothing to accumulate, because the operation itself does not exist in the
product. `BuiltInParameterViolationException : InvalidOperationException` satisfies spec.md's
required case while carrying the `Code` the controller maps to 409.

**Pattern note:** the `switch` lists the four permitted built-in actions explicitly before the
`default`, so adding an enum member is a visible decision rather than a silent fall-through — paired
with the field-set unit test.

**One test-file edit was required:** `BuiltInParameterGuardTests.cs` gained
`using …Application.Parameters.Exceptions;`. Article 1A puts exceptions in an `Exceptions/`
sub-folder, hence a sub-namespace. No assertion changed — this is a namespace detail, not a
weakening.

---

## T057 — `IParameterService` / `ParameterService`

**Time to implement: ~45 minutes.** The largest single task in the pass.

**Files:** `Interfaces/IParameterService.cs`, `ParameterService.cs`, `MappingSupportPolicy.cs`,
`ParameterNameValidator.cs`, plus the shared `ParameterValidationResult` / `ParameterValidationError`
/ `ParameterErrorCodes` / `ParameterFields`.

**What/why:** the aggregate. Composes the rules, persists through `ITenantDbContext` (the context
**is** the unit of work — no repository layer, DB-08 / AMENDMENT-007), and owns the transaction
boundary.

**Function-level rationale:**

- **`CreateAsync`** — validates the closed type list first (every rule below switches on the type, so
  it must be a real member), then VR-F13's ceiling, then names/field/range/channels. Reads **every**
  existing `api_field` with no `enabled` filter — that single line is VR-F06's "including disabled".
  Writes the parameter, its channel assignments and the `parameter.created` audit row inside one
  `ExecuteAsync`, then publishes to M-10 *outside* it.
- **`PatchAsync`** — the most intricate path. It (a) consults `BuiltInParameterGuard` **only** for
  changes the client actually asked for, (b) merges submitted-over-stored before validating so an
  omitted field keeps its value, (c) runs BR-10's impact scan *before* any write and returns
  `ConfirmationRequired` without touching the row, and (d) emits both the generic `parameter.updated`
  and the transition-specific `parameter.enabled`/`disabled`, so an auditor can find a status flip
  without diffing payloads.
- **`ListAsync`** — computes the origin counts **before** applying the filters (AC-S5-01: the tab
  counts are global). Materialises the filtered set before slicing the cursor page, which is safe
  and deliberate: VR-F13 caps the tenant at 200 customs + 23 built-ins and BR-09 means no row is ever
  deleted, so the cursor row cannot vanish mid-pagination.
- **`ScanReferencesAsync`** — assembles BR-10's three sources: channel names by a real join, the other
  two through `IExternalParameterReferenceReader` (see T060 / TODO-M13-005).
- **`HasReceivedRequestAsync`** — BR-11's live probe as a server-side `jsonb` key-existence test
  (`EF.Functions.JsonExists`) rather than pulling `parameters_received` payloads into memory. On a
  90-day partitioned log table that difference is the whole cost of the check.
- **`AssignChannels`** — FR-S6-05: a pill adds the parameter as *supported* with the required-default
  applied. BR-08 keeps the channel's own contract row authoritative at request time, so this is only
  a seed.
- **`MappingSupportPolicy.Resolve`** — BR-27 extracted to its own type because three call sites need
  it (create, patch, and the wire projection's `MappingSupportChangeable`). The submitted flag is an
  *input*, never the stored value: a contradicting client is corrected, not rejected, because the
  server owns the rule.

**`ParameterNameValidator` was not assigned by any task** — VR-F05 (names required, ≤ 50 chars) had no
owner in tasks.md. Added here rather than left to the database CHECK, so a long name is an inline
error and not a 500. Noted in tasks.md's T057 annotation.

**Pattern:** returned-not-thrown validation outcomes (`ParameterSaveResult`), one transaction per
write spanning row + assignments + audit, injected `TimeProvider` for every timestamp (no
`DateTime.UtcNow` anywhere in the module).

---

## T058 — `ParametersController` + API contracts

**Time to implement: ~30 minutes.**

**Files:** `Api/Controllers/ParametersController.cs`; `Api/Contracts/` — `CreateParameterRequest`,
`PatchParameterRequest`, `ParameterResponse`, `ParameterListResponse`, `ParameterCountsResponse`,
`ParameterReferenceResponse`, `PatchParameterResponse`; `Domain/ValueObjects/ParameterWireValues.cs`.

**What/why:** `GET` (list, AND-combined filters + global counts), `GET /{id}`, `POST`, `PATCH`. **No
`DELETE` route** — BR-09's enforcement *is* the absence of the route; `BuiltInParameterGuard` is the
second line of defence behind it.

**Two decisions worth recording:**

1. **BR-10's wire shape, resolved.** contracts/api-endpoints.md explicitly left the choice open
   ("response-includes-list vs. a required `confirm=true` re-call"). Chosen: a disable on a referenced
   parameter returns **200** with `requires_confirmation: true` + `references[]` and leaves the
   parameter **unchanged**; the client re-sends with `confirm_disable: true`. It is not a 4xx because
   BR-10 calls for a *warning*, not a rejection — a failure status would misrepresent it to any client
   that only checks `response.ok`. `PatchParameterResponse` documents both 200-shaped outcomes so a
   client can tell "withheld" from "applied, and here is what it touched".
2. **`ParameterWireValues` promoted to Domain.** The host registers no `JsonStringEnumConverter`
   (verified by grep), so a bare enum would serialise as `9` instead of `"date_time"` and couple the
   console to C# member ordering. Api and Infrastructure both need that mapping and **may not
   reference each other** (Article 1A), so the table would otherwise be written twice — the exact
   drift risk where a stored `date_time` starts being returned as something else. The two EF
   `ValueConverter`s now delegate to it. `TryParse*` returns `false` rather than throwing, because an
   inbound `"duration"` (`[PO-G17]`: evaluated and rejected) is a client error to report as 400, not
   an unhandled exception; the EF read path keeps throwing, since an unrecognised literal in the
   *column* means the CHECK and the enum have diverged and must fail loudly.

**Pattern:** identical to `ServiceChannelsController` — `[Authorize]` only (T146 applies the
Permissions Matrix across all M-13 controllers in one pass), actor from `ISessionContextAccessor`
passed down on the command, API-05 envelope with one `details[]` entry per accumulated failure, and a
`Failure(...)` helper that picks the lead error by severity (404 → 409 → 400).

---

## T059 — The real M-10 data-scope integration

**Time to implement: ~28 minutes.**

**Files:** `Infrastructure/UserManagementIntegration/DataScopeHttpClient.cs`,
`Application/Parameters/DataScopeContractPublisher.cs`,
`Application/Parameters/Interfaces/IDataScopeContractClient.cs`.

**What/why:** BR-10's forward half — M-10's data-scope filters are built on M-13's parameter
definitions and value sets. M-10's `M13ParameterContractAdapter` and its endpoint already exist
(research.md §4.1); this makes M-13 the caller. **Not a stub.**

**Function-level rationale:**

- **`DataScopeContractPublisher.BuildAsync`** — selects parameters that are enabled, filterable or
  mapping-enabled, **and** have a non-empty value set from the mapping table. The last condition is
  not optional: M-10 rejects a definition with empty `allowedValues` and fails the **entire** payload
  on one bad row, so including a value-less parameter would silently strand every other parameter's
  push. It also filters M-10's reserved names locally for the same reason — none of the 23 built-ins
  collide (checked against the seed, closing research.md §4.1's reconciliation task), but a tenant is
  free to create a parameter called `persona`.
- **`PublishAsync`** — sends the tenant's *full* qualifying set, not a delta, because M-10 upserts by
  name and has no notion of a partial update; that also makes the push self-healing after a failure.
  Chunks at 500 (M-10's ceiling). **Catches and logs rather than propagating** — the push is a
  projection of data M-13 already owns and is invoked *after* the commit, so an unreachable M-10 must
  not make the console's Create button fail.
- **`DataScopeHttpClient`** — HTTP rather than an in-process call even though M-10 is
  project-referenced. The endpoint is M-10's *published* integration surface; calling
  `M13ParameterContractAdapter` directly would bypass M-10's own controller validation, bind M-13 to
  another module's Application-layer type (forbidden), and break the day the two deploy separately
  (AD-05). It sets its JSON naming policy explicitly because it serialises for *another service's*
  contract, and it carries M-10's rejection body into the exception message — with wholesale batch
  rejection, the body is the only way to tell which rule tripped.

**Pattern:** policy in Application (`DataScopeContractPublisher`), transport behind a port
(`IDataScopeContractClient`) implemented in Infrastructure. Named `HttpClient`
(`DataScopeHttpClient.ClientName`) so the integration lane can retarget exactly this client without
relying on the type-name default.

---

## T060 — Composition root wiring

**Time to implement: ~8 minutes.**

**Files:** `IntegrationHubServiceCollectionExtensions.cs`,
`Domain/Interfaces/IExternalParameterReferenceReader.cs`,
`Infrastructure/CrossModule/NullExternalParameterReferenceReader.cs`.

**What/why:** the US2 block, shaped like the US1 one — stateless rules as singletons registered as
their **concrete** types (they are internal composition parts of `ParameterService`, not mock seams;
`IParameterService` is the seam), the aggregate scoped.

**The new port:** BR-10's external half has **no provider**. M-10 publishes only a forward per-user
scope read — `IDataScopeService` cannot answer "which scope filters reference parameter *P*?" — and
M-14/15/16 do not exist. `IExternalParameterReferenceReader` binds to an empty adapter, so the impact
warning is **complete for channel contracts and silent for the other two kinds**. Returning empty
rather than throwing is deliberate: BR-10's warning is informational and does not gate the operation,
so a throwing reader would take the whole disable flow down for a dependency that has no provider
yet. Recorded as **TODO-M13-005** with literal resume instructions.

**Base address is not defaulted.** In a single-host deployment it points back at the same host, in a
split deployment at M-10's service; guessing either would produce a silently-wrong push. Unset → the
client throws → the publisher logs → the tenant's catalogue is unaffected. A missing projection that
is visible in the log beats a console write that fails for a downstream reason.

---

## T065 — Parameter endpoint integration tests 🐳

**Time to implement: ~35 minutes.**

**File:** `tests/…/Endpoints/ParametersEndpointTests.cs` — 22 tests, all green against a real
Testcontainers Postgres.

Covers spec.md's listed cases plus, beyond them: both BR-27 branches (List forced on server-side
against a contradicting client value; text user-changeable), duplicate-against-a-**built-in**,
`[PO-G17]`'s rejected `duration` type, FR-S6-05's supported-with-required-default channel pill, the
**withheld-then-confirmed** BR-10 disable pair, BR-09's editable built-in display names and flags,
BR-11's **live** lock probe driven purely by a logged request, an unlocked custom rename, 404, the
AND-combined filter with global tab counts, the 23-built-ins-all-enabled baseline check, and
search-by-api-field.

**The assertion that matters most:** in
`PATCH_parameters_withholds_the_disable_and_returns_the_reference_list_when_a_channel_uses_it`, the
test asserts the parameter is **still enabled** after the call and that storage was not touched. AC-S5-02
says the warning lists the reference "before anything changes" — asserting only the response body
would pass even if the change had been applied.

**Hygiene:** every parameter takes a unique API field, because this lane writes real rows and never
rolls back. VR-F13's 200-custom ceiling makes the shared container a slow-growing hazard —
TODO-M13-004.

---

## T066 — Real M-10 call, end-to-end 🐳

**Time to implement: ~22 minutes** (including the fixture work).

**Files:** `tests/…/Endpoints/DataScopeContractPublisherTests.cs` (4 tests, green), plus
`IntegrationHubApplicationFactory` gaining `SeedParameterMappingAsync`, `GetDataScopeDefinitionAsync`,
and the outbound-client redirection.

**What/why:** spec.md offered two shapes ("a row in `data_scope_parameter_definitions` … **or** a
captured HTTP call"). The **first** was taken. The fixture points M-13's outbound `HttpClient` at the
same in-memory test server via `ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler())`, so
the request traverses M-10's **real** controller, its real validator, and its real persistence — and
the assertions read the resulting rows. A captured-call assertion would only have proved that M-13
called *something*, which is precisely what research.md §4.1 says not to settle for, given M-10's side
is already built.

The handler factory is a lambda so `Server` is resolved lazily — the `HttpClient` is first built
during a request, by which time the host exists.

**The four tests:** the value set + label + `source_module` land in M-10; a parameter with no mapped
values is correctly withheld until it has one; M-10's reserved `persona` is filtered locally **while
the rest of the batch still arrives** (a two-sided assertion — the point is that one bad name does not
strand everything else); a disabled parameter drops out of the next push.

**Known limit, stated in the test:** M-10's endpoint only upserts and has no removal path, so a
previously-pushed definition for a now-disabled parameter stays stale in M-10 until that changes. The
test asserts what M-13 controls — its own outbound set.

---

## Phase 4 checkpoint

| Gate | Result |
|---|---|
| `dotnet test tests/Nabadat.IntegrationHub.UnitTests` | **126 passed**, 0 failed (44 US1 + 82 US2) |
| `dotnet test tests/Nabadat.IntegrationHub.IntegrationTests` | **41 passed**, 0 failed (15 US1 + 26 US2) |
| `dotnet build Nabadat.TenantAdmin.sln` | 0 errors |
| `npm run build` | **not run** — frontend deferred (T061–T064) |
| E2E `ParameterCatalogueTests` | **not authored** — deferred (T067) |
