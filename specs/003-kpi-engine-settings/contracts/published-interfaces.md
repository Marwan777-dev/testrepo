# Published Interfaces: M-06 + M-11 + M-16 deltas

**Feature**: 003-kpi-engine-settings | **Date**: 2026-06-21

These are the **in-process, synchronous published interfaces** that cross module boundaries per AD-01 / AMENDMENT-006. Consumers depend only on these interface definitions — never on concrete types, internal services, or database tables of the publishing module.

| Interface | Publisher | Consumers | Purpose |
|-----------|-----------|-----------|---------|
| `IKpiConfigReader` | M-06 | M-01, M-07, M-09 | Read active KPI definitions for question authoring, dashboard rendering, alerting |
| `IJourneyBindingQuery` | M-16 | M-06 | FR-026 binding-usage probe (touchpoint + journey counts) |
| `IScoringConfigStore` | M-16 | M-06 | FR-053–FR-061 read/write of `scoring_configs` |
| `IIndustryEnumProvider` | M-11 | M-06 | FR-050 canonical industry list |
| `IOrganizationSettingsStore` | M-11 | M-06 | FR-050–FR-052 Organization read/write |
| `ILogoStore` | M-11 | M-06 | FR-050 logo blob storage |

All interfaces and DTOs live under `Nabadat.Platform.Contracts.<Module>` namespaces in a contracts-only assembly per AD-01.

---

## 1. `IKpiConfigReader` (M-06 publishes)

```csharp
namespace Nabadat.Platform.Contracts.M06;

/// <summary>
/// Published interface: M-06 → M-01 / M-07 / M-09.
/// Read-only access to the KPI catalogue for question authoring (M-01),
/// dashboard rendering (M-07), and alert evaluation (M-09).
/// Consumers MUST NOT read M-06 tables directly (AD-01).
/// </summary>
public interface IKpiConfigReader
{
    /// <summary>Returns all active KPIs for the current tenant, in canonical order.</summary>
    Task<IReadOnlyList<KpiDefinitionDto>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Returns a single KPI's full configuration by id; null if not found.</summary>
    Task<KpiDefinitionDto?> GetByIdAsync(Guid kpiId, CancellationToken ct = default);

    /// <summary>Returns a single KPI's configuration by short_name (case-insensitive); null if not found.</summary>
    Task<KpiDefinitionDto?> GetByShortNameAsync(string shortName, CancellationToken ct = default);

    /// <summary>
    /// Returns the CXI ScoreSnapshot member breakdown for M-07 dashboard rendering.
    /// Returns null when CXI is inactive or has fewer than 2 members.
    /// The returned `composite_score` is computed live by M-06 (when the score-computation
    /// engine ships) or read from the latest cached snapshot (out of scope here).
    /// </summary>
    Task<CxiSnapshotDto?> GetCxiSnapshotAsync(CancellationToken ct = default);
}

public record KpiDefinitionDto(
    Guid Id,
    string ShortName,
    string FullName,
    KpiType KpiType,
    bool IsComposite,
    CalculationMethod CalculationMethod,
    int? TopNValue,
    Scale? Scale,
    BilingualText? MinScaleDescription,
    BilingualText? MaxScaleDescription,
    RepresentationStyle? RepresentationStyle,
    EmojiSet? EmojiSet,
    decimal? Target,
    bool IsActive,
    bool ShowOnDashboard,
    KpiThresholdDto Thresholds,
    IReadOnlyList<KpiPerspectiveDto> Perspectives,
    IReadOnlyList<CxiWeightDto>? CxiWeights);

public record KpiThresholdDto(
    decimal LowerBound,
    decimal X,
    decimal Y,
    decimal UpperBound);

public record KpiPerspectiveDto(
    Guid Id,
    string Label,
    short DisplayOrder);

public record CxiWeightDto(
    Guid MemberKpiId,
    string MemberShortName,
    int Weight,
    decimal EffectivePercentage);

public record CxiSnapshotDto(
    decimal CompositeScore,
    IReadOnlyList<CxiMemberBreakdownDto> MemberBreakdown);

public record CxiMemberBreakdownDto(
    Guid KpiId,
    string KpiShortName,
    decimal Score,
    decimal EffectivePercentage);

public record BilingualText(string En, string Ar);

public enum KpiType { Standard, Custom }

public enum CalculationMethod { WeightedAverage, TopNBox, NPSStandard, WeightedComposite }

public enum Scale { Scale0_10, Scale1_3, Scale1_5, Scale1_7, Scale1_10, Scale1_100, Nps }

public enum RepresentationStyle { Number, Stars, Emoji, Slider }

public enum EmojiSet { FaceClassic, HandThumbs }
```

**Implementation**: `Nabadat.Platform.M06.Application.KpiConfigReader` (M-06 internal) reads the four M-06 tables and assembles the DTO. The implementation is registered with DI; only `IKpiConfigReader` is exported across the module boundary.

**No write methods** on this interface — M-06's writes go through `KpiSaveService` (internal). Consumers cannot mutate the catalogue.

---

## 2. `IJourneyBindingQuery` (M-16 publishes) — NEW

```csharp
namespace Nabadat.Platform.Contracts.M16;

/// <summary>
/// Published interface: M-16 → M-06.
/// Returns the count of active touchpoints and distinct journeys
/// where the given KPI is bound. M-06 calls this to assemble the
/// FR-026 deactivation confirmation message and to detect when a
/// scale change affects existing bindings (FR-017).
/// </summary>
public interface IJourneyBindingQuery
{
    /// <summary>
    /// Returns binding-usage counts for the given KPI on the current tenant.
    /// Returns (0, 0) for an unbound KPI.
    /// </summary>
    Task<KpiBindingUsage> GetKpiBindingUsageAsync(
        Guid kpiId,
        CancellationToken ct = default);
}

public record KpiBindingUsage(int TouchpointCount, int JourneyCount);
```

**Implementation**: in M-16, a new `JourneyBindingQueryService` (in `src/Nabadat.Platform.M16/Application/Bindings/`) that runs:

```sql
SELECT COUNT(DISTINCT t.id) AS touchpoint_count,
       COUNT(DISTINCT s.journey_id) AS journey_count
  FROM kpi_bindings kb
  JOIN touchpoints t ON t.id = kb.touchpoint_id
  JOIN stages s ON s.id = t.stage_id
  JOIN journeys j ON j.id = s.journey_id
 WHERE kb.kpi_id = @kpiId
   AND j.status != 'Archived'
```

against M-16's own tenant-schema tables. The implementation is the only point that touches M-16's tables on M-06's behalf.

---

## 3. `IScoringConfigStore` (M-16 publishes) — NEW

```csharp
namespace Nabadat.Platform.Contracts.M16;

/// <summary>
/// Published interface: M-16 → M-06.
/// Read and write the tenant's ScoringConfig row. M-06's Customer Journey
/// settings page edits the row through this interface, never via direct
/// table access.
/// </summary>
public interface IScoringConfigStore
{
    Task<ScoringConfigDto> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Atomic update: persists the row AND emits the journey.scoring_config.updated event
    /// in one PostgreSQL transaction. Returns the persisted DTO.
    /// A no-op update (payload matches current state) returns the current DTO and emits no event.
    /// </summary>
    Task<ScoringConfigDto> UpdateAsync(
        ScoringConfigUpdate update,
        Guid actorId,
        CancellationToken ct = default);
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

`beta` is NOT carried — always derived as `1.000m - alpha` on read (R6). The DTO returned to M-06 includes only `alpha`; the M-06 controller computes `beta` and returns both to the wire.

**Implementation**: in M-16, a new `ScoringConfigStore` service (in `src/Nabadat.Platform.M16/Application/ScoringConfig/`) wrapping the `scoring_configs` table via M-16's existing repository pattern.

---

## 4. `IIndustryEnumProvider` (M-11 publishes) — NEW

```csharp
namespace Nabadat.Platform.Contracts.M11;

/// <summary>
/// Published interface: M-11 → M-06.
/// Provides the canonical industry enum used by both tenant provisioning
/// and Organization Settings. Single source of truth (R13).
/// </summary>
public interface IIndustryEnumProvider
{
    IReadOnlyList<string> GetAll();
    bool IsValid(string value);
}
```

Returns: `["Banking", "Telecommunications", "Government", "Automotive", "Entertainment", "Services"]` (canonical order).

---

## 5. `IOrganizationSettingsStore` (M-11 publishes) — NEW

```csharp
namespace Nabadat.Platform.Contracts.M11;

public interface IOrganizationSettingsStore
{
    Task<OrganizationSettingsDto> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Atomic update: persists name + industry AND emits the settings.changed event
    /// in one transaction. A no-op update emits no event.
    /// </summary>
    Task<OrganizationSettingsDto> UpdateAsync(
        OrganizationSettingsUpdate update,
        Guid actorId,
        CancellationToken ct = default);

    /// <summary>
    /// Atomic update of logo_blob_ref: persists the new blob ref AND emits the
    /// settings.changed event with action='logo_replaced' in one transaction.
    /// </summary>
    Task<OrganizationSettingsDto> UpdateLogoRefAsync(
        LogoBlobRef? newRef,
        Guid actorId,
        CancellationToken ct = default);
}

public record OrganizationSettingsDto(
    string Name,
    string Industry,
    LogoBlobRef? LogoRef,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public record OrganizationSettingsUpdate(
    string Name,
    string Industry);
```

---

## 6. `ILogoStore` (M-11 publishes) — NEW

```csharp
namespace Nabadat.Platform.Contracts.M11;

/// <summary>
/// Published interface: M-11 → M-06.
/// Region-routed blob storage abstraction (T-04). SaaS implementation
/// targets S3-compatible storage; on-prem implementation targets local
/// filesystem under the configured DEPLOYMENT_REGION mount.
/// The store does NOT sanitise content — that is M-06's responsibility (R1).
/// </summary>
public interface ILogoStore
{
    Task<LogoBlobRef> PutAsync(
        string contentType,
        Stream payload,
        CancellationToken ct = default);

    Task<Stream> GetAsync(LogoBlobRef blobRef, CancellationToken ct = default);

    /// <summary>Returns the public-facing URL the frontend uses to render the logo.</summary>
    Uri GetPublicUrl(LogoBlobRef blobRef);
}

public record LogoBlobRef(string StorageKey);
```

The `PutAsync` callsite (M-06's `OrganizationController`) is responsible for ensuring the `payload` stream contains sanitised bytes for SVG uploads (R1).

---

## Cross-Module Call Diagram

```text
                ┌────────────────────────────────────────┐
                │              M-06 (this feature)        │
                │  ┌─────────────────┐  ┌───────────────┐ │
   Frontend ───►│  │  KpisController  │  │ Settings ctlrs│ │
                │  └────────┬────────┘  └───────┬────────┘ │
                └───────────┼───────────────────┼──────────┘
                            │                   │
       ┌────────────────────┴─────┐            │
       │                          │            │
       ▼                          ▼            ▼
   ┌────────────┐         ┌─────────────┐  ┌─────────────────┐
   │   M-16     │         │   M-17      │  │       M-11      │
   │            │         │             │  │                 │
   │ IJourney   │         │ IEvent      │  │ IIndustry       │
   │ Binding    │         │ Publisher   │  │  EnumProvider   │
   │   Query    │         │  (audit)    │  │ IOrganization   │
   │ IScoring   │         │             │  │  SettingsStore  │
   │  Config    │         └─────────────┘  │ ILogoStore      │
   │   Store    │                          │                 │
   └────────────┘                          └─────────────────┘
                            ▲
   M-06's IKpiConfigReader  │ consumed by M-01 / M-07 / M-09
   ────────────────────────┘
```

All arrows are synchronous in-process method calls. M-17 writes are in the same transaction as the data write (constitution Section 4 + AMENDMENT-007).
