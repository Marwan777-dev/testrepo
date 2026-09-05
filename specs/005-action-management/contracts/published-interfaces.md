# Published Interface Contracts: M-15 Action Management

Per architecture-constitution Article 1A rule 3, published cross-module interfaces live in
`Domain/Interfaces/`. This document lists (a) the interfaces M-15 **consumes** from other
modules, and (b) the interfaces M-15 **publishes** for other modules to consume — the
cross-module contract surface, distinct from the internal REST API in `api-endpoints.md`.

---

## Consumed — M-06 (`Nabadat.KpiManagement`)

### `IKpiConfigReader` (existing, stable — consumed directly)

`Nabadat.KpiManagement.Application.Kpis.Interfaces.IKpiConfigReader` — already published for
M-01/M-07/M-09. M-15 takes a direct project reference (same pattern as those consumers) and
calls:

- `GetActiveAsync()` — populates the KPI select (BR-002: only Active KPIs); drives
  `KpiOptionsFilter` (excludes already-chosen + deactivated KPIs, BR-001/BR-002).
- `GetByIdAsync(kpiId)` / `GetByShortNameAsync(shortName)` — KPI name/metadata for display
  (mini chips, outcome labels, tooltips).

### `IKpiScoreReader` (M-15-owned inbound port — no real M-06 implementation exists yet)

Because M-06 has no score-computation engine (research.md §4, coordination-log.md C-01), M-15
defines and owns this port itself — mirroring the exact dependency-inversion shape M-16 already
uses for `IActiveKpiCatalogReader` (M-16 owns the port; the host wires a real M-06-backed
adapter when one exists; the module ships its own default otherwise).

```csharp
namespace Nabadat.ActionManagement.Domain.Interfaces;

public interface IKpiScoreReader
{
    /// <summary>Live current score for a KPI, on M-06's native scale. Null if unavailable.</summary>
    Task<decimal?> GetCurrentScoreAsync(Guid kpiId, CancellationToken ct = default);

    /// <summary>Historical score for a KPI as of a specific date (baseline capture/recapture,
    /// retro-dating). Null if M-06 has no score for that date — callers surface ERR-5.</summary>
    Task<decimal?> GetHistoricalScoreAsync(Guid kpiId, DateOnly asOfDate, CancellationToken ct = default);

    /// <summary>KPI's live score normalised to a 0-100 index, used only for the Planned-card
    /// "lowest current score" fallback (FR-M15) when no Baseline exists yet. Null if unavailable.</summary>
    Task<decimal?> GetNormalisedIndexAsync(Guid kpiId, CancellationToken ct = default);
}
```

**Default adapter (shipped today)**: `NullKpiScoreReader` — returns `null` from every method,
deterministically. Baseline capture against a `null` result raises `NoBaselineScoreException`
(surfaced as ERR-5); Planned-card fallback with a `null` normalised index falls back to
KPI-name ordering (documented degraded behaviour, not a crash).

**Real adapter (future, C-01)**: registered by the host (`Nabadat.TenantAdmin`) once M-06 ships
score storage — zero change to `Nabadat.ActionManagement` beyond the DI registration swap.

### KPI deactivation/reactivation signal (consumed indirectly — no new interface)

M-15 does **not** define an inbound-event interface for this. It reads the **shared**
`event_log` table (already mapped by every module that writes to it) filtering
`event_type = 'settings.changed' AND entity_type = 'kpi'`, inspecting the diff for an
`{ field: "active", from, to }` entry, watermarked via `Action.last_kpi_event_watermark` /
`ActionSettings.last_kpi_event_watermark` (data-model.md §1/§3). This is a **read of a shared
table this module already legitimately writes to for its own audit events** — not a
cross-module table read of another module's owned data — so it does not require a published
interface or violate Article 3.2 (event_log is the M-17-owned coordination mechanism precisely
for this kind of independent consumption, Article 3.1).

---

## Published — for M-07 (`Nabadat.Dashboards`, does not exist under `src/` yet)

### `IActionOverlayReader` (M-15-owned, forward contract only)

Per INT-02 (SRS §12.2): M-07's trend-analysis chart SHALL offer an option to overlay
Planned/Active/Completed Actions on KPI trend lines. M-15 exposes this now so a future M-07
has something to consume without an M-15 code change (coordination-log.md C-04):

```csharp
namespace Nabadat.ActionManagement.Domain.Interfaces;

public interface IActionOverlayReader
{
    /// <summary>Actions targeting a given KPI within a date window, for chart-annotation overlay.
    /// Archived Actions are excluded by default (INT-02).</summary>
    Task<IReadOnlyList<ActionOverlayEntry>> GetActionsForKpiAsync(
        Guid kpiId, DateOnly windowStart, DateOnly windowEnd, CancellationToken ct = default);
}

public sealed record ActionOverlayEntry(
    Guid ActionId, string ActionName, string Status,
    DateOnly ActionStartDate, DateOnly ActionEndDate,
    DateOnly TargetStartDate, DateOnly LatestTargetDate);
```

No consumer exists yet; this is a skeleton per the same convention `IKpiConfigReader` documents
("Skeleton only" — see M-06's own doc comment) — implemented against M-15's real tables from
day one (unlike `IKpiScoreReader`, this one has no upstream blocker).

---

## Published — for M-09 (`Nabadat.Notifications`, does not exist under `src/` yet)

None. INT-03 explicitly postpones all user alerting to M-09 in full; M-15 ships zero
notification-triggering interfaces. M-09, once it exists, subscribes to M-15's audit events via
M-17 independently (event-driven mode, Article 3.1) — no direct interface needed.
