# Published Interfaces: Customer Journey Mapping Module (M-16)

**Module**: M-16 Customer Journey Mapping
**Date**: 2026-06-08

These are M-16's published synchronous interfaces consumed in-process by M-06 and M-07 per AD-01. Consumers depend only on these interface definitions; they MUST NOT reference M-16 concrete types, internal services, or database tables directly.

---

## `IJourneyConfigReader`

**Consumed by**: M-06 (CX Metrics and KPI Engine)  
**Purpose**: Provides M-06 with the journey configuration it needs to compute touchpoint, stage, and journey scores.

### C# Interface Definition

```csharp
namespace Nabadat.Platform.Contracts.M16;

/// <summary>
/// Published interface: M-16 → M-06.
/// M-06 calls this to retrieve journey configuration for score computation.
/// M-06 MUST NOT read M-16 tables directly.
/// </summary>
public interface IJourneyConfigReader
{
    /// <summary>
    /// Returns the journey configuration for scoring.
    /// Returns null if the journey does not exist or has no scoring config.
    /// </summary>
    Task<JourneyConfigDto?> GetJourneyConfigAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>
    /// Returns all active journeys for the current tenant.
    /// Used by M-06 to batch-process score updates.
    /// </summary>
    Task<IReadOnlyList<JourneyConfigDto>> GetActiveJourneyConfigsAsync(CancellationToken ct = default);
}
```

### `JourneyConfigDto`

```csharp
public record JourneyConfigDto(
    Guid JourneyId,
    string Name,
    JourneyConfigStatus Status,
    ScoringConfigDto ScoringConfig,
    IReadOnlyList<StageConfigDto> Stages
);

public record ScoringConfigDto(
    string ModelType,
    string StageWeightMode,
    JsonDocument? NormalizationParams
);

public record StageConfigDto(
    Guid StageId,
    int SequenceNumber,
    string Name,
    IReadOnlyList<TouchpointConfigDto> Touchpoints
);

public record TouchpointConfigDto(
    Guid TouchpointId,
    string Name,
    bool IsMoT,
    bool IsMandatory,
    bool IsMeasured,
    IReadOnlyList<KpiBindingConfigDto> KpiBindings
);

public record KpiBindingConfigDto(
    string KpiType,
    decimal Weight,
    bool IsPlatformStandard,
    ScoringDirection ScoringDirection
);

public enum JourneyConfigStatus { Draft, Active, Inactive, Archived }
public enum ScoringDirection { Ascending, Descending }
```

### Contract Rules

1. M-16's implementation (`JourneyConfigReaderService`) queries the tenant PostgreSQL schema directly.
2. The DTO is constructed fresh on each call; no cross-request caching.
3. Unmeasured touchpoints (`IsMeasured = false`) are included in the DTO with an empty `KpiBindings` list. M-06 must exclude them from score computation.
4. If a `ScoringConfig` has not been saved for the journey, `ScoringConfig.ModelType` defaults to `"WeightedAverage"` and `NormalizationParams` is null.
5. This interface is registered as `Scoped` in the DI container. M-06 receives it through constructor injection.

---

## `IReportContractReader`

**Consumed by**: M-07 (Dashboards and Reporting)  
**Purpose**: Provides M-07 with report layout and dimension metadata for rendering journey dashboards.

### C# Interface Definition

```csharp
namespace Nabadat.Platform.Contracts.M16;

/// <summary>
/// Published interface: M-16 → M-07.
/// M-07 calls this to retrieve report contract metadata.
/// M-07 MUST NOT read M-16 tables directly.
/// </summary>
public interface IReportContractReader
{
    /// <summary>
    /// Returns the report contract for a journey.
    /// Returns null if the journey does not exist or has no contract yet.
    /// </summary>
    Task<ReportContractDto?> GetReportContractAsync(Guid journeyId, CancellationToken ct = default);

    /// <summary>
    /// Returns report contracts for all active journeys in the current tenant.
    /// </summary>
    Task<IReadOnlyList<ReportContractDto>> GetActiveReportContractsAsync(CancellationToken ct = default);
}
```

### `ReportContractDto`

```csharp
public record ReportContractDto(
    Guid JourneyId,
    string JourneyName,
    DateTime GeneratedAt,
    IReadOnlyList<StageReportDto> Stages,
    IReadOnlyList<string> ScoreDimensions,
    DetectionConfigReportDto DetectionConfig
);

public record StageReportDto(
    Guid StageId,
    string Name,
    int SequenceNumber,
    IReadOnlyList<TouchpointReportDto> Touchpoints
);

public record TouchpointReportDto(
    Guid TouchpointId,
    string Name,
    bool IsMoT,
    IReadOnlyList<string> KpiTypes,
    bool IsMeasured
);

public record DetectionConfigReportDto(
    decimal? PainThreshold,
    decimal? HappyThreshold
);
```

### Contract Rules

1. M-16's implementation reads from `report_contracts.contract_payload` (pre-built `jsonb`) and deserializes to `ReportContractDto`.
2. If the `report_contracts` row does not exist (journey has no stages), the method returns `null`. M-07 must handle null gracefully (skip the journey in reporting).
3. `ScoreDimensions` is always `["journey_score", "stage_score", "touchpoint_score", "kpi_score"]` in Phase 1.
4. Registered as `Scoped` in the DI container.

---

## `IJourneyScoreProvider`

**Consumed by**: Any caller (UI via API, M-07 for enriched reports)  
**Purpose**: On-demand journey score computation. Delegates to M-06's scoring interface, persists the result, and publishes the `journey.score.updated` event.

### C# Interface Definition

```csharp
namespace Nabadat.Platform.Contracts.M16;

/// <summary>
/// Published interface: M-16 (outward-facing).
/// Consumers call this to retrieve the latest computed scores for a journey.
/// M-16 delegates computation to M-06, persists the result, and publishes an event.
/// </summary>
public interface IJourneyScoreProvider
{
    /// <summary>
    /// Computes (or refreshes) scores for the given journey.
    /// Always triggers a fresh computation via M-06.
    /// Returns null if the journey has no measured touchpoints.
    /// </summary>
    Task<JourneyScoreResultDto?> GetScoresAsync(Guid journeyId, CancellationToken ct = default);
}
```

### `JourneyScoreResultDto`

```csharp
public record JourneyScoreResultDto(
    Guid JourneyId,
    decimal? JourneyScore,
    DateTime ComputedAt,
    IReadOnlyList<StageScoreDto> StageScores,
    IReadOnlyList<TouchpointScoreDto> TouchpointScores
);

public record StageScoreDto(
    Guid StageId,
    decimal? Score,
    int MeasuredTouchpointCount
);

public record TouchpointScoreDto(
    Guid TouchpointId,
    decimal? Score,
    IReadOnlyList<KpiScoreDto> KpiScores
);

public record KpiScoreDto(
    string KpiType,
    decimal? Score,
    int ResponseCount
);
```

### Execution Contract

```
GetScoresAsync(journeyId):
  1. Call IJourneyConfigReader.GetJourneyConfigAsync(journeyId)
     → returns JourneyConfigDto (or null → return null)
  2. Call IM06ScoringService.ComputeJourneyScoreAsync(JourneyConfigDto)
     → returns JourneyScoreResultDto from M-06
  3. BEGIN TRANSACTION
     3a. UPSERT journey_scores (INSERT ... ON CONFLICT journey_id DO UPDATE)
     3b. M17EventPublisher.Publish("journey.score.updated", { journeyId, score, computedAt })
  4. COMMIT TRANSACTION
  5. Return JourneyScoreResultDto
```

**Error handling**:
- If M-06's `ComputeJourneyScoreAsync` throws, the transaction in step 3 is not started. The error propagates to the caller (500 or circuit-breaker response).
- If the transaction commit fails, the caller receives an error; retrying `GetScoresAsync` produces a new fresh computation.

**Registered as**: `Scoped` in the DI container.

---

## DI Registration (M-16 module)

```csharp
// In M-16's DI registration extension method:
services.AddScoped<IJourneyConfigReader, JourneyConfigReaderService>();
services.AddScoped<IReportContractReader, ReportContractReaderService>();
services.AddScoped<IJourneyScoreProvider, JourneyScoreProviderService>();
```

All three interfaces are registered in M-16's DI module. Consuming modules (M-06, M-07) receive them through constructor injection. They never instantiate M-16 concrete types directly.

---

## Interface Stability Contract

These interfaces are **stable published contracts**. Breaking changes (removing a method, changing a method signature, adding a required parameter) require a constitution amendment and a versioned migration plan. Additive changes (new optional method, new field in a DTO with a default value) are non-breaking and may be shipped within the same major version.
