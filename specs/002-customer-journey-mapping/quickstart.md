# Quickstart & Validation Guide: Customer Journey Mapping Module (M-16)

**Feature**: 002-customer-journey-mapping
**Date**: 2026-06-08

This guide describes how to validate the M-16 implementation end-to-end once it is complete. It covers backend unit tests, integration tests (Docker required), and browser E2E tests.

For API endpoint shapes, see [contracts/](contracts/). For database schema, see [data-model.md](data-model.md).

---

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| .NET SDK | 10.0+ | `dotnet --version` |
| Docker Desktop | 24+ | Required for Testcontainers (integration tests) |
| Node.js | 22+ | `node --version` |
| Playwright browsers | — | One-time install (see Step 5) |
| Running backend | — | `dotnet run` from the host project (`Nabadat.TenantAdmin`) |

---

## Step 1 — Build the M-16 Module

```powershell
# From repo root
dotnet build src/Nabadat.Platform.M16/Nabadat.Platform.M16.csproj
```

Expected: Zero errors. All three published interfaces (`IJourneyConfigReader`, `IReportContractReader`, `IJourneyScoreProvider`) compile correctly.

---

## Step 2 — Run Unit Tests (no Docker required)

```powershell
dotnet test tests/Nabadat.Platform.M16.UnitTests
```

Expected: All tests green. Key test classes and what they prove:

| Class | What it proves |
|-------|---------------|
| `JourneyStatusTransitionServiceTests` | Valid transitions accepted; Archived → any rejected; P-02 transition attempt rejected at service layer |
| `PersonaStatusTransitionServiceTests` | Persona lifecycle state machine; Archive terminal; non-Active persona binding rejected |
| `KpiWeightValidatorTests` | Sum = 100% accepted; sum ≠ 100% rejected with `kpi.weight_sum_invalid`; empty list accepted (unmeasured) |
| `JourneySnapshotSerializerTests` | Snapshot includes all stages, touchpoints, KPI bindings, `ScoringConfig`, `DetectionConfig`; subsequent entity changes do not mutate a captured snapshot |
| `DetectionOverrideResolverTests` | Touchpoint override wins over stage; stage override wins over journey default; null override fields inherit parent |
| `JourneyNameUniquenessValidatorTests` | Case-insensitive conflict detected; Archived journey name considered released |
| `JourneyScoreProviderServiceTests` | M-06 delegation called; result upserted; `journey.score.updated` published in same transaction; M-06 failure does not persist partial state |
| `M17EventPublisherTests` | Transaction rollback on event write failure propagates correctly |

---

## Step 3 — Run Integration Tests (Docker required)

```powershell
dotnet test tests/Nabadat.Platform.M16.IntegrationTests
```

Docker must be running. Testcontainers provisions a fresh PostgreSQL instance per fixture class. Expected: All tests green. Key coverage areas:

### Endpoint Tests
- `POST /api/v1/journeys` → creates journey with status `Draft`; name conflict returns 409
- `PATCH /api/v1/journeys/{id}/status` → valid transitions persist; Archived → Active returns 422
- `PUT /api/v1/touchpoints/{id}/kpis` → valid weights persist; sum ≠ 100 returns 422 with correct error code
- `POST /api/v1/journeys/{id}/publish` → creates version snapshot; snapshot is self-contained
- `PATCH /api/v1/personas/{id}/status` → P-02 caller returns 403; P-01 caller succeeds
- `DELETE /api/v1/personas/{id}` → returns 405 with `persona.use_archive_instead`

### Service Tests
- `JourneyScoreProviderTransactionTests` — score upsert + M-17 event are in same transaction; simulated M-06 failure rolls back both writes
- `KpiWeightEnforcementTests` — saving bindings with weight sum = 85% fails before any DB write; no partial state in `kpi_bindings`

### Scenario Tests

**`JourneyDefinitionFlowTests.JourneyDefinitionFlow`** (US-1):
1. P-01 creates a journey → status `Draft`
2. Adds 3 stages with touchpoints
3. Configures KPI bindings (NPS 60% + CSAT 40%) on touchpoints
4. Verifies `GET /api/v1/journeys/{id}` returns the full structure
5. Verifies unmeasured touchpoint has `isMeasured: false`
6. P-03 attempts to create a journey → 403 verified
7. P-01 transitions journey to `Active` → status change + event emitted

**`KpiAndScoringConfigurationTests.KpiAndScoringConfiguration`** (US-2):
1. P-01 or P-02 saves KPI bindings with 85% total → receives `kpi.weight_sum_invalid` 422
2. Corrects to 100% → bindings persisted; `npsWarning: true` in response when NPS included
3. Saves scoring config (`WeightedAverage`, normalization params)
4. `IJourneyConfigReader.GetJourneyConfigAsync` returns config with correct KPI types and weights

**`PersonaAndVersionManagementTests.PersonaAndVersionManagement`** (US-3):
1. P-01 creates persona → status `Draft`
2. Transitions to `Active` → persona appears in binding selector (`GET /api/v1/personas?status=Active`)
3. Binds persona to journey → binding persisted
4. Transitions persona to `Inactive` → binding selector no longer shows it
5. P-01 publishes journey version → `JourneyVersion` row created with snapshot
6. Edits journey name → re-fetches version → version snapshot unchanged (immutable)
7. P-02 attempts to publish → 403 verified
8. P-01 archives persona with active binding → 409 `persona.archive_blocked_active_bindings` returned

**`DetectionAndReportContractTests.DetectionAndReportContract`** (US-4):
1. P-01 saves detection config: journey-level `painThreshold=40, happyThreshold=75`
2. Adds stage-level override: `painThreshold=35, happyThreshold=70` for stage 1
3. `IReportContractReader.GetReportContractAsync` returns contract with all stages and touchpoints
4. Unmeasured touchpoint has `isMeasured: false` in the contract; is absent from KPI dimension list
5. `painThreshold >= happyThreshold` returns 422 `detection.threshold_invalid`

---

## Step 4 — Run the Frontend Dev Server

```powershell
# From frontend/ directory
npm install
npm run dev
```

App served at `http://localhost:5173` (proxies `/api` → `https://localhost:7286`).

Backend must be running: `dotnet run --project src/Nabadat.TenantAdmin/Nabadat.TenantAdmin.csproj`.

---

## Step 5 — Run E2E Tests (requires running stack)

```powershell
# Install Playwright browsers once
cd tests/Nabadat.TenantApp.E2ETests
dotnet run -- playwright install

# Run M-16 E2E tests
$env:E2E_BASE_URL = "http://localhost:5173"
dotnet test tests/Nabadat.TenantApp.E2ETests --filter "FullyQualifiedName~JourneyBuilder|FullyQualifiedName~KpiScoring|FullyQualifiedName~PersonaVersion|FullyQualifiedName~DetectionRules"
```

Test credentials and TOTP secret must be in `tests/Nabadat.TenantApp.E2ETests/appsettings.local.json` (gitignored). Seed the test tenant with a P-01 and a P-02 test user before running.

E2E tests capture a screenshot and Playwright trace per test — visible in VS Test Explorer **Attachments** section.

### E2E Coverage Summary

| Test class | User story | Key scenarios |
|-----------|-----------|---------------|
| `JourneyBuilderTests` | US-1 | Create journey; add stages and touchpoints; transition Draft → Active; P-03 access denial |
| `KpiScoringTests` | US-2 | KPI weight validation error; NPS warning banner; scoring config save |
| `PersonaVersionTests` | US-3 | Create persona; lifecycle transitions; binding selector behavior; publish version; P-02 denial |
| `DetectionRulesTests` | US-4 | Journey-level threshold save; stage override; unmeasured touchpoint indicator |

---

## Step 6 — Validate Published Interfaces (Integration Smoke Test)

From the integration test project, the `JourneyDefinitionFlowTests` scenario exercises all three published interfaces at the end of the flow:

```csharp
// After flow completion:
var configReader = factory.Services.GetRequiredService<IJourneyConfigReader>();
var config = await configReader.GetJourneyConfigAsync(journeyId);
config.Should().NotBeNull();
config!.Stages.Should().HaveCount(3);
config.Stages[0].Touchpoints[0].KpiBindings.Should().HaveCount(2);

var contractReader = factory.Services.GetRequiredService<IReportContractReader>();
var contract = await contractReader.GetReportContractAsync(journeyId);
contract.Should().NotBeNull();
contract!.Stages.Should().HaveCount(3);

var scoreProvider = factory.Services.GetRequiredService<IJourneyScoreProvider>();
var scores = await scoreProvider.GetScoresAsync(journeyId);
// scores is null if no M-06 mock is wired; test verifies the call pattern
```

---

## Common Failure Modes

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `kpi.weight_sum_invalid` on valid weights | Floating-point rounding in `decimal` sum | Use `decimal` not `double` throughout; sum = `100.00m` exactly |
| Snapshot mutable after entity update | `JourneySnapshotSerializer` serializing live entity references instead of copying values | Ensure snapshot is a deep copy (JSON-serialize to string, then deserialize) |
| P-02 can publish versions | Permission check missing on `journey.publish` scope | Add `[Authorize(Policy = "JourneyPublish")]` attribute to `JourneyVersionsController.Publish` |
| E2E tests fail with login timeout | TOTP secret not seeded or expired | Re-seed test user MFA secret; ensure system clock is synced |
| `idx_journeys_name_ci` unique violation on valid rename | Archived journey's old name blocking reuse | Confirm `WHERE status <> 'Archived'` partial index was applied; check migration |
