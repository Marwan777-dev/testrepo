# TODO — Project-Wide Deferred & Gap Tracker

Single ledger across every module/spec in this repo. Do not create per-feature TODO files —
everything lands here so cross-module blockers stay visible in one place.

## ID scheme

`TODO-{MODULE}-{NNN}` — module code comes from the `**Module**` line in the `tasks.md` header
of whatever spec is being implemented (e.g. `M-01`, `M-04`, `M-11`). `NNN` is a 3-digit counter,
per module, never reused even if an entry is later resolved.
Example: the 4th tracked item ever raised against module M-06 is `TODO-M06-004`.

## Entry types

- **DEFERRED** — a specific future task or external dependency will complete this. Must name
  that task/dependency and carry literal resume instructions.
- **GAP** — something is incomplete, skipped, or missing right now, with no task assigned
  to fix it. Exists purely so incompleteness is visible and doesn't get silently forgotten.
  Requires human triage to become either a real task or a `DEFERRED` entry with an owner.

## Status values

`OPEN` → `READY` (blocker landed, not yet actioned) → `RESOLVED`.
A `GAP` entry has no `READY` state — it goes straight `OPEN` → `RESOLVED` once triaged and fixed,
or `PROMOTED` if it gets converted into a real backlog task (record the task ID it became).

---

## Triage — remaining OPEN issues by action needed (derived view, 2026-07-20)

A prioritised grouping of every OPEN entry by the kind of action it needs. This is a convenience
index — the authoritative detail is in each entry below.

### A. Actionable now inside M-01 (no external dependency — code you can write today)

*(none — all section-A items resolved)*

### B. Needs a product / spec / design decision before coding

| ID | One-line | Owner to consult |
|----|----------|------------------|
| [TODO-M13-006](#todo-m13-006--gap) | FR-GBL-03's unsaved-changes guard — SCR-04 + SCR-06 now ship it; **SCR-02's wizard still has no owning task** | Spec owner + frontend lead — extend T085 or add one US9 task |
| [TODO-M13-003](#todo-m13-003--gap) | No task owns `integration_request_logs` partition roll-forward + NFR-8 90-day retention detach | Deployment-topology owner — BackgroundService vs pg_cron vs deploy cron |
| [TODO-M13-004](#todo-m13-004--gap) | M-13's shared integration-test container accumulates channels toward VR-F13's 100 ceiling | Backend lead — add a fixture reset helper before the suite outgrows the cap |
| [TODO-M01-004](#todo-m01-004--gap) | "Reason follow-up" + "Allow N/A" KPI-question fields have no task | Spec owner — confirm descope or add task |
| [TODO-M01-005](#todo-m01-005--gap) | Languages beyond EN/AR + AI auto-translation | Spec owner — new FR/BR entries |
| [TODO-M01-008](#todo-m01-008--gap) | `SurveyDefinition` published-record field shape undefined | M-04 owner — pin shape at T144 |
| [TODO-M01-010](#todo-m01-010--gap) | Design-system CSS class-token allowlist for sanitised HTML | Frontend lead — supply token list |
| [TODO-M01-024](#todo-m01-024--gap) | No report visual defined for `Matrix` / `Ranking` question types | Spec owner — extend FR-13.3 |
| [TODO-M01-025](#todo-m01-025--gap) | No source for KPI gauge `target` values | Decide M-06 interface method vs ES denormalisation |
| [TODO-M01-009](#todo-m01-009--gap) | Distributed backing for the idempotency replay store | Platform/deployment owner — Redis vs Postgres decision |

### C. Blocked on another module shipping (cannot act until then)

| ID | One-line | Blocked on |
|----|----------|-----------|
| [TODO-M13-005](#todo-m13-005--deferred) | BR-10's impact warning is silent for M-10 scope filters and M-14/15/16 rules | M-10 adding a reverse "who references parameter P?" lookup; M-14/15/16 shipping |
| [TODO-M01-001](#todo-m01-001--deferred) | Actual response purge on Return-to-Draft | M-04 `IResponsePurgeService` (T021) |
| [TODO-M01-002](#todo-m01-002--deferred) | Emit `survey.responses.purged` audit event | Constitution AMENDMENT-012 ratification |
| [TODO-M01-006](#todo-m01-006--deferred) | Consume M-11 `ITenantSettingsReader` / design-guidelines readers | M-11 |
| [TODO-M01-011](#todo-m01-011--deferred) | Real M-17 audit emission for status events | M-17 host wiring (T020) |
| [TODO-M01-012](#todo-m01-012--deferred) | Real `IJourneyReader` / `IKpiCatalogReader` / `IChannelSurveyRulesReader` | M-16 / M-06 / M-02 |
| [TODO-M01-014](#todo-m01-014--deferred) | Real `IPermissionChecker` / `INotificationDispatcher` | M-10 / M-09 |
| [TODO-M01-017](#todo-m01-017--deferred) | Live ES read of per-question response counts | M-04 (`Nabadat.ResponseCollection`) — owns + populates the `tenant_{id}_analytics` projection |
| [TODO-M01-023](#todo-m01-023--deferred) | Survey Report live read + native ES aggregations (scale) | M-04 populates `tenant_{id}_responses`; native aggs coupled to M-04's index **mapping** |
| [TODO-M01-026](#todo-m01-026--deferred) | Survey Analytics live read + native date-histogram aggs (scale) | M-04 populates the `tenant_{id}_analytics` funnel; native aggs coupled to M-04's **mapping** |

### D. Blocked on infra / environment (Elasticsearch in prod) or a scale decision

*(none — the three ES read/scale items moved to C on 2026-07-20: their hard blocker is M-04 shipping, not infra. Local dev ES is now configured; prod `Elasticsearch:Uri` config and the native-aggregation scale rewrite remain as sub-points **inside** those C entries, since the rewrite cannot be finalised without M-04's index mapping.)*

---

## OPEN

### TODO-M13-003 — GAP

- **Module**: M-13 (Nabadat.IntegrationHub)
- **Found during task**: T009 (`IntegrationHub_Baseline.sql`, Phase 2 Foundational)
- **What's missing**: Ongoing partition maintenance and retention enforcement for `integration_request_logs`. The table is DB-04 monthly-partitioned as specified, and T009's baseline creates partitions for the previous 3 through the next 12 months plus a `DEFAULT` partition (verified against a real Postgres 16: a `now()` insert routed to `integration_request_logs_2026_07`). But **no task in `specs/006-integration-hub/tasks.md` (T001–T218) owns (a) rolling new monthly partitions forward, or (b) NFR-8's 90-day retention, which DB-04 requires be enforced by DETACHING old partitions rather than row-level `DELETE`s.** Two consequences: ~13 months after provisioning, new request-log rows fall into the `DEFAULT` partition; and log rows are retained forever, contradicting NFR-8. There is also a known Postgres caveat — once rows for month M have landed in `DEFAULT`, attaching a real partition for M fails until those rows are moved (documented in a comment in the baseline).
- **Why not fixed now**: T009's scope is the DDL, and a scheduled maintenance job is neither DDL nor owned by any user story — it is platform/operations work (a `BackgroundService`, a pg_cron job, or a deployment cron), and the choice depends on the deployment topology (Kubernetes SaaS vs. Docker Compose on-prem, AD-05). The `DEFAULT` partition is a deliberate safety net so the absence of the job can never fail an inbound request.
- **Suggested next step**: Add a task (M-13 Infrastructure, or platform-level) to roll partitions forward monthly and detach + drop partitions older than 90 days, then assert NFR-8 in an integration test. Decide the mechanism with whoever owns the deployment topology. Until then, treat the 12-month partition lead as the operational deadline.
- **Added**: 2026-07-30

### TODO-M13-004 — GAP

- **Module**: M-13 (Nabadat.IntegrationHub)
- **Found during task**: T041 (`ServiceChannelsEndpointTests.cs`, US1 integration lane)
- **What's missing**: A way to reset M-13 fixture state between integration-test classes. The lane shares one Testcontainers Postgres for the whole run (`IntegrationHubIntegrationCollection`) and, per CLAUDE.md's E2E/integration convention, does **not** roll back — every test's writes are real, permanent rows. Meanwhile `ServiceChannelService` enforces VR-F13's ceiling of **100 service channels per tenant** on the create path. US1 alone leaves roughly 10 channels behind per run, which is comfortably clear of the cap; but US2–US10 add eight more test classes to the same container, and the channel count is cumulative across every class in the run. Once the suite crosses 100, `POST .../service-channels` starts returning `400 validation.capacity_exceeded` and the create-path tests fail for a reason that has nothing to do with the code under test — a slow-growing, order-dependent failure that will look like a flake. The same shape applies to VR-F13's other two ceilings (200 integrations, 200 custom parameters) as US3 and US2 land.
- **Why not fixed now**: The current count is far below the ceiling, so there is nothing broken to fix yet, and the right shape of the fix depends on how many channels the later stories actually seed — a per-class truncate, a per-run truncate in the fixture's `InitializeAsync`, or a schema-per-class factory. Guessing now would add fixture machinery that may be the wrong machinery. Note the fix belongs in the **test fixture only**: BR-07 forbids a delete operation in the product, and adding one to satisfy tests would be a spec violation — the fixture may issue raw `TRUNCATE`/`DELETE` SQL because that is arranging test state, not a product capability.
- **Also affects the E2E tenant (noted 2026-09-02, T042)**: the same cumulative-write shape applies to the shared `e2e` tenant schema the browser lane drives, which was already carrying **73 service channels** (58 active) from earlier runs when `ServiceChannelTests` landed — 27 short of the same 100 ceiling. `ServiceChannelTests` mitigates its own share by tearing down every channel it seeds or creates in `[TestCleanup]` (`E2ETenantDb.DeleteServiceChannelAsync` / `…ByChannelIdAsync`), so it is net-zero per run; the pre-existing 73 are leftovers from runs before that cleanup existed and still need a one-off prune. Later M-13 E2E files must follow the same teardown discipline or the browser lane hits `validation.capacity_exceeded` first.
- **Suggested next step**: Before the first story that pushes the run past ~60 channels (US3 is the likely one — it seeds a channel per integration test), add a `ResetIntegrationHubStateAsync()` helper to `IntegrationHubApplicationFactory` that truncates `channel_parameter_assignments`, `integration_request_logs`, `credentials`, `integrations`, and `service_channels` (leaving `parameters` intact so the 23 seeded built-ins survive), and call it from each endpoint test class's arrange step. Add an assertion that the built-in count is still 23 afterwards, so a careless truncate of `parameters` fails loudly instead of silently breaking every later story's contract tests.
- **Added**: 2026-07-30

### TODO-M13-006 — GAP

- **Module**: M-13 (Nabadat.IntegrationHub)
- **Found during task**: T037 (`ServiceChannelForm.tsx`, SCR-04, US1)
- **What's missing**: **FR-GBL-03 — the unsaved-changes guard — is owned by no task.** The spec requires it on **three** screens ("SCR-02 (wizard), SCR-04 (channel editor), and SCR-06 (parameter drawer) — prompts before discarding unsaved edits on navigation away", spec.md line 760), but a search of `tasks.md` for `GBL-03` / "unsaved" returns nothing: T037 (SCR-04), T061 (SCR-06 drawer) and T085 (SCR-02 wizard) each describe their screen's fields without mentioning the guard, and the US9 global-behaviour tasks cover only FR-GBL-02 (access-denied, T148) and FR-GBL-05 (read-only rendering, T147). So two of the three screens have no task that would produce it.
- **Why not fixed now**: SCR-04's guard **was** implemented here (an `AlertDialog` mirroring `KpiConfigPage`'s existing pattern, `data-testid="channel-unsaved-dialog"`), since shipping the editor without it would leave a ratified FR silently unmet on the one screen this story owns. SCR-02 and SCR-06 belong to US3/US2 and are out of this task's scope — writing their guards now would mean writing the screens now.
- **Note**: the click-through has no unsaved-changes guard on SCR-04 either, so this is also a place where the design source of truth sits **behind** the spec. The clickthrough-parity run reports the dialog as a frontend-only element for discussion rather than a defect; the design owner should decide whether the click-through gains it or the FR is narrowed.
- **Suggested next step**: Either add "…plus the FR-GBL-03 unsaved-changes guard" to T061 and T085's text, or add a single US9 task alongside T147/T148 that applies the guard across SCR-02/04/06 in one consistent pass (the shape SCR-04 now uses). Confirm with the spec owner that FR-GBL-03 is still in scope for all three screens.
- **Added**: 2026-09-02
- **Note (2026-09-03)**: T061 briefly shipped SCR-06's guard, but that pass was **reverted in full**
  (see tasks.md above T061), so SCR-06 is again without one. Two of three screens still lack it.
  One implementation detail worth reusing when it is rebuilt: derive dirtiness by digesting the whole
  field set against a baseline captured on open, rather than flipping a `setDirty(true)` in each
  `onChange` — a derived flag cannot go stale when a field is added to the form later.
- **Update (2026-09-03, later — T061 click-through-blind rebuild)**: **SCR-06's guard now ships for
  real**, built the derived way recommended above (`digest(form) !== baseline`, baseline re-captured
  on each open and again after a successful save so closing post-save raises nothing). All three of
  the drawer's dismissal routes — ✕, scrim click, Esc — funnel through one `requestClose()`, so the
  scrim cannot bypass it; the List panel's "Open mappings" link is guarded too, since following it is
  also a navigation away. **Two of three screens are now done (SCR-04, SCR-06); only SCR-02's wizard
  guard remains unowned**, so this entry stays `GAP` rather than moving to `RESOLVED`. The suggested
  next step narrows to: add the guard to T085 (SCR-02) explicitly, or add one US9 task that applies
  the now twice-proven shape to the wizard.

### TODO-M13-005 — DEFERRED

- **Module**: M-13 (Nabadat.IntegrationHub)
- **Created by task**: T057 (`ParameterService.ScanReferencesAsync`, US2) / T055 (`ParameterDisableImpactScanner`)
- **Deferred feature**: The **external two-thirds of BR-10's impact warning** (Dialog D-6). BR-10 requires that disabling a parameter list *every* consumer referencing it, across three families: M-13's own channel contracts, **M-10 data-scope filters** (CMC-06), and **M-14/15/16 rules and actions** (CMC-07). Only the first is implemented for real.
- **Blocked by**: **M-10 has no reverse lookup**, and **M-14/M-15/M-16 do not exist under `src/`**. M-10's published `IDataScopeService` can answer "what scope does user X have?" (`GetScopeAssignmentsAsync(userId)`) and "what parameter definitions exist?" (`GetParameterDefinitionsAsync`), but *not* "which users' scope assignments or custom rules reference parameter `P`?" — the reverse index D-6 needs. Verified by reading the interface, 2026-07-30. Cross-module direct table reads are not an option (DB-08 / Article 4.1: identifier-only references, no cross-module FKs or joins).
- **Current stub behavior**: M-13 declares its own consumer-side port `Domain/Interfaces/IExternalParameterReferenceReader.cs` (`GetDataScopeFilterNamesAsync(apiField)` / `GetRuleNamesAsync(apiField)`), bound in DI to `Infrastructure/CrossModule/NullExternalParameterReferenceReader`, which returns **empty** for both. Consequence: `PATCH /api/v1/integration-hub/parameters/{id}` with `{enabled:false}` lists **channel-contract references correctly and completely** (read for real from `channel_parameter_assignments`) but reports **zero** scope-filter and rule references. A parameter used *only* by an M-10 scope filter therefore disables with no warning at all. Returning empty rather than throwing is deliberate: BR-10's warning is *informational* and does not gate the operation, so a reader that threw would take the whole disable flow down for a dependency that has no provider yet.
- **Exact resume instructions**: (1) Ask the M-10 owner to add a reverse lookup to the published data-scope contract — e.g. `IDataScopeService.GetAssignmentsReferencingParameterAsync(string parameterName)` returning the referencing users/rules, plus the equivalent over `custom_authorization_rules.parameter_scope_assignments`. (2) Add a host adapter `class UserManagementParameterReferenceReader : IExternalParameterReferenceReader` mapping that onto display names, and register it in place of `NullExternalParameterReferenceReader` (the `services.TryAddScoped<IExternalParameterReferenceReader, …>` line in `IntegrationHubServiceCollectionExtensions`). (3) Do the same for M-14/15/16's rule references when those modules ship. **No change to `ParameterService` or `ParameterDisableImpactScanner` is needed** — they already call the port correctly and the scanner already stamps and orders all three kinds. (4) Extend `ParametersEndpointTests.PATCH_parameters_withholds_the_disable_and_returns_the_reference_list_when_a_channel_uses_it` with a `data_scope_filter`-kind assertion.
- **Files affected**: `src/Nabadat.IntegrationHub/IntegrationHubServiceCollectionExtensions.cs`, `src/Nabadat.IntegrationHub/Infrastructure/CrossModule/NullExternalParameterReferenceReader.cs`, `src/Nabadat.IntegrationHub/Domain/Interfaces/IExternalParameterReferenceReader.cs`
- **Added**: 2026-07-30

### TODO-M01-001 — DEFERRED

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T072 (`DestructiveReturnToDraftService.cs`, US1)
- **Deferred feature**: Actual response purge on Return-to-Draft
- **Blocked by**: T021 (M-04 must ship the concrete `IResponsePurgeService.PurgeSurveyResponsesAsync`)
- **Current stub behavior**: **Updated 2026-07-16 (T063–T084 pass).** The port `Domain/Interfaces/IResponsePurgeService.cs` now exists and `DestructiveReturnToDraftService` (T072) is fully implemented — it calls the port after the status commit and compensates (reverts to prior status) on failure. In DI, the port resolves to the placeholder `Infrastructure/CrossModule/UnavailableResponsePurgeService`, which throws 501 `survey.return_to_draft.purge_service_unavailable`. So a destructive Return-to-Draft currently reverts + surfaces 501; every non-destructive transition is unaffected.
- **Exact resume instructions**: When M-04 ships (T021), add a real `IResponsePurgeService` adapter in the **host** and register it in place of `UnavailableResponsePurgeService` (the `services.TryAddScoped<IResponsePurgeService, …>` line in `SurveyBuilderServiceCollectionExtensions`). No change to `DestructiveReturnToDraftService` is needed — it already calls the port correctly.
- **Files affected**: `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs`, `src/Nabadat.SurveyBuilder/Infrastructure/CrossModule/UnavailableResponsePurgeService.cs`
- **Added**: 2026-07-15 · **Updated**: 2026-07-16 (T072 implemented; port + 501 stub wired)

### TODO-M01-002 — DEFERRED

- **Module**: M-01
- **Created by task**: T072 / T273
- **Deferred feature**: Writing `survey.responses.purged` to the M-11 audit log
- **Blocked by**: Constitution AMENDMENT-012 **ratification** (the amendment was FILED by T022 on 2026-07-16 — appended to `.specify/memory/constitution.md` §24; only platform-architect ratification remains. Coordination tracked in `specs/004-survey-form-builder/coordination-log.md` C-06).
- **Current stub behavior**: Audit event is not emitted until the amendment is ratified.
- **Exact resume instructions**: Once ratified, emit via `IEventLogWriter.WriteAsync(...)` in `DestructiveReturnToDraftService`'s success path.
- **Files affected**: `src/Nabadat.SurveyBuilder/Application/Surveys/DestructiveReturnToDraftService.cs`
- **Added**: 2026-07-15

### TODO-M01-004 — GAP

- **Module**: M-01
- **Found during task**: T093 (`KpiBindingEditor.tsx`, US1)
- **What's missing**: "Reason follow-up" (toggle + list) and "Allow N/A" fields on KPI questions — both are defined in the spec's Field Definitions table but appear in no task in tasks.md.
- **Why not fixed now**: Out of scope for the current implementation pass; no task was ever written for these fields, so there's nothing to defer to.
- **Suggested next step**: Needs a new task (e.g. `T093b`) added to `tasks.md` under US1 to add both fields to `KpiBindingEditor.tsx` and the underlying `Question` entity/config. Flag to spec owner to confirm these weren't deliberately descoped.
- **Added**: 2026-07-15

### TODO-M01-005 — GAP

- **Module**: M-01
- **Found during task**: T217 (`TranslateWorkspacePage.tsx`, US6)
- **What's missing**: No way to add languages beyond the fixed EN|AR pair, and no AI-assisted auto-translation on adding a new language (business requirement raised outside the current spec — see Clarifications discussion).
- **Why not fixed now**: Not in the current spec at all (spec itself only supports EN/AR); no task exists to defer to.
- **Suggested next step**: Requires a spec-level change (new FR/BR entries) before this can become real tasks — not implementable as a code fix alone. Raise with whoever owns `spec.md` for this feature.
- **Added**: 2026-07-15

### TODO-M01-006 — DEFERRED

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T001 (`Nabadat.SurveyBuilder.csproj`, Setup)
- **Deferred feature**: Consuming the M-11 readers `ITenantSettingsReader` + `ITenantDesignGuidelinesReader` (needed by T080/AppearanceService, US1).
- **Blocked by**: T020 (M-11 does not exist under `src/`; the readers are absent from the repo).
- **Also blocked structurally**: M-01 MUST NOT add a `ProjectReference` to `Nabadat.TenantAdmin` as T001 literally instructed — the host references M-01 (T006, added to `Nabadat.TenantAdmin.csproj`), so a reference back would be a project-reference cycle. The host→module direction is the only legal one; the module declares the port and the host supplies the implementation.
- **Current stub behavior**: **Updated 2026-07-16 (T063–T084 pass).** `ITenantDesignGuidelinesReader` is now declared as a consumer-side port at `Domain/Interfaces/ITenantDesignGuidelinesReader.cs` (+ the `TenantDesignGuidelines` token record), and `AppearanceService` (T080) resolves Inherited tokens through it. In DI it resolves to the placeholder `Infrastructure/CrossModule/DevTenantDesignGuidelinesReader`, returning the default Nabadat palette (`#0D8BBC` / `#1E2235` / radius 12) so Inherited-mode appearance works for dev/E2E. **`ITenantSettingsReader` is still NOT created** — only the design-guidelines reader was needed by US1; the settings reader (post-expiry collection flag, etc.) lands with its first consumer. **Update 2026-07-19 (T213 pass):** `TranslationBundleService` is now a consumer of the tenant's supported-locale set (the `translation.locale.not_configured` gate, contracts/translations.md PUT). It currently uses a hardcoded `SupportedLocales = { "en", "ar" }` constant (Phase-1 T-01, consistent with TODO-M01-005) in place of `ITenantSettingsReader.GetSupportedLocalesAsync()`. When that reader ships, inject it into `TranslationBundleService` and source the set from it (delete the constant).
- **Exact resume instructions**: When M-11 ships, add a real `ITenantDesignGuidelinesReader` adapter in the **host** and register it in place of `DevTenantDesignGuidelinesReader` (the `services.TryAddScoped<ITenantDesignGuidelinesReader, …>` line). Add `ITenantSettingsReader` under `Domain/Interfaces/` when its first consumer is implemented. Do NOT add a `ProjectReference` to the host (cycle — see the structural note above).
- **Files affected**: `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs`, `src/Nabadat.SurveyBuilder/Infrastructure/CrossModule/DevTenantDesignGuidelinesReader.cs`, `src/Nabadat.SurveyBuilder/Domain/Interfaces/` (ITenantSettingsReader, future)
- **Added**: 2026-07-16 · **Updated**: 2026-07-16 (design-guidelines port + Dev stub wired; AppearanceService consumes it)

### TODO-M01-008 — GAP

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T018 (`ISurveyRenderService.cs`, Foundational published interface)
- **What's missing**: The `SurveyDefinition` record's field-level shape. The published contract (`contracts/published-interface.md`) declares `ISurveyRenderService.GetActiveSurveyDefinitionAsync` returning `SurveyDefinition?` and describes the pieces it must carry (settings, appearance, welcome/thanks, sections/sets/questions, translations bundle) — but never defines their actual fields, and no other spec doc (surveys.md, research.md, data-model.md) does either. The record was therefore shipped exposing only the unambiguous scalars (`SurveyId`, `Status`, `Locale`, `Layout`, `WelcomeHtml`, `ThanksHtml`).
- **Why not fixed now**: The full authoring/rendering shape can't be defined without M-04's concrete rendering requirements; guessing it would bake an arbitrary schema into a published cross-module contract. `SurveyDefinitionAssembler` (T144, US3) is the task that populates it and is the natural place to finalize the shape, in coordination with M-04.
- **Suggested next step**: When T144 is implemented, pin `SurveyDefinition`'s full shape with the M-04 owner (appearance tokens, per-question authoring detail, inlined translation bundle), expand the record in `src/Nabadat.SurveyBuilder/Domain/Interfaces/ISurveyRenderService.cs`, and have M-02/M-04 re-compile against it (additive published-contract change). If M-04 needs the shape before T144, raise it as a contract-doc change to `contracts/published-interface.md` first.
- **Added**: 2026-07-16

### TODO-M01-009 — GAP

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T024 (`IdempotencyKeyMiddleware.cs` / `InMemoryIdempotencyStore.cs`, Foundational)
- **What's missing**: A distributed backing for the `Idempotency-Key` replay store. `IIdempotencyStore` is implemented by `InMemoryIdempotencyStore` (IMemoryCache), which is per-process and does not survive a restart. In a multi-instance production deployment the 24h idempotency guarantee (APIs-constitution Article 7.1) does NOT hold — a retry that lands on a different host re-executes the write and can double-audit. No task assigns a distributed implementation.
- **Why not fixed now**: The functional requirement (replay same response within 24h) is satisfied for dev/single-instance, and the port (`IIdempotencyStore`) fully abstracts the backing — swapping it is a one-line DI change with no consumer impact. Choosing/standing up the distributed store (Redis vs. a Postgres table) is a platform/deployment decision out of scope for the middleware task.
- **Suggested next step**: Add a task (likely platform-level, or an M-01 Infrastructure task before production) to implement `IIdempotencyStore` over the platform's distributed cache (e.g. Redis) and register it in the host in place of `InMemoryIdempotencyStore`. Confirm with whoever owns the production deployment topology whether M-01 runs multi-instance.
- **Added**: 2026-07-16

### TODO-M01-010 — GAP

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T028 (`SanitiserPolicyVersion.cs`, Foundational)
- **What's missing**: The concrete whitelist of design-system CSS class tokens permitted on the `class` attribute in sanitised welcome/thank-you HTML. research.md §1 says `class` is allowed but "limited to a whitelist of design-system tokens" — it never enumerates the tokens, and no other doc does. `SanitiserPolicyVersion.V1` currently allows the `class` ATTRIBUTE without restricting its VALUES (the adapter sets no `AllowedCssClasses`), so any class string survives sanitisation.
- **Why not fixed now**: Enforcing a per-token whitelist requires the actual token list, which is unspecified — guessing it would either strip legitimate design-system classes (breaking rendering) or under-restrict. Allowing the attribute unrestricted is not a script/handler XSS vector (class values are inert), so it is a hardening gap, not an open injection hole.
- **Suggested next step**: Get the allowed design-system class-token list from the frontend lead, then add an `AllowedCssClasses` FrozenSet to `SanitiserPolicyVersion.V1` and wire `sanitiser.AllowedCssClasses` in `GannsHtmlSanitiserAdapter.Build`. If a versioned policy change is warranted, bump to `PolicyVersion = 2` rather than mutating v1 (the version is persisted per row).
- **Added**: 2026-07-16

### TODO-M01-011 — DEFERRED

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T073/T072 (`SurveyLifecycleService` / `DestructiveReturnToDraftService`, US1)
- **Deferred feature**: Real M-17 audit emission for M-01 status events.
- **Blocked by**: T020 host wiring (M-17's published port is `IM17EventPublisher`; M-01 declares its own consumer-side `IEventLogWriter` taking a `SurveyAuditEvent`).
- **Current stub behavior**: `IEventLogWriter` resolves to `Infrastructure/CrossModule/NoOpEventLogWriter`, which drops the event (returns `Task.CompletedTask`). Status changes therefore succeed but emit NO audit log — acceptable for dev/E2E, NOT for production (audit is mandatory, constitution §5).
- **Exact resume instructions**: Add a host adapter `class M17EventLogWriter : IEventLogWriter` that maps `SurveyAuditEvent` (EventType/SurveyId/ActorId/CorrelationId/Payload) onto M-17's `IM17EventPublisher` and register it in place of `NoOpEventLogWriter` (the `services.TryAddScoped<IEventLogWriter, …>` line in `SurveyBuilderServiceCollectionExtensions`). No change to the emitting services is needed.
- **Files affected**: `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs`, `src/Nabadat.SurveyBuilder/Infrastructure/CrossModule/NoOpEventLogWriter.cs`
- **Added**: 2026-07-16

### TODO-M01-012 — DEFERRED

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T071/T074/T079 (rules projection, survey + question command services, US1)
- **Deferred feature**: Real cross-module readers `IJourneyReader` (M-16), `IKpiCatalogReader` (M-06), `IChannelSurveyRulesReader` (M-02) — declared as M-01 ports this pass, not yet backed by their owning modules.
- **Blocked by**: T020 (M-16/M-06 must expose the readers; M-02 does not exist under `src/` yet).
- **Current stub behavior**: In DI, `IChannelSurveyRulesReader` → `DevChannelSurveyRulesReader` (returns 0, so F1 rules_count is always 0 and Pause never needs confirmation); `IJourneyReader` → `UnavailableJourneyReader` and `IKpiCatalogReader` → `UnavailableKpiCatalogReader` (both throw 501 rather than fabricating validity). Consequence: creating a survey with NO bound journey and authoring NON-KPI questions works; binding a journey, or authoring a KPI question / stage / touchpoint, is refused with 501 until the real adapters land. Blocks the US1 E2E KPI-binding path (T103–T106).
- **Exact resume instructions**: When M-16/M-06/M-02 expose their published readers, add host adapters and register them in place of the three `Infrastructure/CrossModule/*` placeholders (the `services.TryAddScoped<…>` lines). No change to the consuming services is needed.
- **Files affected**: `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs`, `src/Nabadat.SurveyBuilder/Infrastructure/CrossModule/{DevChannelSurveyRulesReader,UnavailableJourneyReader,UnavailableKpiCatalogReader}.cs`
- **Added**: 2026-07-16

### TODO-M01-014 — DEFERRED

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T115/T116/T118 (`PublishAuthorizationService` / `ReviewNotificationBuilder` / `ApprovalWorkflowService`, US2)
- **Deferred feature**: Real cross-module adapters for the two US2 ports — `IPermissionChecker` (M-10, the `PublishOwnSurveys` grant check) and `INotificationDispatcher` (M-09, the Q7 reviewer broadcast) — declared as M-01 consumer-side ports this pass, not yet backed by their owning modules.
- **Blocked by**: T020 host wiring (M-10 exists and can expose a permission check; M-09 does not exist under `src/` yet).
- **Current stub behavior**: In DI, `IPermissionChecker` → `Infrastructure/CrossModule/DenyAllPermissionChecker` (returns false, so a P-03 can never self-publish — every survey routes through the P-01 review path); `INotificationDispatcher` → `Infrastructure/CrossModule/NoOpNotificationDispatcher` (drops the broadcast). Consequence for dev/E2E: submit → PendingReview works and the reviewer flow works, but the self-publish grant path (FR-15.5) always denies, and no reviewer notification is actually delivered (FR-15.2). The approval orchestration, audit emission, and status transitions are otherwise fully wired.
- **Exact resume instructions**: When M-10/M-09 expose their published services, add host adapters (`class M10PermissionChecker : IPermissionChecker`, `class M09NotificationDispatcher : INotificationDispatcher`) and register them in place of the two placeholders (the `services.TryAddScoped<IPermissionChecker, …>` and `services.TryAddScoped<INotificationDispatcher, …>` lines in `SurveyBuilderServiceCollectionExtensions`). No change to the consuming services is needed — they already call the ports correctly.
- **Files affected**: `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs`, `src/Nabadat.SurveyBuilder/Infrastructure/CrossModule/{DenyAllPermissionChecker,NoOpNotificationDispatcher}.cs`
- **Added**: 2026-07-16

### TODO-M01-015 — GAP — RESOLVED (2026-07-19)

- **Resolution**: `EditLockFilter` now injects `EditLockPolicy` + `ISessionContextAccessor` and evaluates the BR-15.1 submitter lock after the BR-1.5 Active/Paused check: a P-03 `PUT /api/v1/surveys/{id}` on their own PendingReview survey returns 403 `survey.edit_locked_by_pending_review`; a P-01 reviewer's edit passes through with an `X-Warning: survey.edit_during_review` header (contract § "Edit-lock behaviour on PendingReview"). `EditLockPolicy` is registered in DI. The two omitted assertions were added to `SurveyLifecycleEndpointTests` (P-03 → 403 with the error code; P-01 → 200 with the warning header) — full class now 6/6 green.
- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T124 (`SurveyLifecycleEndpointTests`, US2 integration)
- **What's missing**: The BR-15.1 PendingReview submitter edit-lock is NOT enforced on the write endpoints. `EditLockPolicy` (T114) exists and is unit-tested, but nothing in the Api layer calls it — the only write-endpoint filter, `EditLockFilter` (T082), enforces only BR-1.5 (blocks editing an Active/Paused survey), not "a P-03 cannot edit their own survey while it is PendingReview". So a P-03 `PUT /api/v1/surveys/{id}` on their own PendingReview survey currently succeeds (should be 403). The contract (contracts/approval-workflow.md § "Edit-lock behaviour on PendingReview") says the filter must enforce this. Because of this, T124 intentionally OMITS the "PUT → 403 for P-03 on own PendingReview" assertion from the task list (user decision, this pass) — the other four T124 assertions + T125 pass.
- **Why not fixed now**: Wiring it is a change to the US1-owned `EditLockFilter` (inject `EditLockPolicy` + `ISessionContextAccessor`, evaluate the submitter lock) — beyond the T124/T125 test scope, and no task in tasks.md explicitly owns the wiring (it fell between T114 EditLockPolicy and T119 controller).
- **Suggested next step**: Add a task to extend `EditLockFilter` to also evaluate `EditLockPolicy.Evaluate(callerRole, callerUserId, new EditLockState(survey.Status, survey.SubmittedBy))` and return 403 `survey.edit_locked_by_pending_review` when locked; then add back the omitted assertion to `SurveyLifecycleEndpointTests` (PUT → 403 for the P-03 submitter while PendingReview; P-01 still 200).
- **Added**: 2026-07-16

### TODO-M01-016 — GAP — RESOLVED (2026-07-19)

- **Resolution**: Added `Survey.ActivatedAt` (`DateTimeOffset?`) + the `activated_at timestamptz NULL` column (`_Baseline.sql`) + EF mapping (`SurveyConfiguration`). `Survey.ChangeStatus` now stamps `ActivatedAt = now` on every transition **into** Active (the FR-3.4 "start" instant; a Pause→Reactivate is a fresh start), so all activation paths — self-serve publish, approval-workflow publish, reactivate — record it centrally. The destructive Return-to-Draft rollback preserves the original `ActivatedAt` (a compensation is not a fresh start). `ActiveSurveyReader` now returns `ActivatedAt = survey.ActivatedAt` and derives `ExpiresAt = ActivatedAt + ActivePeriod` (null when the survey has no active period or is not yet activated ⇒ "never auto-expires"). Verified: `ActiveSurveyReaderContractTests` (6/6), `SurveyLifecycleServiceTests` + `DestructiveReturnToDraftServiceTests` (8/8), `SurveyEndpointTests` (11/13, 2 pre-existing skips). **Note**: the full integration suite mass-fails on this machine due to Docker parallel-container contention (each `IClassFixture` spins its own Postgres) — this is pre-existing (reproduced on a clean tree with changes stashed: identical 60/71) and unrelated to this change; every affected class passes when run in isolation.
- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T146 (`ActiveSurveyReader`, US3)
- **What's missing**: `IActiveSurveyReader.GetStateAsync` returns `ActiveSurveyState(Status, ActivatedAt, ExpiresAt)`, but the survey aggregate carries no `activated_at` timestamp and `ActivePeriod` is a relative duration, so an absolute expiry instant cannot be computed. `ActiveSurveyReader` therefore returns `ActivatedAt = null` and `ExpiresAt = null` (only `Status` is real). M-04's before-start / post-expiry enforcement (BR-3.4) needs a real `ExpiresAt`.
- **Why not fixed now**: Adding an activation timestamp is a change to the survey entity + `_Baseline.sql` (new `activated_at timestamptz` column) + the Active-transition write path (`SurveyLifecycleService` / `Survey.ChangeStatus`), none of which is owned by any US3 task; out of scope for T146.
- **Suggested next step**: Add a task to (1) add `Survey.ActivatedAt` + the `activated_at` column, (2) stamp it on the transition into Active, (3) populate `ActiveSurveyReader` with `ActivatedAt = survey.ActivatedAt` and `ExpiresAt = survey.ActivatedAt + survey.ActivePeriod.ToTimeSpan()` (null period ⇒ null expiry).
- **Added**: 2026-07-16

### TODO-M01-017 — DEFERRED

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T145 (`ResponseCountReader`, US3)
- **Deferred feature**: Live Elasticsearch read of M-04's per-question response-count projection (FR-10.4).
- **Blocked by**: **M-04 (`Nabadat.ResponseCollection`)** — the module does not exist under `src/` yet and owns the `question_response_counts` projection that populates the `tenant_{tenantId}_analytics` index. The original secondary blocker ("no Elasticsearch configured in dev/E2E") is **cleared** as of 2026-07-20 (see Updated), so this is now purely a cross-module dependency → **reclassified triage D→C on 2026-07-20**.
- **Current stub behavior**: The real `Infrastructure/Elasticsearch/ResponseCountReader` (match_all scan of the tenant analytics index, graceful empty-on-error) IS implemented, and the DI extension registers it whenever `Elasticsearch:Uri` (or `Elasticsearch:Url`) is configured; otherwise it registers `Infrastructure/CrossModule/UnavailableResponseCountReader` (always empty). In **Development** the real reader now binds (dev ES configured), but with **M-04 not producing the index** it still resolves to an empty projection, so low-response ordering degrades to insertion order. In E2E/other envs with no ES, the empty reader is active for the same net effect.
- **Exact resume instructions**: When **M-04 ships and populates `tenant_{tenantId}_analytics`**, no M-01 code change is needed for the wiring — the real `ResponseCountReader` already binds when `Elasticsearch:Uri`/`Url` is set (done in dev; set it per-env for prod with a real CA, not the dev `TrustSelfSignedCertificate`). Then **confirm the `question_response_counts` doc shape (`question_id`, `count`) matches M-04's writer**. (The "add an integration test asserting low-response ordering reflects real counts" step is **already done** — `QuestionsSetLowResponseOrderingScenarioTests` seeds real counts via `SeedResponseCountAsync` against a Testcontainers ES and asserts the ordering.)
- **Files affected**: `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/ResponseCountReader.cs`, `src/Nabadat.SurveyBuilder/Infrastructure/CrossModule/UnavailableResponseCountReader.cs`, `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs`
- **Added**: 2026-07-16 · **Updated**: 2026-07-20 (local dev ES wired — `Elasticsearch:*` in `appsettings.Development.json` → `https://localhost:9200`, and `EsClientFactory.Create` now configures Basic-auth + optional self-signed-cert trust so the real reader binds in Development; DI also accepts an `Elasticsearch:Url` alias. Reclassified D→C: the remaining hard blocker is M-04 shipping the projection, not infra.)

### TODO-M01-018 — GAP — RESOLVED (2026-07-19)

- **Resolution**: Doc-only, no code change. `SectionCommandService` is now assigned by a task: T137's text (`specs/004-survey-form-builder/tasks.md`) was extended to "Create `SectionValidator.cs` **and `SectionCommandService.cs`**", mirroring T139's validator + service pairing for Questions Sets, with a note that it was shipped in the T147 pass. The tasks.md ↔ code mapping is now complete (T147 already cited it as its backing service), so it is no longer unplanned.
- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T147 (`SectionsController`, US3)
- **What's missing**: Section create/update had no dedicated Application service task (T137 is only `SectionValidator`, T138 is only the cascade-delete). To keep the controller thin (Article 1A) a `SectionCommandService` (create/update, append-order, validate-then-write in `ExecuteAsync`) was created this pass to back `POST`/`PATCH /sections`. Questions Sets have the analogous `QuestionsSetService` (T139); sections did not.
- **Why not fixed now**: Nothing to fix — the service was created to fill the gap; this entry just records that `SectionCommandService` was not assigned by any task in tasks.md and should be acknowledged by the spec owner (or folded into T137/T138's scope) so it isn't seen as unplanned.
- **Suggested next step**: Add `SectionCommandService` to T137/T138's task text (or a new T137b) so the tasks.md ↔ code mapping is complete; no code change required.
- **Added**: 2026-07-16

### TODO-M01-020 — GAP — RESOLVED (2026-07-19)

- **Resolution**: `QuestionStore.MoveAsync` now compacts both containers after reparenting. Design decision resolved **by the spec/contract, not left open**: gap-free contiguous ordering IS required — `contracts/questions.md` ("Sibling `order` values compact within `(section_id, set_id)`"), `spec.md` §225 ("order is compact and unique per parent"), and `data-model.md` §2.4/§200 ("`(section_id, set_id, order)` contiguous"). Implementation: a private `ReindexContainerAsync` scans a `(section_id, set_id)` container excluding the moved row, inserts the moved question at the clamped target index (destination only), and renumbers `0..n-1`; the source container is renumbered to close the vacated slot. `targetOrder` is treated as an insertion index and clamped into range, so the result is contiguous even for out-of-range input. All runs inside the existing `ExecuteAsync` transaction (tracked-entity mutations, one `SaveChangesAsync`). Verified by the strengthened `QuestionMoveEndpointTests` (T157): cross-section (source compacts + destination inserts-at-index), same-container reorder, and into-set-with-existing-members all assert full contiguous sequences; `QuestionMoveServiceTests` (T132, delegation) still green.
- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T157 (`QuestionMoveEndpointTests`, US3)
- **What's missing**: A cross-section/cross-set move persists the moved question's target `order` but does NOT reindex (compact) the sibling questions' `order` values in either the source or the destination container. `QuestionMoveService.MoveAsync` (T142) + `QuestionStore.MoveAsync` (T065) set only `section_id`/`set_id`/`order` on the moved row. tasks.md T157 says "order compacts", but no shipped code compacts sibling orders; T157 therefore asserts target-order-persisted only (matching the unit contract T132), leaving potential order gaps/collisions among siblings after a move.
- **Why not fixed now**: Out of scope for T155–T161 (test-authoring tasks). Compaction is an implementation change to T142/T065 (or a dedicated re-ordering service) with its own ordering-invariant unit tests, and no task in tasks.md assigns it.
- **Suggested next step**: Add a task (e.g. T142b) to compact `(section_id|set_id, order)` on move — decrement siblings after the vacated slot in the source and open a slot at the target index in the destination, inside the existing `ExecuteAsync` — plus a unit test pinning the compaction, then strengthen T157 to assert sibling order after a move. Decide with the spec owner whether gap-free ordering is required or client-side drag order is authoritative.
- **Added**: 2026-07-19

### TODO-M01-022 — GAP — RESOLVED (2026-07-19)

- **Resolution**: The template snapshot now carries the survey's translations end-to-end (FR-7.4 copy-all, data-model.md §2.9). Added `TranslationBundleSnapshot(Locale, Keys)` and an `IReadOnlyList<TranslationBundleSnapshot> Translations` member on `SurveySnapshot`; `TemplateSnapshotBuilder.Build` gained an optional `IReadOnlyList<SurveyTranslation>? translations` param and copies every locale bundle in; `TemplateCommandService.BuildSnapshotAsync` loads them via `ITranslationStore.GetBySurveyAsync` and `InstantiateAsync` re-persists them via `ITranslationStore.AddAsync` inside the existing `ExecuteAsync` transaction. `TemplateInstantiator` now builds a section id-map alongside the question id-map and **remaps** each `section.{oldId}.*` / `question.{oldId}.*` key onto the regenerated row ids (survey-level keys like `survey.name` pass through); the remapped bundles hang off `InstantiatedSurvey.Translations`. Verified: 3 new unit cases (snapshot copies bundles; snapshot defaults to none; instantiate copies + remaps keys) — full unit suite 230/230 green; T201 gained `POST_instantiate_copies_the_arabic_translations_and_remaps_their_keys_to_the_new_questions` (5/5 green) and T202 now asserts the Arabic `survey.name` + remapped `question.{newId}.text` survive save→instantiate (1/1 green), both against Testcontainers Postgres. Question-key remapping was NOT deferred — question keys are snapshotted and remapped in this pass.
- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T191/T195 (`TemplateSnapshotBuilder` / `TemplateCommandService`, US5)
- **What's missing**: The template snapshot does NOT include the survey's translations. FR-7.4 says a template copies "all" data "including ... translations", and data-model.md §2.9 lists `"translations": {"en": {keys}, "ar": {keys}}` in the snapshot JSON — but `TemplateSnapshotBuilder.Build` never loads `survey_translations` and `SurveySnapshot` has no translations member, so save-as-template drops all localized strings and instantiate produces an English-only survey. Only the pinned US5 unit cases (settings + question bindings) are covered; translations were deliberately scoped out this pass (the translation store only shipped alongside, US6/T210).
- **Why not fixed now**: Out of scope for T188–T197 (the US5 unit tests don't assert translations, and T201/T202 as written assert settings/appearance/questions/bindings, not translations). Adding it touches T191 (builder signature + a `TranslationsSnapshot` member on `SurveySnapshot`), T195 (`BuildSnapshotAsync` must load `ITranslationStore.GetBySurveyAsync`; instantiate must re-persist the copied bundles via `ITranslationStore.AddAsync` with the new survey id), and needs id-remapping of any per-question translation keys onto the regenerated question ids.
- **Suggested next step**: Add a task (e.g. T194b) to (1) add `IReadOnlyList<TranslationBundleSnapshot> Translations` to `SurveySnapshot`, (2) have `TemplateSnapshotBuilder.Build` accept the survey's `SurveyTranslation` rows and copy them (remapping `question.{oldId}.*` keys is deferred until question keys are snapshotted), (3) have `TemplateCommandService.BuildSnapshotAsync` load them and `InstantiateAsync` re-persist them for the new survey, and (4) extend T201/T202 to assert Arabic strings survive save→instantiate. Confirm with the spec owner that translations are in-scope for copy-all (FR-7.4 says yes).
- **Added**: 2026-07-19

### TODO-M01-023 — DEFERRED

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T239 (`ReportAggregator.cs`, US8)
- **Deferred feature**: Native Elasticsearch aggregations for the Survey Report, and a live ES read.
- **Blocked by**: **M-04 (`Nabadat.ResponseCollection`)** — the module does not exist under `src/` yet; its ingest pipeline populates `tenant_{id}_responses` **and owns that index's mapping** (research.md §3). The native-aggregation scale rewrite (step 2) is tightly coupled to that mapping (see the coupling note in Updated), so it cannot be finalised without M-04. The original blocker "no Elasticsearch configured in dev/E2E" is **cleared** as of 2026-07-20 → **reclassified triage D→C on 2026-07-20**.
- **Current stub behavior**: `ReportAggregator` (T239) is real — it queries `tenant_{id}_responses` via `EsQueryBuilder` (period range + data-scope terms + per-question filter) and aggregates the bounded result set (Size cap 10,000) IN-PROCESS: responses/completion/median/touchpoints, per-question distributions, headline CSAT/NPS/CES from denormalised `kpi_family` fields, and verbatims. It is registered ONLY when `Elasticsearch:Uri` is set; otherwise DI binds `UnavailableReportAggregator` (returns `ReportAggregate.Empty` / empty verbatims), so `/report` and `/report/verbatims` return a well-formed empty report in dev/E2E. Deltas are computed by a second aggregate over the equal-length previous window.
- **Exact resume instructions**: (1) set `Elasticsearch:Uri` in the host config (DI then wires `EsClientFactory.Create` + `ReportAggregator` automatically). (2) For large surveys, replace the in-process aggregation in `ReportAggregator.AggregateAsync` with server-side ES aggregations (`value_count`, `avg`, `percentiles[50]` for median, `cardinality` for touchpoints, `terms` sub-aggs for per-question distributions) so the whole result set is not pulled into memory; keep the `ResponseWindowFilter` (FR-13.6) applied as a `range` filter on `submitted_at ≤ sent_at + active_period`. (3) Confirm the seeded document shapes in the T248 integration fixture match `ReportAggregator`'s `ResponseDocument`/`AnswerDocument` DTOs (fields: `response_id`, `survey_id`, `channel`, `submitted_at`, `sent_at`, `completed`, `completion_time_seconds`, `touchpoint_id`, `answers[].{question_id, kpi_family, numeric_value, gauge_target, option_label, option_labels, text}`).
- **Files affected**: `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/ReportAggregator.cs`, `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/UnavailableReportAggregator.cs`, `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs`, `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Report/ReportEndpointTests.cs`.
- **Added**: 2026-07-19 · **Updated**: 2026-07-19 (T248 landed — `ReportEndpointTests` now exercises the real `ReportAggregator` end-to-end against Testcontainers Postgres + ES via `ReportApplicationFactory` (swaps in the real adapter, seeds an explicitly-mapped `tenant_{id}_responses` index). Confirms step (3): the `ResponseDocument`/`AnswerDocument` shapes are correct and the in-process aggregation + FR-13.6 window filter work. Steps (1) prod `Elasticsearch:Uri` config and (2) native ES aggregations for scale REMAIN — entry stays DEFERRED.) · **Updated**: 2026-07-20 (local dev ES wired via `appsettings.Development.json` + `EsClientFactory` Basic-auth/cert-trust; the real `ReportAggregator` now binds in Development too. **Native-aggs coupling clarified** — step (2) can be *drafted* now behind the same `IReportAggregator` port, but server-side aggregations require M-04's index mapping to be aggregation-friendly: `answers` mapped as **`nested`** (the T248 fixture maps it as `object` — fine for the current in-process read, WRONG for a `terms` sub-agg on `answers.question_id`), `survey_id`/`channel`/`option_label` as **`keyword`**, `submitted_at`/`sent_at` as **`date`**. The current in-process reader tolerates a loose mapping because it pulls `_source` and aggregates in C#; native aggs do not. So the rewrite cannot be finalised until M-04 pins the mapping contract (see TODO-M01-008). Reclassified D→C; prod `Elasticsearch:Uri` config still pending.)

### TODO-M01-024 — GAP

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T235 (`PerQuestionViewSelector.cs`, US8)
- **What's missing**: FR-13.3 enumerates a report visual for every question type EXCEPT `Matrix` and `Ranking` — the spec's per-question list covers KPI, single/multi-select, Yes/No, Scale, Text/Paragraph, and Number/Date/Time, but not Matrix or Ranking. `PerQuestionViewSelector.Pick` therefore defaults both to `BarWithCountsAndPct` (a counts+% bar) as the sanest fallback, which is an implementation guess, not a spec decision.
- **Why not fixed now**: No task defines the Matrix/Ranking report visual; T229/T235 only pinned the FR-13.3 mappings. Choosing the visual (e.g. a per-row heat grid for Matrix, an average-rank bar for Ranking) is a design + spec decision beyond US8's scope.
- **Suggested next step**: Spec change — extend FR-13.3 to define the Matrix and Ranking report visuals, then add `PerQuestionViewKind` members + a `PerQuestionViewSelector` mapping + a `PerQuestionView.For` wire shape (and a unit case) for each. Until then the counts+% bar stands.
- **Frontend note (2026-07-20, T246)**: `ReportPage.tsx` renders an explicit "no report visual is defined for this question type yet" note for Matrix/Ranking cards rather than guessing a chart — swap that branch in `QuestionCardView` for the real visual once the spec decides.
- **Added**: 2026-07-19

### TODO-M01-025 — GAP

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T242 (`ReportService.cs`, US8)
- **What's missing**: The report contract shows a `target` on each headline KPI gauge and on KPI per-question gauges (contracts/report-and-analytics.md), but M-01 has no source for KPI targets — the cross-module `IKpiCatalogReader` (M-06) exposes only `KpiExistsAsync` / `ListPerspectivesAsync`, no target getter. `ReportService.BuildKpi` and `PerQuestionAggregate.GaugeTarget` therefore emit `target = null` (the gauge renders without a target marker).
- **Why not fixed now**: Adding a target requires either an M-06 published-interface method (`IKpiCatalogReader.GetTargetAsync(kpiCode)`) or a denormalised `gauge_target` field on the ES answer docs (the `AnswerDocument.gauge_target` field already exists in `ReportAggregator` for the per-question path, but nothing populates it and there is no headline-target source). Out of scope for US8, which owns no M-06 contract change.
- **Suggested next step**: Decide the target source (M-06 interface method vs ES denormalisation), then wire it into `ReportService.BuildKpi` (headline) and the aggregator's `GaugeTarget` (per-question), and assert it in the T248 integration fixture.
- **Added**: 2026-07-19

### TODO-M01-026 — DEFERRED

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T260 (`AnalyticsAggregator.cs`, US9)
- **Deferred feature**: Native Elasticsearch date-histogram aggregations for Survey Analytics, and a live ES read.
- **Blocked by**: **M-04 (`Nabadat.ResponseCollection`)** — the module does not exist under `src/` yet; its ingest pipeline populates the `tenant_{id}_analytics` funnel index **and owns that index's mapping** (research.md §3), which the native `date_histogram` rewrite (step 2) is coupled to (see Updated). The original blocker "no Elasticsearch configured in dev/E2E" is **cleared** as of 2026-07-20 → **reclassified triage D→C on 2026-07-20**.
- **Current stub behavior**: `AnalyticsAggregator` (T260) is real — it queries `tenant_{id}_analytics` for the survey's funnel documents across the previous-through-current window (Size cap 10,000) and splits/sums/groups/re-buckets them IN-PROCESS into current+prior `FunnelCounts`, per-channel `ChannelCounts`, and the granularity-bucketed trend. It is registered ONLY when `Elasticsearch:Uri` is set; otherwise DI binds `UnavailableAnalyticsAggregator` (returns `AnalyticsAggregate.Empty`), so `/analytics` returns a well-formed empty payload (all-zero funnel, no channels/trend, deltas suppressed) in dev/E2E.
- **Exact resume instructions**: (1) set `Elasticsearch:Uri` in the host config (DI then wires `EsClientFactory.Create` + `AnalyticsAggregator` automatically). (2) For large surveys, replace the in-process split/sum/bucket in `AnalyticsAggregator.AggregateAsync` with server-side ES aggregations (a `date_histogram` on `bucket_start` at the requested calendar interval, `sum` sub-aggs on sent/opened/started/finished, a `terms` agg on `channel`) and issue two range-bounded searches (current + prior) instead of pulling the combined result set into memory. (3) Confirm the seeded document shape in the T267 integration fixture matches `AnalyticsAggregator`'s `FunnelDocument` DTO (fields: `survey_id`, `channel`, `bucket_start`, `sent`, `opened`, `started`, `finished`).
- **Files affected**: `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/AnalyticsAggregator.cs`, `src/Nabadat.SurveyBuilder/Infrastructure/Elasticsearch/UnavailableAnalyticsAggregator.cs`, `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs`, `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Analytics/AnalyticsEndpointTests.cs`.
- **Added**: 2026-07-19 · **Updated**: 2026-07-20 (local dev ES wired via `appsettings.Development.json` + `EsClientFactory` Basic-auth/cert-trust; the real `AnalyticsAggregator` now binds in Development, and `AnalyticsEndpointTests` already swaps it in against a Testcontainers ES [the earlier "T267, later" caveat is stale — the analytics integration test exists and drives the real adapter]. **Native-aggs coupling** — step (2) is *draftable* now behind the same `IAnalyticsAggregator` port, but the server-side `date_histogram` needs M-04's funnel index to map `bucket_start` as **`date`**, `channel` as **`keyword`**, and `sent`/`opened`/`started`/`finished` as **numeric**; the current in-process reader tolerates a loose mapping because it aggregates `_source` in C#. So it cannot be finalised until M-04 pins the mapping (see TODO-M01-008). Reclassified D→C; prod `Elasticsearch:Uri` config still pending.)

---

### TODO-M01-027 — GAP

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T036 (`useUnsavedChangesGuard.ts`)
- **What's missing**: In-app navigation blocking for dirty forms. The app mounts a declarative `<BrowserRouter>` (`frontend/src/App.tsx`), and react-router v7's `useBlocker` only works on data routers (`createBrowserRouter`) — so `useUnsavedChangesGuard` can only (a) arm the browser `beforeunload` prompt (tab close / refresh / external nav) and (b) expose `confirmIfDirty()` for the page's own programmatic navigations. A dirty-form user who clicks a sidebar `<Link>` navigates away with no prompt (NFR-5/Q1 intends a guard on navigation).
- **Why not fixed now**: Fixing it properly means migrating `App.tsx` from `<BrowserRouter>`+`<Routes>` to `createBrowserRouter`/`<RouterProvider>` (or shipping a custom navigation-confirm context that wraps every nav trigger) — an app-wide router change far outside T036's scope, touching every existing feature's routes.
- **Suggested next step**: Decide with the frontend lead: either (1) a small platform task to migrate to the data router (then `useUnsavedChangesGuard` adds `useBlocker(isDirty)` + a design-system confirm `<Dialog>` — ~10-line hook change, exact spot marked by the scope note comment in `useUnsavedChangesGuard.ts`), or (2) accept `beforeunload`-only coverage and have M-01 form pages call `confirmIfDirty()` on their own exits. Option 1 is the durable fix.
- **Added**: 2026-07-20

---

### TODO-M01-028 — DEFERRED (narrowed 2026-07-20 — write path RESOLVED by T152/T153/T154; read path still blocked)

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T090 (`SurveyBuilderPage.tsx`, US1); narrowed by T152–T154 / T178–T180 (US3/US4 pass, 2026-07-20)
- **Deferred feature**: Builder canvas **structure loading on mount**. The SAVE half is done: the canvas now persists through `sections-api.ts` / `questions-sets-api.ts` / `questions-api.ts` / `routing-api.ts` (create on Add/drop, debounced PATCH renames, FR-2.5/2.6 confirm-delete cascades, move endpoint on drag, per-question routing maps) — the client-side flows target the same endpoints the T155–T157/T181 backend integration tests prove. The LOAD half cannot be built: **neither the contracts nor the shipped controllers expose any GET/list endpoint for sections/sets/questions** (verified 2026-07-20: `SectionsController` / `QuestionsSetsController` / `QuestionsController` are POST/PATCH/DELETE(+move) only; `GET /surveys/{id}` returns survey columns only; `GET …/render-plan` is Active-only and differently shaped). After a page reload the builder starts from an empty canvas view even though rows exist; the client-side BR-1.7 gate then sees 0 sections until edits resume (the server-side gate stays authoritative).
- **Blocked by**: a missing backend structure-read endpoint (e.g. `GET /api/v1/surveys/{id}/structure` returning sections → sets → questions with row versions). NO task in tasks.md ships one — needs a contract addition (sections-and-sets.md) + a small backend task; surface to the module owners (abukr/attia).
- **Current stub behavior**: `SurveyBuilderPage.load()` fetches only the survey (`GET /surveys/{id}`); `sections` state initialises empty. All in-session structure edits persist correctly with per-row ETags (`serverId`/`rowVersion` on the model).
- **Exact resume instructions**: once the read endpoint exists: add `getSurveyStructure(surveyId)` to `sections-api.ts`; in `SurveyBuilderPage.load()` call it after `getSurvey` and map wire → model: `SectionView` → `BuilderSection` (`serverId: id`, `rowVersion`), `QuestionsSetView` → `BuilderSet`, `QuestionView` → `BuilderQuestion` (use `normalizeQuestionType` / `normalizeQuestionSubType` from `questions-api.ts`; set `hasRoutingMap` from a per-question `hasRouting` flag on the view or a follow-up `getQuestionRouting`). The model fields already line up 1:1 — no model change needed.
- **Files affected**: `frontend/src/features/surveys/pages/SurveyBuilderPage.tsx` (`load()`), `frontend/src/features/surveys/api/sections-api.ts` (new `getSurveyStructure`), backend: new controller action + contract section.
- **Added**: 2026-07-20 · **Narrowed**: 2026-07-20

---

### TODO-M01-029 — GAP

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T266 (`AnalyticsTrendChart.tsx`, US9)
- **What's missing**: T265/T266 call for the trend chart to carry action/event annotation markers (Recharts `ReferenceDot`, per CLAUDE.md § Trend Chart Annotations), but the M-01 analytics contract (`AnalyticsView`) carries no events collection and no module exposes an action/event feed for surveys. `AnalyticsTrendChart` ships the full marker rendering (ReferenceDot + dashed ReferenceLine) behind an `events?: TrendEvent[]` prop that currently defaults to empty — `AnalyticsPage` has nothing to pass it.
- **Why not fixed now**: No task defines an events source; it is a cross-module question (likely M-05 action plans or M-02 rule changes) plus a contract addition to `report-and-analytics.md`.
- **Suggested next step**: Decide the event source with the spec owner; then extend `AnalyticsView` (or add a sibling endpoint) with `{bucket_start, label}` events, map them in `analytics-api.ts`, and pass them to `<AnalyticsTrendChart events={…}>` in `AnalyticsPage.tsx` — the component needs no further change.
- **Added**: 2026-07-20

---

## READY

*(blocker landed, stub not yet removed — should not sit here long)*

(none)

---

## RESOLVED

### TODO-M13-002 — GAP → RESOLVED (2026-09-02)

- **Module**: M-13 (Nabadat.IntegrationHub)
- **Found during task**: T021 (Integration Hub sidebar nav, Phase 2 Foundational)
- **What was missing**: T021's text targeted `NAV_ITEMS` / `ROLE_NAV_KEYS` in
  `frontend/src/components/layout/app-sidebar.tsx` — three names that exist only in the
  click-through, whose sidebar *is* a data-driven registry. This repo's authenticated shell is
  `frontend/src/components/layout/AppLayout.tsx`, built from inline `<SidebarGroup>` blocks gated by
  per-persona booleans computed in the component.
- **How resolved**: Took **option (a)** — the cheap path all three earlier (reverted) attempts also
  took, now kept. `AppLayout.tsx` gained two adjacent `SidebarGroup`s — `nav.integrationHub`
  (Integrations · Request logs) and `nav.dataModel` (Service channels · Parameters · Parameter
  mappings), matching the click-through's two groups exactly — plus **two** persona booleans, not
  one: `canViewIntegrationHub` (P-01 ∪ P-07, gating both groups) and a separate
  `canViewRequestLogs` (P-07 only), because per the Permissions Matrix request logs are the single
  M-13 screen with **no** P-01 grant at all, unlike every other screen's BR-24 read-only mirror.
  Both were added to `hasFeatureNav`, so a P-07-only user does not get the "no permissions" empty
  state beside a populated group. T021's text in `specs/006-integration-hub/tasks.md` was rewritten
  to describe this, so the task no longer points at the wrong codebase. Verified: `npm run build`
  green, and the P-07 half is asserted live by E2E `M13-E2E-06`.
- **Files affected**: `frontend/src/components/layout/AppLayout.tsx`,
  `frontend/src/i18n/locales/{en,ar}.json` (the seven `nav.*` labels),
  `specs/006-integration-hub/tasks.md` (T021 text)
- **Added**: 2026-07-30 · **Resolved**: 2026-09-02



*(stub removed and verified, or GAP fixed)*

### TODO-M13-001 — GAP → RESOLVED (2026-07-30)

- **Module**: M-13 (Nabadat.IntegrationHub)
- **Found during task**: T004 (solution registration, Phase 1 Setup); resolved by T012 + T018 (Phase 2 Foundational)
- **What was missing**: Host wiring for the new module. No task in `specs/006-integration-hub/tasks.md` (T001–T218) added a `<ProjectReference Include="..\Nabadat.IntegrationHub\Nabadat.IntegrationHub.csproj" />` to `src/Nabadat.TenantAdmin/Nabadat.TenantAdmin.csproj`, nor a `builder.Services.AddIntegrationHubModule(...)` call in the host's startup — T004 only registered the three projects in the `.sln`, and T012 only *defines* the extension method inside the module. Without the host reference the module's `Api/Controllers/*` would never be discovered as an `ApplicationPart` (every M-13 endpoint 404s) and T018's `WebApplicationFactory<Program>` fixture would boot a host with no M-13 services. The integration-test project likewise lacked its `Nabadat.TenantAdmin` reference (T003 specified the module reference only).
- **How resolved**: Absorbed into T012 and T018 exactly as the suggested next step proposed, all four pieces landed 2026-07-30:
  1. `src/Nabadat.TenantAdmin/Nabadat.TenantAdmin.csproj` — added the M-13 `ProjectReference` (with a T012 comment mirroring the existing M-01 one).
  2. `src/Nabadat.TenantAdmin/Program.cs` — added `using Nabadat.IntegrationHub;` and `builder.Services.AddIntegrationHubModule(builder.Configuration);` after the M-01 registration.
  3. `tests/Nabadat.IntegrationHub.IntegrationTests/…csproj` — added the `Nabadat.TenantAdmin` `ProjectReference` (the stale "added by T018 — see TODO-M13-001" comment is now an accurate back-reference) plus `OTP.NET` for the real login → MFA-verify flow.
  4. **Beyond the entry's original scope, same class of gap**: `src/Nabadat.TenantAdmin/Development/DevTenantSchemaBootstrapper.cs` now also reads and applies `IntegrationHub_Baseline.sql` per tenant schema, gated on a `service_channels` sentinel — without it a dev tenant schema would carry zero M-13 tables. The module csproj copies the baseline to the output `Migrations/` folder so both the bootstrapper and the test factory find it.
- **Verified**: `dotnet build Nabadat.TenantAdmin.sln` → **0 errors** (the host now compiles against and calls `AddIntegrationHubModule`, which is what the entry's resolve criterion asked for); `IntegrationHub_Baseline.sql` confirmed present in both `src/Nabadat.TenantAdmin/bin/.../Migrations/` and the integration-test project's output. The baseline itself was applied to a throwaway Postgres 16 container (all 8 owned tables + `event_log`, 17 partitions, 23 seeded built-ins, every CHECK/unique constraint enforcing its VR/BR as specified, idempotent on re-run). **Not** exercised at HTTP runtime — deliberately, because no M-13 controller exists yet: the first endpoint lands with US1's T035 and is proven through this very factory by T041. If that first endpoint 404s, this wiring is the first thing to re-check.
- **Files affected**: `src/Nabadat.TenantAdmin/Nabadat.TenantAdmin.csproj`, `src/Nabadat.TenantAdmin/Program.cs`, `src/Nabadat.TenantAdmin/Development/DevTenantSchemaBootstrapper.cs`, `src/Nabadat.IntegrationHub/Nabadat.IntegrationHub.csproj`, `tests/Nabadat.IntegrationHub.IntegrationTests/Nabadat.IntegrationHub.IntegrationTests.csproj`
- **Added**: 2026-07-30 · **Resolved**: 2026-07-30

### TODO-M01-003 — DEFERRED → RESOLVED (2026-07-19)

- **Module**: M-01
- **Created by task**: T138 (`SectionCascadeService.Delete`, US3); real store shipped by T210/T213 (US6)
- **Deferred feature**: Purging translation rows when a question/section is deleted (FR-2.8)
- **How resolved**: The real EF store shipped 2026-07-19 (T210/T213) — `Infrastructure/Persistence/Stores/TranslationStore.PurgeQuestionKeysAsync` scrubs every `question.{id}.*` key from every locale bundle (bumping row_version), and it is registered as the `ITranslationStore` so `SectionCascadeService`, `QuestionDeletionService`, and `QuestionsSetService` call the REAL purge inside their `ExecuteAsync` transaction (the interim no-op `DeferredTranslationStore` was deleted). **Verified end-to-end 2026-07-19**: added `DELETE_question_purges_its_translation_keys_from_every_locale_bundle` to `TranslationEndpointTests` (+ a `GetTranslationKeyNamesAsync` DB-inspection helper on `SurveyBuilderApplicationFactory` that reads `survey_translations.keys` directly, since a GET can't prove the purge once the source stops emitting the deleted question's key). The test PUTs an `ar` bundle carrying `survey.name` + `question.{id}.text`, DELETEs the question via `DELETE /api/v1/surveys/{id}/sections/{sid}/questions/{qid}`, then asserts the question key is gone from stored `keys` while `survey.name` survives. Green vs Testcontainers Postgres (`Passed! Failed: 0, Passed: 2`).
- **Files affected**: `src/Nabadat.SurveyBuilder/Infrastructure/Persistence/Stores/TranslationStore.cs`, `src/Nabadat.SurveyBuilder/SurveyBuilderServiceCollectionExtensions.cs`, `tests/Nabadat.SurveyBuilder.IntegrationTests/Api/Translations/TranslationEndpointTests.cs`, `tests/Nabadat.SurveyBuilder.IntegrationTests/Infrastructure/SurveyBuilderApplicationFactory.cs`
- **Added**: 2026-07-15 · **Resolved**: 2026-07-19 (delete→purge exercised end-to-end; T218 endpoints already green)

### TODO-M01-007 — DEFERRED → RESOLVED (2026-07-19)

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T009/T011 (`ITenantDbContext.cs` / `TenantDbContext.cs`, Foundational)
- **Deferred feature**: Full domain entities for the 9 aggregates. `ITenantDbContext` and `TenantDbContext` expose `DbSet<T>` for all 9 owned tables, which requires the entity TYPES to exist to compile — but the full entities are assigned to later tasks (T053 Survey, T054 Section, T055 Question, T056 Theme; QuestionsSet / RoutingMap / SurveyTranslation / Template / TemplateSnapshot in their US phases).
- **Blocked by**: the later per-entity creation tasks for the 5 remaining aggregates (QuestionsSet, RoutingMap, SurveyTranslation, Template, TemplateSnapshot) in their US phases. T053–T057 (Survey, Section, Question, Theme, QuestionTypePayload) are DONE.
- **How resolved**: **Partially resolved 2026-07-16 (T053–T062 pass).** Survey (T053), Section (T054), Question (T055), and Theme (T056) now carry their full column sets + factory (`Survey.Create`) + `IncrementRowVersion()`/`ChangeStatus`; the polymorphic `QuestionTypePayload` + 8 payloads (T057) and the 4 `IEntityTypeConfiguration<T>` (T058–T061, wired in `TenantDbContext.OnModelCreating`) + 3 value converters (T062) all landed and `src` builds clean. **Update 2026-07-16 (T134–T135 pass):** `QuestionsSet` is now fully fleshed (full column set per §2.3 — SectionId/Title/Description/SelectionMode/Count/Order/timestamps/row_version, with the `QuestionsSetSelectionMode` enum↔`random`/`low_response` text conversion) with its `QuestionsSetConfiguration` wired into `OnModelCreating`, plus its `IQuestionsSetStore`/`QuestionsSetStore` (T136). **Update 2026-07-16 (T168–T170 pass):** `RoutingMap` is now fully fleshed (full column set per §2.5 — SurveyId/SourceQuestionId/AnswerKey/TargetQuestionId/timestamps, no row_version) with its `RoutingMapConfiguration` wired into `OnModelCreating`, plus its `IRoutingMapStore`/`RoutingMapStore` (T170) and the test `InMemoryRoutingMapStore` aligned to the port. **Update 2026-07-16 (T100–T102 pass):** the T063–T084 stores/controllers now exercise the EF model at runtime, which surfaced that `TemplateSnapshot.TemplateId` is not a convention key → the model failed to build (every `/api/v1/surveys` request 500'd); a **minimal `modelBuilder.Entity<TemplateSnapshot>().HasKey(t => t.TemplateId)`** was added to `OnModelCreating` to unblock it. **Update 2026-07-19 (T208/T209 pass):** `SurveyTranslation` is now fully fleshed (full column set per §2.7 — SurveyId/Locale/Keys jsonb/timestamps/row_version, with a `Dictionary<string,string>`↔jsonb `HasConversion` + `ValueComparer`) with its `TranslationConfiguration` wired into `OnModelCreating`, plus its `ITranslationStore`/`TranslationStore` (T210). **RESOLVED 2026-07-19 (T188–T189 pass):** `Template` and `TemplateSnapshot` are now fully fleshed (full column sets per §2.8/§2.9 — Template: Class/NameEn/NameAr/Description/Tags[]/Sectors[]/thumbnail/timestamps/row_version; TemplateSnapshot: TemplateId/Snapshot jsonb/SchemaVersion/CreatedAt) with `TemplateConfiguration` + `TemplateSnapshotConfiguration` wired into `OnModelCreating`, and the interim `modelBuilder.Entity<TemplateSnapshot>().HasKey(...)` was MOVED into `TemplateSnapshotConfiguration`. All 9 aggregates are now real entities mapped to their real tables; the full unit suite (169 tests) is green.
- **Files affected**: `src/Nabadat.SurveyBuilder/Domain/Entities/{SurveyTranslation,Template,TemplateSnapshot}.cs`
- **Added**: 2026-07-16 · **Resolved**: 2026-07-19 (T188–T189: Template + TemplateSnapshot fleshed + configured; interim HasKey moved into TemplateSnapshotConfiguration; all 9 aggregates done)

### TODO-M01-013 — GAP → RESOLVED (2026-07-19)

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T081 (`SurveysController.RenderPlan`, US1)
- **What was missing**: The real `GET /surveys/{id}/render-plan` assembly (FR-10.4). The endpoint returned a MINIMAL plan — sections in order with EMPTY question-id lists and an EMPTY routing map — so it did not perform the low-response Questions-Set sampling, standalone-question ordering, or the routing-map projection the contract (contracts/surveys.md) specifies for M-02/M-04.
- **How resolved**: Superseded by the T150 work tracked in TODO-M01-019 (RESOLVED 2026-07-19). `SurveyRenderService` (T143) implements the FR-10.4 ordering + set sampling + routing projection, and T150 wired `SurveysController.RenderPlan` (GET) to call `ISurveyRenderService.GetRenderPlanAsync` and map the enriched `RenderPlanResponse` (items with `question`/`set` kind + the routing map). Same 3 passing `RenderPlanEndpointTests`. No separate action remains.
- **Added**: 2026-07-16 · **Resolved**: 2026-07-19

### TODO-M01-019 — DEFERRED → RESOLVED (2026-07-19)

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Created by task**: T158/T159 (US3, render-plan API/scenario tests); resolved by T150.
- **Deferred feature**: HTTP `GET /api/v1/surveys/{id}/render-plan` returning the real FR-10.4 low-response order (ordered sections, per-set samples, routing map) for M-02/M-04 diagnostics.
- **How resolved**: T150 wired the endpoint to the published `ISurveyRenderService`. `SurveysController.RenderPlan` (GET) now calls `GetRenderPlanAsync(new SurveyId(id), new RespondentContext(RespondentSeed.From(respondent_id), new LocaleCode("en")), ct)` and maps via `RenderPlanResponse.From(RenderPlan)`; the response DTO was enriched to the contract shape (`RenderPlanSection.Items` of `RenderPlanItem` kind `question`/`set`, + the `question_id → answer_key → target|"__end"` routing map). A new `SurveyRenderPlanController` adds the POST diagnostics variant (T150). The service now 404s (indistinguishable-absence) when the survey is missing OR not Active. Verified: added two HTTP-level tests to `RenderPlanEndpointTests` (auth added to `RenderPlanApplicationFactory` via `SignedInClientAsync`) — the (7,4,12) fixture orders sections [s2,s1,s3] through the real GET route, and an unknown survey returns 404. `dotnet test … --filter "FullyQualifiedName~RenderPlanEndpointTests"` → 3 passed (Postgres + ES containers), 2026-07-19.
- **Added**: 2026-07-19 · **Resolved**: 2026-07-19

### TODO-M01-021 — GAP → RESOLVED (2026-07-19)

- **Module**: M-01 (Nabadat.SurveyBuilder)
- **Found during task**: T204 (`TranslationBundleBuilderTests.cs`, US6)
- **What was missing**: `TranslationBundleBuilder` was a named US6 unit-under-test (T204) with no implementation task in tasks.md (T208–T217).
- **How resolved**: Created `src/Nabadat.SurveyBuilder/Application/Translations/TranslationBundleBuilder.cs` (ctor `(LocaleFallbackPolicy)`; `ResolvedTranslationBundle Build(TranslationBundle source, TranslationBundle target)` — resolves each source key to target-or-English and lists missing keys), plus the supporting records `ResolvedTranslationBundle`/`TranslationBundle`. Registered it in DI, and `TranslationBundleService` (T213) delegates its Get/resolved-view assembly to it. Verified: the T204 `TranslationBundleBuilderTests` (and T205/T206) pass — 22 Translations unit tests green (`dotnet test … --filter "FullyQualifiedName~Translations"`, 2026-07-19). The suggested-next-step task `T212b` is unnecessary — the builder is landed and exercised.
- **Added**: 2026-07-19 · **Resolved**: 2026-07-19

---

## PROMOTED

*(GAP entries converted into real backlog tasks — record what they became)*

(none yet)

---

## Entry templates

**DEFERRED** (has a known future fixer):

```
### TODO-{MODULE}-{NNN} — DEFERRED
- **Module**: <M-XX>
- **Created by task**: <task ID that created the stub>
- **Deferred feature**: <one-line description>
- **Blocked by**: <task ID / external dependency>
- **Current stub behavior**: <exactly what the code does right now>
- **Exact resume instructions**: <literal code-level fix, not "revisit">
- **Files affected**: <paths>
- **Added**: <date>
```

**GAP** (incomplete now, no assigned fixer):

```
### TODO-{MODULE}-{NNN} — GAP
- **Module**: <M-XX>
- **Found during task**: <task ID being implemented when this was noticed>
- **What's missing**: <what wasn't done / was skipped / isn't covered>
- **Why not fixed now**: <why it's out of scope for the current task, e.g. no task exists for it>
- **Suggested next step**: <what needs to happen — new task? spec change? design decision?>
- **Added**: <date>
```
