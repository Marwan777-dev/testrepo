# Phase 0 Research: CX Metrics & KPI Engine (M-06) + Platform Settings

**Feature**: 003-kpi-engine-settings | **Date**: 2026-06-21 | **Status**: Complete

This document resolves the technical unknowns the spec deferred to planning. All NEEDS CLARIFICATION items from [plan.md](plan.md) are answered here. Format per Spec Kit: Decision / Rationale / Alternatives Considered.

---

## R1. SVG sanitiser library + ruleset

**Decision**: Use **`Ganss.Xss.HtmlSanitizer`** (NuGet `HtmlSanitizer`, MIT-licensed, actively maintained) configured for SVG with a strict element + attribute allow-list. The sanitiser is invoked by an M-06 wrapper class `SvgSanitiser` (see `Application/Organization/SvgSanitiser.cs` in the plan's Project Structure).

### Allow-list

**Elements**: `svg, g, path, rect, circle, ellipse, line, polyline, polygon, text, tspan, defs, linearGradient, radialGradient, stop, symbol, title, desc`.

**Disallowed (stripped)**: `script, foreignObject, iframe, object, embed, audio, video, animate, animateTransform, animateMotion, set, image, use[href^="http"], use[href^="data:"], use[href^="file:"]`.

**Attributes**:

- Always allowed: `xmlns, viewBox, width, height, x, y, x1, y1, x2, y2, cx, cy, r, rx, ry, d, points, fill, stroke, stroke-width, transform, id, class, opacity, fill-opacity, stroke-opacity, gradientUnits, offset, stop-color, stop-opacity`.
- Stripped: every `on*` event-handler attribute (`onload`, `onerror`, `onclick`, `onmouseover`, etc.), every `xlink:href` referencing an external URL, every `href` referencing an external URL on `<use>`, every `style` attribute containing `url(` or `expression(` (eliminating CSS-injected scripts).

### Rejection

The sanitiser RETURNS sanitised bytes for any payload it can make safe. It throws `SvgUnsafeContentException` only when the input bytes cannot be parsed as SVG at all (empty file, binary garbage, JSON / HTML masquerading as `image/svg+xml`). The API layer converts the exception to `400 LOGO_SVG_UNSAFE_CONTENT` per FR-050.

### Rationale

- `Ganss.Xss.HtmlSanitizer` is MIT-licensed (aligns with the platform's FluentAssertions 6.12 pin policy of avoiding paid licences).
- Actively maintained: latest release within the last 90 days; ≥ 60 contributors.
- Built on AngleSharp parser, which is the same parser used inside ASP.NET Core's HTML helpers — proven against malformed input.
- Configurable allow-list (vs. block-list) — failure-secure: anything not on the list is dropped.

### Alternatives considered

| Library | Rejected because |
|---------|------------------|
| **DOMPurify (via Node.js sidecar)** | Adds a Node runtime to the .NET host; cross-language ops + cold-start risk + a new process boundary. The Ganss library is C#-native and runs in-process. |
| **Custom regex-based stripper** | XSS bypasses against regex sanitisers are well-documented (mXSS, mutation XSS). Whitelist-based AST traversal is the only safe approach. |
| **Hand-rolled SVG parser** | The platform already includes AngleSharp transitively; reusing it via Ganss avoids re-implementing a battle-tested parser. |
| **No sanitiser; render via `<img src=…>` only** | Storage still holds attacker bytes; a single inline-render slip-up undoes the protection. Per Clarifications session 2026-06-21 round 2 Q2, this option was explicitly rejected. |
| **Reject SVG entirely (PNG/JPG only)** | Tightens the SRS-mandated SVG support (FR-S9 / FR-050); rejected per the same clarifications round. |

---

## R2. Emoji Set v1 catalogue: per-K glyph slot assignments

**Decision**: Two static sets — `FaceClassic` and `HandThumbs` (per Clarifications session 2026-06-21 Q4). Each set defines a **canonical 11-glyph ordered sequence** indexed `0..10` (matching the 0–10 scale). At render time for a scale with K distinct values, the platform picks K glyphs from the sequence using **linearly-spaced indices** (rounded to nearest), with the boundary glyphs always pinned to indices 0 and 10.

### `FaceClassic` sequence (worst → best, index 0 → 10)

| Index | Glyph | Description |
|------:|-------|-------------|
| 0 | 😡 | extremely angry |
| 1 | 😠 | angry |
| 2 | 😞 | disappointed |
| 3 | 🙁 | slightly frowning |
| 4 | 😕 | confused |
| 5 | 😐 | neutral |
| 6 | 🙂 | slightly smiling |
| 7 | 😊 | smiling |
| 8 | 😄 | grinning |
| 9 | 😁 | beaming |
| 10 | 😍 | heart eyes |

### `HandThumbs` sequence (worst → best, index 0 → 10)

| Index | Glyph | Description |
|------:|-------|-------------|
| 0 | 👎🏿 | strong down (darkest skin tone) |
| 1 | 👎🏾 | down |
| 2 | 👎🏽 | down |
| 3 | 👎🏼 | mild down |
| 4 | 👎🏻 | slight down |
| 5 | ✋ | flat hand (neutral) |
| 6 | 👍🏻 | slight up |
| 7 | 👍🏼 | mild up |
| 8 | 👍🏽 | up |
| 9 | 👍🏾 | up |
| 10 | 👍🏿 | strong up (darkest skin tone) |

### Per-K slot rule

For a scale with K distinct values (K ∈ {3, 5, 7, 10, 11, 100}):

- K = 3 → indices `[0, 5, 10]` (worst / neutral / best).
- K = 5 → indices `[0, 3, 5, 8, 10]`.
- K = 7 → indices `[0, 2, 4, 5, 6, 8, 10]`.
- K = 10 → indices `[1..10]` (the 1–10 scale skips the "worst-of-worst" anchor 0; rationale: a 1–10 scale's minimum already represents "worst").
- K = 11 → indices `[0..10]` (the 0–10 scale uses the full sequence).
- K = 100 → emoji is NOT a sensible representation for 100 distinct values; the form layer hides the Emoji option when scale = 1–100 (the dropdown does not list it). This is a UI-only constraint; no FR change.

### Rationale

- Linearly-spaced selection keeps each set's emotional gradient intact across scale lengths.
- Pinning indices 0 and 10 to the worst/best anchors guarantees the visual extremes match the numeric extremes.
- K=100 Emoji is intentionally unavailable — emoji-per-value above ~11 is unreadable; the spec already supports Number / Stars for fine-grained scales.

### Alternatives considered

- **Per-K hand-curated tables** — gives perfect per-K glyph choices but adds 6× the data, with maintenance overhead when a new set is added. Linear spacing is good enough for v1.
- **Dynamic emoji generation** — out of scope.
- **Tenant-customisable sets** — explicitly rejected in Clarifications round 1 Q4 (Option D).

---

## R3. Logo storage abstraction (`ILogoStore`)

**Decision**: `ILogoStore` is an M-11-published interface with two members:

```csharp
public interface ILogoStore
{
    Task<LogoBlobRef> PutAsync(
        Guid tenantId,
        string contentType,       // "image/png" | "image/jpeg" | "image/svg+xml"
        Stream payload,
        CancellationToken ct = default);

    Task<Stream> GetAsync(LogoBlobRef blobRef, CancellationToken ct = default);
}

public record LogoBlobRef(string StorageKey);  // e.g., "tenants/{tenantId}/branding/logo.png"
```

Implementation routes the call to the tenant's configured storage region (resolved from M-11 tenant provisioning state — T-04 compliant). In SaaS mode the implementation targets an S3-compatible object store; in on-prem mode it targets a local filesystem mount. The interface is identical in both modes (AD-05).

### Rationale

- Abstraction lets M-06 stay storage-agnostic.
- Per-tenant region routing without any runtime jurisdiction logic (T-04).
- `LogoBlobRef` is an opaque, durable handle stored in `organization_settings.logo_blob_ref` (nullable when no logo is set).

### Alternatives considered

- **Logo stored as a `bytea` column** — works but blows the PostgreSQL row size budget for tenants with multi-MB logos; harder to CDN-front.
- **Pre-signed direct-upload URLs** — adds a round-trip and an S3-coupling. The simpler "POST through the API" path matches the SRS (`POST /api/v1/tenant/organization/logo`) without exposing implementation details to the frontend.

---

## R4. M-16 published-interface surface additions

**Decision**: Add two new interfaces to M-16's published-interface surface (defined in `Nabadat.Platform.Contracts.M16`):

```csharp
public interface IJourneyBindingQuery
{
    Task<KpiBindingUsage> GetKpiBindingUsageAsync(Guid kpiId, CancellationToken ct = default);
}

public record KpiBindingUsage(int TouchpointCount, int JourneyCount);

public interface IScoringConfigStore
{
    Task<ScoringConfigDto> GetAsync(Guid tenantId, CancellationToken ct = default);
    Task<ScoringConfigDto> UpdateAsync(Guid tenantId, ScoringConfigUpdate update, CancellationToken ct = default);
}

public record ScoringConfigDto(
    decimal Alpha,
    decimal MotMultiplier,
    int NFloor,
    int FlagPercentile,
    int RollingWindowDays);

public record ScoringConfigUpdate(
    decimal Alpha,
    decimal MotMultiplier,
    int NFloor,
    int FlagPercentile,
    int RollingWindowDays);
```

`IJourneyBindingQuery.GetKpiBindingUsageAsync` returns `(touchpoint_count, journey_count)` — the two numbers FR-026's deactivation confirmation needs. `IScoringConfigStore` is M-06's only path to `scoring_configs`; M-06 NEVER reads or writes the M-16-owned `scoring_configs` table directly (AD-01). β is not part of the DTO — always derived on read.

### Rationale

- Keeps M-06 free of M-16's table layout knowledge.
- `IScoringConfigStore.UpdateAsync` returns the updated DTO so callers don't need a follow-up GET.
- The DTO carries primitive types only — no entity leakage across the module boundary.

### Alternatives considered

- **M-06 reads M-16 tables directly** — violates AD-01.
- **One large `IJourneyConfig` interface** — couples binding-usage to all the other journey-config concerns; the new `IJourneyBindingQuery` interface is intentionally narrow so M-06 doesn't transitively depend on the rest of M-16's surface.
- **`ScoringConfigDto` includes β** — would risk α + β ≠ 1.0 through a partial update; β is always derived from α (FR-054).

---

## R5. CXI cascade transaction model

**Decision**: `KpiActivationCommandHandler.Handle({active:false, confirm:true})` runs as a single PostgreSQL transaction:

1. SELECT FOR UPDATE on `kpi_definitions` for the deactivated KPI (row lock).
2. SELECT `cxi_weights` rows where `member_kpi_id = <deactivatedKpiId>` (typically 0 or 1 — but generalised for future multi-CXI scenarios).
3. UPDATE `kpi_definitions` SET `is_active=false`, `show_on_dashboard=false`.
4. DELETE FROM `cxi_weights` WHERE `member_kpi_id = <deactivatedKpiId>`.
5. For each affected CXI row's `cxi_kpi_id`: read its remaining weights, compute the post-removal `effective_percentages` via `CxiWeightNormaliser`, build a per-CXI side-effect tuple `(cxi_kpi_id, removed_member_kpi_id, recomputed_effective_percentages)`.
6. INSERT INTO `event_log` ONE row with `event_type='settings.changed'` and JSON payload `{ kpi_id, diff: { is_active: {from:true, to:false}, show_on_dashboard: {from:..., to:false} }, cxi_side_effect: [...] }` (array of side-effect tuples; empty array if not a CXI member).
7. COMMIT.

Any step failure rolls everything back; no partial state, no orphaned event.

### Rationale

- Single transaction matches the audit "exactly ONE event" rule (FR-026, SC-006, Clarifications round 1 Q2).
- `SELECT FOR UPDATE` prevents concurrent edits from the same tenant from racing the cascade.
- The recompute is small (≤ 7 member KPIs) — runs in-transaction without locking concern.

### Alternatives considered

- **Two events (one per affected entity)** — explicitly rejected in Clarifications round 1 Q2 (Option A); the cascade is captured inside the deactivation event's diff per Option B.
- **Asynchronous cascade via M-17 event subscriber** — would defer the CXI recompute to a separate transaction, breaking GP-01 ("single source of truth, atomic write"). Also adds latency.

---

## R6. α / β floating-point precision

**Decision**: Persist α as PostgreSQL `numeric(4,3)` (i.e., exact 3 decimal places, 0.000–1.000). Derive β at read time as `decimal beta = 1.000m - alpha;` (C# decimal arithmetic, NOT `double` — eliminates IEEE 754 rounding). The frontend slider emits α at 3 dp; the AlphaBetaDeriver returns β at 3 dp.

### Rationale

- `numeric(4,3)` preserves user-entered values exactly (no `double` drift).
- C# `decimal` arithmetic is exact for the operation `1.000 - alpha` at 3 dp.
- β is never stored, so α + β ≠ 1.0 cannot occur even through partial updates.

### Alternatives considered

- **`double`/`float`** — IEEE 754 rounding produces values like `β = 0.30000000000000004` for `α = 0.7`; rejected.
- **Store both α and β** — explicitly forbidden by FR-054 ("β MUST NOT be stored"); risk of α + β ≠ 1.0 through partial update.
- **Persist α as 4 dp** — adds an unused decimal of precision; the slider only emits 3 dp.

---

## R7. KPI catalogue ordering composition

**Decision**: The `GET /api/v1/kpis` list orders rows in two layers:

1. **Layer 1 (canonical for standards)**: A static lookup `Dictionary<string, int>` maps each standard short_name to its canonical position (NPS=0, CSAT=1, CES=2, CXI=3, FCR=4, VFM=5, AgentScore=6, CHS=7). Composing this into PostgreSQL: `ORDER BY CASE short_name WHEN 'NPS' THEN 0 WHEN 'CSAT' THEN 1 ... END NULLS LAST`.
2. **Layer 2 (custom KPIs)**: `ORDER BY created_at DESC` for any row whose short_name is not in the canonical map (i.e., custom KPIs).

The `CASE` expression returns `NULL` for custom KPIs; `NULLS LAST` pushes them after every standard. Within the NULL group, `created_at DESC` orders them newest-first.

### Rationale

- Single query, single sort.
- Canonical order is data-driven (the map is the only place the order is defined; the migration seed uses the same map).
- Tested explicitly by `KpiSeedDataProviderTests.Seed_returns_eight_rows_in_canonical_order`.

### Alternatives considered

- **`ORDER BY` on a sequence column** — would require maintaining a `seed_order` column on `kpi_definitions`. The CASE-based approach keeps the table schema clean.
- **Application-layer sort** — works but adds memory pressure on large catalogues and prevents PostgreSQL from streaming results.

---

## R8. Cursor-based pagination on `GET /api/v1/kpis`

**Decision**: Cursor-paginated per API-04, with cursor format `created_at|id` (base64-encoded). For the typical tenant size (≤ 60 KPIs) the first page returns everything; the cursor exists for future scale and to comply with API-04 uniformly.

- `?cursor=<opaque>` — opaque base64-encoded `<iso8601>|<uuid>` tuple.
- `?limit=<n>` — default 50, max 200.
- Response includes `next_cursor` (null when exhausted).

### Rationale

- Uniform with the rest of the platform.
- The cursor encodes the same (created_at, id) tuple PostgreSQL sorts by, so pagination is stable across inserts.

### Alternatives considered

- **Offset pagination** — explicitly forbidden by API-04.
- **No pagination for catalogue (always return all)** — works today but violates API-04 and locks the team into a hidden tenant-size ceiling.

---

## R9. Frontend gauge — custom SVG vs Recharts

**Decision**: The universal arc gauge is a **custom SVG component** (`frontend/src/components/cx/kpi/UniversalArcGauge.tsx`), not Recharts.

### Rationale

- CLAUDE.md's "When to use Recharts vs Custom SVG" rule explicitly mandates custom SVG for gauges with zone colouring, needle dots, and target markers.
- Recharts has no native gauge component that supports the dual-ring + threshold-band + target-marker geometry the SRS requires.
- Custom SVG is purely a render function over `(value, target, x, y, lower, upper, kpiType, shortName)` — no chart-engine overhead, no third-party API to wrangle.

### Alternatives considered

- **Recharts `PieChart` with a half-rotation hack** — produces a semicircle but lacks the needle dot, target tick mark, and zone colouring; would require custom SVG overlays anyway.
- **`react-gauge-chart`** — exists, but uses Chart.js under the hood; brings a charting engine for one component; brand colours not consistent with the D1–D5 palette without overrides.

---

## R10. Live preview <100 ms re-render budget

**Decision**: The KPI Configuration form holds all field state in a single `useReducer` with the entire configuration as state. The preview panel subscribes to the reducer state via React context (not a global store — local feature scope). Every form field change dispatches an action that updates the reducer in O(1); the preview re-renders via React's normal reconciliation. The gauge SVG is wrapped in `React.memo` keyed on the four values it depends on (value, target, x, y). The Question Preview is memoised on scale + representation style + emoji set.

### Rationale

- Single source of state — no `useState` proliferation that could cause inconsistent renders.
- React 19's reconciliation handles a single-component re-render in well under 16 ms even on cold paint, comfortably inside the 100 ms budget.
- Memoisation isolates the SVG render path from unrelated form changes (e.g., editing the Full Name shouldn't re-render the gauge unless it affects gauge text).

### Alternatives considered

- **Per-field `useState`** — causes a re-render storm on related changes (e.g., editing `x` should also reposition the target marker).
- **Zustand / Redux** — overkill for feature-local state.

---

## R11. RTL parity for gauge labels

**Decision**: The gauge labels (`−100 / 0 / +100` for NPS; `0 / 25 / 50 / 75 / 100` for non-NPS) render with logical CSS direction (`direction: ltr` on the SVG itself, since numbers and decimals are inherently LTR). The SVG's container — and therefore its layout — flips with the page direction (`dir="rtl"` on `<html>`), but the numerals inside stay LTR. This matches industry convention (Arabic UIs use Western digits + LTR numeric grouping per the design system's "Arabic Typography Rules").

### Rationale

- Western digits + LTR numbers are mandated by the design system for the platform's Arabic UI.
- Forcing `direction: ltr` on the gauge SVG isolates the numeric labels from the page's RTL frame without affecting positioning.

---

## R12. Migration ordering + seeding

**Decision**: One migration `M06_Baseline.sql` creates the four M-06 tables and seeds the eight standard KPIs in one transaction. Seed data:

| short_name | full_name | kpi_type | is_composite | calc_method | scale | thresholds (lower, x, y, upper) | target |
|------------|-----------|----------|-------------:|-------------|-------|-------------------------------:|-------:|
| NPS | Net Promoter Score | Standard | false | NPSStandard | Scale0_10 | (-100, 0, 30, 100) | 50 |
| CSAT | Customer Satisfaction Score | Standard | false | WeightedAverage | Scale1_5 | (0, 20, 70, 100) | 80 |
| CES | Customer Effort Score | Standard | false | WeightedAverage | Scale1_7 | (0, 20, 70, 100) | 80 |
| CXI | Customer Experience Index | Standard | **true** | WeightedComposite | NULL | (0, 20, 70, 100) | 80 |
| FCR | First Contact Resolution | Standard | false | WeightedAverage | Scale1_3 | (0, 20, 70, 100) | 80 |
| VFM | Value for Money | Standard | false | WeightedAverage | Scale1_5 | (0, 20, 70, 100) | 80 |
| AgentScore | Agent Score | Standard | false | WeightedAverage | Scale1_5 | (0, 20, 70, 100) | 80 |
| CHS | Customer Happiness Score | Standard | false | WeightedAverage | Scale1_5 | (0, 20, 70, 100) | 80 |

Notes:
- NPS uses `(-100, 0, 30, 100)` per Clarifications round 2 Q1 of session 2026-06-21.
- CXI's scale and representation_style are NULL (composite).
- All eight rows are inserted with `is_active=true`, `show_on_dashboard=false`.
- Idempotency: the seed uses `INSERT … ON CONFLICT (LOWER(short_name)) DO NOTHING` so re-runs are safe.

### Rationale

- Single migration matches DB-05 (atomic).
- Seed data is canonical config, not tenant data — same seed for every tenant.
- ON CONFLICT clause makes the migration safe to re-run during dev/CI.

### Alternatives considered

- **Seed via application start-up** — rejected; constitution and DB-05 prefer migration-time seeding (deterministic at provisioning, atomic).
- **Per-tenant seed via separate migration** — splits one logical change across two migrations; lower atomicity.

---

## R13. Industry enum: single source of truth

**Decision**: `M11.IIndustryEnumProvider.GetAll(): IReadOnlyList<Industry>` returns the six canonical values: `Banking, Telecommunications, Government, Automotive, Entertainment, Services` (in this canonical order). M-06's `OrganizationController` reads this list and exposes it under `GET /api/v1/tenant/organization` as the `industry_options` field. The frontend `IndustryDropdown` consumes this list — no hard-coded enum in TypeScript.

### Rationale

- Single source of truth (the Clarifications-confirmed M-11 industry list).
- Adding a new industry is a one-line change in M-11; the dropdown picks it up automatically.

### Alternatives considered

- **Hard-code in TypeScript** — drift risk vs backend over time.
- **Read from a database table** — overkill for a static six-value list.

---

## R14. E2E project scaffolding (`Nabadat.Portal.E2ETests`)

**Decision**: First E2E project for the `frontend/` workspace in this repo. Scaffolded per CLAUDE.md E2E Test Policy:

- `Microsoft.Playwright.MSTest` references.
- `E2ETestBase.cs` extends `Microsoft.Playwright.MSTest.PageTest`, exposes `SignInAsync(persona)` that drives the portal MFA flow with a seeded test user (credentials in gitignored `appsettings.local.json`).
- Token storage: portal uses `localStorage.session_token` (per CLAUDE.md `frontend/portal/` row in the E2E workspace table).
- `E2E_BASE_URL` default `http://localhost:5173`.
- Each test class corresponds to one user story (`KpiManagementTests.cs` for US-1, etc.).
- `COVERAGE.md` maps each `[TestMethod]` ID → US scenario.

### Rationale

- One project per workspace per CLAUDE.md.
- MSTest is the only test framework that pairs with `Microsoft.Playwright.MSTest`'s `[TestMethod]` attribution.

### Alternatives considered

- **xUnit + Playwright** — works but loses `[TestMethod].AddResultFile` for trace + screenshot attachments visible in the VS Test Explorer.
- **Cypress / Vitest** — non-.NET stack; rejected by CLAUDE.md.

---

## R15. Audit-log diff payload shape for KPI saves

**Decision**: The `settings.changed` event JSON payload for a KPI save is:

```json
{
  "entity_type": "kpi",
  "entity_id": "<uuid>",
  "kpi_short_name": "QUAL",
  "action": "created" | "updated" | "activated" | "deactivated",
  "diff": {
    "<field_name>": { "from": <old_value>, "to": <new_value> },
    ...
  },
  "cxi_side_effect": [{
    "cxi_kpi_id": "<uuid>",
    "removed_member_kpi_id": "<uuid>",
    "recomputed_effective_percentages": { "<member_kpi_id>": <pct>, ... }
  }]
}
```

- `entity_type` is fixed `"kpi"` for KPI events (vs `"organization"` for Organization edits, `"scoring_config"` for ScoringConfig edits).
- `diff` only contains fields that actually changed; `created` action carries the full set; `deactivated` carries `is_active`, `show_on_dashboard`, and the nested `cxi_side_effect` array (which is an empty array when the deactivated KPI was not a CXI member).
- Bilingual fields (`min_scale_description`, `max_scale_description`): the diff captures BOTH the EN and AR versions as separate object values (`min_scale_description: { from: {en:"...", ar:"..."}, to: {en:"...", ar:"..."} }`). This avoids choosing a canonical language for the audit log.

### Rationale

- Single, consistent shape across actions makes the audit consumer's life easy.
- The bilingual-as-object approach is forward-compatible if a third language ships.

### Alternatives considered

- **Full document at each save** — bloats `event_log`; the diff is sufficient.
- **Single-language diff** — discards information; rejected.

---

## All NEEDS CLARIFICATION items resolved. Ready for Phase 1 (data-model, contracts, quickstart).
