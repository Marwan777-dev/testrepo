# Quickstart: CX Metrics & KPI Engine (M-06) + Platform Settings

**Feature**: 003-kpi-engine-settings | **Date**: 2026-06-21

This guide proves the feature works end-to-end on a freshly provisioned tenant. It validates the four P1 user stories (US-1 catalogue, US-2 KPI config, US-3 CXI, US-4 ScoringConfig), one P2 story (US-6 Organization), and the cross-tenant isolation pass condition (SC-005 / GP-04).

For implementation details, follow the canonical artefacts: [spec.md](spec.md), [plan.md](plan.md), [data-model.md](data-model.md), and `contracts/*.md`. This file is a runbook, not a substitute.

---

## Prerequisites

1. Docker Desktop running (Testcontainers + Postgres + on-prem object-storage stand-in).
2. .NET 10 SDK + Node 20+.
3. `appsettings.local.json` populated in `tests/Nabadat.Portal.E2ETests/` with seeded portal test-user credentials and TOTP secret. (Template shipped at `tests/Nabadat.Portal.E2ETests/Infrastructure/appsettings.local.json.template`; never committed.)
4. Branch `003-kpi-engine-settings` checked out.

---

## Boot the stack

```powershell
# Backend host (Kestrel + Postgres + migrations)
Stop-Process -Name "Nabadat.TenantAdmin" -ErrorAction SilentlyContinue | Out-Null
dotnet build Nabadat.TenantAdmin.sln
dotnet run --project src/Nabadat.TenantAdmin/Nabadat.TenantAdmin.csproj

# In a second terminal — frontend dev server
cd frontend
Remove-Item -Recurse -Force node_modules/.vite -ErrorAction SilentlyContinue
npm run dev   # listens on http://localhost:5173
```

The portal proxies `/api` → `https://localhost:7002` (self-signed dev cert; `secure: false` in `vite.config.ts`).

---

## Provision a fresh tenant

```powershell
# Apply all migrations including M06_Baseline.sql and M11_OrganizationSettings.sql
dotnet build tools/Nabadat.Migrations
dotnet run --project tools/Nabadat.Migrations -- --target=tenant --tenant-slug=quickstart-tenant
```

Verification (psql one-liner against the new schema):

```powershell
psql $env:DEV_PG_CONNECTION -c "SET search_path TO tenant_quickstart_tenant; SELECT short_name, kpi_type, calculation_method, scale, is_active, target FROM kpi_definitions ORDER BY (CASE short_name WHEN 'NPS' THEN 0 WHEN 'CSAT' THEN 1 WHEN 'CES' THEN 2 WHEN 'CXI' THEN 3 WHEN 'FCR' THEN 4 WHEN 'VFM' THEN 5 WHEN 'AgentScore' THEN 6 WHEN 'CHS' THEN 7 ELSE 99 END), created_at;"
```

**Expected**: 8 rows in canonical order (NPS, CSAT, CES, CXI, FCR, VFM, AgentScore, CHS), all `is_active=true`, with the right `calculation_method` and `scale` per `data-model.md` §4.

---

## US-1: Browse the catalogue

1. In the portal, sign in as **P-01** (CX Program Manager).
2. Navigate to `/kpi-management`.
3. Verify the header subtitle reads **"8 Active KPIs"**.
4. Verify the table lists the 8 standard KPIs in canonical order.
5. Toggle **"Active only"** off → all 8 rows remain visible (none inactive yet) → toggle back on.
6. Change **Type** filter to **Custom** → table is empty with the design-system empty-state component.
7. Click the **NPS** row → land on `/kpi-management/<nps_id>` (KPI Configuration page in edit mode).

**Pass criteria**: matches SC-001 (8 standard KPIs visible immediately on a fresh tenant; page renders within 1.5 s — verify via DevTools Network panel).

---

## US-2: Create a custom KPI

1. On `/kpi-management`, click **"+ Add KPI"** → land on `/kpi-management/new`.
2. Fill: Short Name = `QUAL`, Full Name = `Service Quality`, Calculation Method = `Weighted Average`, Scale = `1–7`, Min Description (EN) = `Very poor` (AR = `ضعيف جدًا`), Max Description (EN) = `Excellent` (AR = `ممتاز`), Threshold x = `20`, y = `70`, Target = `80`, Active checked, Show on Dashboard unchecked.
3. Confirm the live preview updates within ~100 ms of each field change (the gauge re-centers the target marker, the question preview re-renders with the new scale/labels).
4. Click **Save**. Land back on `/kpi-management`. The new **QUAL** row is visible at the top of the Custom section (Header subtitle now reads **"9 Active KPIs"**).
5. Open the QUAL row, change Full Name to `Service Quality Score`, hit Save. Confirm the row reflects the new full name.

**Audit check** (psql):

```powershell
psql $env:DEV_PG_CONNECTION -c "SET search_path TO tenant_quickstart_tenant; SELECT event_type, payload->>'action', payload->>'kpi_short_name' FROM event_log WHERE event_type = 'settings.changed' ORDER BY created_at DESC LIMIT 4;"
```

**Expected**: rows with `action=updated` (most recent) and `action=created` (the QUAL create); kpi_short_name = `QUAL`.

---

## US-3: Configure CXI

1. From `/kpi-management`, open the **CXI** row.
2. Verify the Question Preview card is HIDDEN; the Scale and Representation Style fields are ABSENT; the Calculation Method field reads **"Weighted Composite"** and is read-only.
3. In the **KPI Weights** table, set: NPS=3, CSAT=2, CES=1. The Effective % column should show **50.0% / 33.3% / 16.7%**.
4. The Active checkbox should be enabled (≥ 2 non-zero weights). Check it. Save.
5. Open the **CSAT** row → uncheck Active. Confirm the blocking confirmation dialog appears (none of CSAT's bindings created yet in this fresh tenant, so the dialog skips the touchpoint count; it still appears because CSAT is a CXI member — the cascade behaviour applies).
6. Re-open the **CXI** row → the weights table now lists only NPS + CES with proportions **75.0% / 25.0%** (CSAT was auto-removed).

**Audit check**:

```powershell
psql $env:DEV_PG_CONNECTION -c "SET search_path TO tenant_quickstart_tenant; SELECT payload->>'action', payload->>'kpi_short_name', payload->'cxi_side_effect' FROM event_log WHERE event_type='settings.changed' AND payload->>'action'='deactivated' ORDER BY created_at DESC LIMIT 1;"
```

**Expected**: ONE row with `action=deactivated`, `kpi_short_name=CSAT`, and the `cxi_side_effect` array carrying one tuple for CXI's recomputed effective percentages (NPS=75.0, CES=25.0). Critically, only ONE event row exists for this deactivation (per FR-026 + Clarifications round 1 Q2).

---

## US-4: Configure ScoringConfig (Customer Journey settings)

1. Navigate to `/settings` → click **Customer Journey**.
2. Verify the five parameters are at their defaults: α = `0.500` (β = `0.500`), MOT Multiplier = `1.5`, Responses Count Floor = `100`, Flag Percentile = `25`, Rolling Window Days = `30`.
3. Drag the α slider to **0.7** → β updates to **0.300** within ~100 ms.
4. Try MOT Multiplier = `2.5` → inline error **"MOT multiplier must be between 1.0 and 2.0."** appears; Save disabled.
5. Reset MOT to `1.5`, Save.

**Audit check**:

```powershell
psql $env:DEV_PG_CONNECTION -c "SET search_path TO tenant_quickstart_tenant; SELECT event_type, payload->>'diff' FROM event_log WHERE event_type='journey.scoring_config.updated' ORDER BY created_at DESC LIMIT 1;"
```

**Expected**: one event row with the per-field diff `{ alpha: {from: 0.500, to: 0.700} }`.

---

## US-6: Organization settings + SVG sanitisation

1. Navigate to `/settings` → click **Organization**.
2. Edit **Name** = `Quickstart Bank`, **Industry** = `Banking`.
3. Upload a benign 500 KB PNG logo → success; the logo renders in the platform topbar within a route navigation.
4. Try to upload a `.pdf` file → blocked with **"Logo must be a PNG, JPG, or SVG file."**
5. Upload an SVG containing a `<script>` tag:

   ```xml
   <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
     <script>alert("xss")</script>
     <circle cx="50" cy="50" r="40" fill="#0D8BBC"/>
   </svg>
   ```

   The upload SUCCEEDS (200) but the response carries `was_sanitised: true`. Fetch the logo's URL — the served SVG MUST NOT contain `<script>`.
6. Upload a `.pdf` renamed to `.svg` (so the content-type detection picks it up wrong and the SVG parser fails) → rejected with **"Logo could not be uploaded — the SVG file contains content that is not allowed."** (API code `LOGO_SVG_UNSAFE_CONTENT`).

**Pass criteria**: SC-009 (no XSS surface) verified by manual inspection of the persisted SVG bytes.

---

## SC-005: Cross-tenant isolation

1. Sign in as a P-01 user of `quickstart-tenant`.
2. Use Postman / curl with the same Bearer token to call `GET /api/v1/kpis/{id}` substituting an `id` that belongs to a DIFFERENT tenant (e.g., from a test fixture).
3. Confirm the response is **404** (not 403, not the actual row).
4. psql against the global `audit_log` table:

   ```powershell
   psql $env:DEV_PG_CONNECTION -c "SELECT actor_id, target_tenant_id, denial_reason FROM audit_log WHERE denial_reason='cross_tenant_access' ORDER BY created_at DESC LIMIT 1;"
   ```

**Expected**: a denial row recording the cross-tenant attempt. SC-005 / GP-04 pass condition met.

---

## Run the full test suite

```powershell
# Unit + Integration (M-06 + M-11 + M-16 deltas)
dotnet test tests/Nabadat.Platform.M06.UnitTests
dotnet test tests/Nabadat.Platform.M06.IntegrationTests

# E2E (Playwright) — requires the stack from "Boot the stack" + the seeded test user
$env:E2E_BASE_URL = "http://localhost:5173"
dotnet test tests/Nabadat.Portal.E2ETests --filter "FullyQualifiedName~KpiManagementTests|FullyQualifiedName~KpiConfigTests|FullyQualifiedName~CxiConfigTests|FullyQualifiedName~CustomerJourneySettingsTests|FullyQualifiedName~OrganizationSettingsTests"

# Full solution (CI gate)
dotnet test Nabadat.TenantAdmin.sln
```

Pass criteria: every assertion green, every `[TestMethod]` annotated in `tests/Nabadat.Portal.E2ETests/COVERAGE.md` with its US ID.

---

## Tear-down

```powershell
dotnet run --project tools/Nabadat.Migrations -- --target=tenant --tenant-slug=quickstart-tenant --rollback
```

(Rolls back `M06_Baseline_Rollback.sql` + `M11_OrganizationSettings_Rollback.sql`. The corrective `UPDATE scoring_configs SET n_floor=100` is NOT rolled back, because 100 is the canonical default going forward.)

---

## Troubleshooting

- **Vite "Failed to resolve import @/..."** after adding `frontend/src/features/kpi-management/` — restart the dev server and clear the cache: `Remove-Item -Recurse -Force frontend/node_modules/.vite` then `npm run dev`.
- **`dotnet build` fails with MSB3026/MSB3027** — the running `Nabadat.TenantAdmin.exe` is locking its DLLs. Stop it (`Stop-Process -Name "Nabadat.TenantAdmin" -Force`) and re-build.
- **Integration tests fail with "no Docker daemon"** — start Docker Desktop; Testcontainers spins up a per-fixture Postgres on each test run.
- **`Nabadat.Portal.E2ETests` complains about missing Playwright browsers** — run `pwsh tests/Nabadat.Portal.E2ETests/bin/Debug/net10.0/playwright.ps1 install` once.
- **`event_log` missing rows after a save** — confirm M-17's `IEventPublisher` is registered with DI; integration tests for atomicity (`KpiSaveAtomicityTests`) catch this.
