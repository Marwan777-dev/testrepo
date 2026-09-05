# API Contract: Journey Configuration (M-16)

**Module**: M-16 Customer Journey Mapping
**Covers**: KPI bindings, scoring configuration, detection configuration
**Date**: 2026-06-08

All endpoints return the API-05 error envelope on non-2xx responses. All require `Authorization: Bearer <session_token>`.

---

## PUT /api/v1/touchpoints/{touchpointId}/kpis

Saves KPI bindings for a touchpoint. This is a full replace — the request body is the complete, authoritative set of KPI bindings for the touchpoint. Existing bindings are deleted and replaced atomically.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "kpiBindings": [
    { "kpiType": "NPS", "weight": 60 },
    { "kpiType": "CSAT", "weight": 40 }
  ]
}
```

**Valid `kpiType` values**:
- Platform-standard (built-in): `NPS`, `CSAT`, `CES`, `FCR`, `AgentSatisfaction`, `VFM`
- Tenant-defined: any `type_key` in `kpi_type_definitions` for this tenant

**Validation rules**:
1. `kpiBindings` may be empty (saves an unmeasured touchpoint — all existing bindings deleted).
2. When non-empty: all `weight` values must be `> 0` and `<= 100`; the sum of all weights must equal exactly `100`.
3. No duplicate `kpiType` values in a single request.
4. Any non-platform-standard `kpiType` must exist in the tenant's `kpi_type_definitions`.

### Response 200 OK

```json
{
  "touchpointId": "uuid",
  "kpiBindings": [
    {
      "kpiBindingId": "uuid",
      "kpiType": "NPS",
      "weight": 60,
      "isPlatformStandard": true,
      "scoringDirection": "Ascending"
    },
    {
      "kpiBindingId": "uuid",
      "kpiType": "CSAT",
      "weight": 40,
      "isPlatformStandard": true,
      "scoringDirection": "Ascending"
    }
  ],
  "isMeasured": true,
  "npsWarning": true,
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

**`npsWarning`**: `true` when `NPS` is in the binding set. This is a non-blocking informational indicator — the response is still 200. The UI MUST display an informational banner on the configuration form when this flag is true: e.g. "NPS is included — ensure your survey distribution supports NPS response scale."

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `kpi.weight_sum_invalid` | 422 | Weights do not sum to 100% |
| `kpi.duplicate_type` | 422 | Same KPI type appears more than once |
| `kpi.unknown_type` | 422 | `kpiType` is not a platform-standard type and not in tenant `kpi_type_definitions` |
| `kpi.individual_weight_invalid` | 422 | Any individual weight is ≤ 0 or > 100 |
| `journey.archived_immutable` | 403 | Parent journey is Archived |

**M-17 event**: `journey.kpi_bindings.updated` published in the same transaction.
**Report contract rebuild**: `ReportContractService.RebuildContractAsync` called in the same transaction.

---

## PUT /api/v1/journeys/{journeyId}/scoring

Saves the strategic scoring configuration for a journey. This is consumed by M-06 via `IJourneyConfigReader`.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "modelType": "WeightedAverage",
  "stageWeightMode": "Equal",
  "normalizationParams": {
    "npsMin": -100,
    "npsMax": 100,
    "normalizeToPercentage": true
  }
}
```

**`modelType`** values: `WeightedAverage` | `HarmonicMean` | `MinScore` — identifies the M-06 scoring algorithm. M-06 is the authority on valid values; M-16 stores and forwards without validating the model type.

**`stageWeightMode`**: `Equal` (default) or `Custom`. If `Custom`, stage weights must be provided in `normalizationParams.stageWeights` as `[{ stageId, weight }]` summing to 100.

**`normalizationParams`**: Arbitrary `jsonb`; structure defined by M-06. M-16 stores and returns it without interpreting content.

### Response 200 OK

```json
{
  "journeyId": "uuid",
  "modelType": "WeightedAverage",
  "stageWeightMode": "Equal",
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

**M-17 event**: `journey.scoring_config.updated` published in the same transaction.

---

## GET /api/v1/journeys/{journeyId}/scoring

Returns the current scoring configuration for a journey.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02, P-04, P-06

### Response 200 OK

```json
{
  "journeyId": "uuid",
  "modelType": "WeightedAverage",
  "stageWeightMode": "Equal",
  "normalizationParams": { },
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

Returns 404 with `journey.no_scoring_config` if no scoring configuration has been saved yet.

---

## PUT /api/v1/journeys/{journeyId}/detection

Saves pain/happy detection configuration for a journey, including optional stage and touchpoint overrides.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "painThreshold": 40,
  "happyThreshold": 75,
  "stageOverrides": [
    {
      "stageId": "uuid",
      "painThreshold": 35,
      "happyThreshold": 70
    }
  ],
  "touchpointOverrides": [
    {
      "touchpointId": "uuid",
      "painThreshold": null,
      "happyThreshold": 80
    }
  ]
}
```

**Validation**:
- `painThreshold` and `happyThreshold` are required at journey level; both in `[0, 100]`.
- `painThreshold` must be strictly less than `happyThreshold`.
- In overrides, `null` for either threshold means "inherit from parent level."
- All referenced `stageId` and `touchpointId` values must belong to the journey.

### Response 200 OK

```json
{
  "journeyId": "uuid",
  "painThreshold": 40,
  "happyThreshold": 75,
  "stageOverrideCount": 1,
  "touchpointOverrideCount": 1,
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `detection.threshold_invalid` | 422 | `painThreshold >= happyThreshold` |
| `detection.out_of_range` | 422 | Any threshold value < 0 or > 100 |
| `detection.unknown_stage` | 422 | `stageId` in override not found in this journey |
| `detection.unknown_touchpoint` | 422 | `touchpointId` in override not found in this journey |

**M-17 event**: `journey.detection_config.updated` published in the same transaction.
**Report contract rebuild**: `ReportContractService.RebuildContractAsync` called in the same transaction.

---

## GET /api/v1/journeys/{journeyId}/detection

Returns the current detection configuration for a journey, including all overrides.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02, P-04, P-06

### Response 200 OK

```json
{
  "journeyId": "uuid",
  "painThreshold": 40,
  "happyThreshold": 75,
  "stageOverrides": [
    { "stageId": "uuid", "painThreshold": 35, "happyThreshold": 70 }
  ],
  "touchpointOverrides": [
    { "touchpointId": "uuid", "painThreshold": null, "happyThreshold": 80 }
  ],
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

Returns 404 with `journey.no_detection_config` if none has been saved.

---

## GET /api/v1/kpi-types

Returns the list of KPI types available to the tenant: the six platform-standard types plus tenant-defined custom types.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Response 200 OK

```json
{
  "platformStandardTypes": [
    { "typeKey": "NPS", "labelAr": "صافي نقاط الترويج", "labelEn": "Net Promoter Score", "scoringDirection": "Ascending" },
    { "typeKey": "CSAT", "labelAr": "رضا العملاء", "labelEn": "Customer Satisfaction", "scoringDirection": "Ascending" },
    { "typeKey": "CES", "labelAr": "جهد العميل", "labelEn": "Customer Effort Score", "scoringDirection": "Descending" },
    { "typeKey": "FCR", "labelAr": "الحل من أول مرة", "labelEn": "First Contact Resolution", "scoringDirection": "Ascending" },
    { "typeKey": "AgentSatisfaction", "labelAr": "رضا الموظف", "labelEn": "Agent Satisfaction", "scoringDirection": "Ascending" },
    { "typeKey": "VFM", "labelAr": "القيمة مقابل المال", "labelEn": "Value for Money", "scoringDirection": "Ascending" }
  ],
  "tenantDefinedTypes": [
    {
      "kpiTypeDefinitionId": "uuid",
      "typeKey": "LOYALTY",
      "labelAr": "الولاء",
      "labelEn": "Loyalty",
      "scoringDirection": "Ascending"
    }
  ]
}
```

---

## POST /api/v1/kpi-types

Creates a new tenant-defined KPI type. P-01 only.

**Required permission**: `journey.admin`
**Required scope**: `organisation`
**Default personas**: P-01

### Request

```json
{
  "typeKey": "LOYALTY",
  "labelAr": "الولاء",
  "labelEn": "Loyalty",
  "scoringDirection": "Ascending"
}
```

**Validation**:
- `typeKey`: 1–64 characters, alphanumeric + underscore, unique within tenant, must not conflict with a platform-standard type key.
- `labelAr` and `labelEn`: required.
- `scoringDirection`: `Ascending` (default) or `Descending`.

### Response 201 Created

```json
{
  "kpiTypeDefinitionId": "uuid",
  "typeKey": "LOYALTY",
  "createdAt": "2026-06-08T10:00:00Z"
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `kpi_type.key_conflict` | 409 | `typeKey` already exists (tenant-defined or platform-standard) |
| `kpi_type.validation_error` | 422 | Invalid format |
