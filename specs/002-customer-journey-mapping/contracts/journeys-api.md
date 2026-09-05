# API Contract: Journeys (M-16)

**Module**: M-16 Customer Journey Mapping
**Base path**: `/api/v1/journeys`
**Date**: 2026-06-08

All endpoints return the API-05 error envelope on non-2xx responses. All endpoints require `Authorization: Bearer <session_token>`. Tenant is resolved from the JWT claim (API-02). All list endpoints use cursor-based pagination (API-04).

---

## GET /api/v1/journeys

Returns a paginated list of journeys for the authenticated tenant.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02, P-04, P-06

### Query parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `status` | string | (all) | Filter by status: `Draft`, `Active`, `Inactive`, `Archived` |
| `page_size` | integer | 50 | Max 200 |
| `page_token` | string | — | Cursor from previous response |

### Response 200 OK

```json
{
  "items": [
    {
      "journeyId": "uuid",
      "name": "string",
      "description": "string",
      "journeyType": "string",
      "status": "Active",
      "stageCount": 5,
      "touchpointCount": 18,
      "updatedAt": "2026-06-08T10:00:00Z",
      "updatedBy": "uuid"
    }
  ],
  "nextPageToken": "string|null",
  "totalCount": 12
}
```

---

## POST /api/v1/journeys

Creates a new journey with status `Draft`.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "name": "Customer Onboarding Journey",
  "description": "End-to-end onboarding experience",
  "journeyType": "Onboarding",
  "personaIds": ["uuid"]
}
```

**Validation**:
- `name`: required, 1–255 characters, unique per tenant (case-insensitive, excluding Archived journeys)
- `journeyType`: required
- `personaIds`: optional; each referenced persona must be `Active`

### Response 201 Created

`Location: /api/v1/journeys/{journeyId}`

```json
{
  "journeyId": "uuid",
  "name": "Customer Onboarding Journey",
  "status": "Draft",
  "createdAt": "2026-06-08T10:00:00Z"
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `journey.name_conflict` | 409 | Name already taken by a non-Archived journey |
| `journey.invalid_persona` | 422 | Referenced persona is not Active |
| `journey.validation_error` | 422 | Name blank or too long |

**M-17 event**: `journey.created` published in the same transaction.

---

## GET /api/v1/journeys/{journeyId}

Returns a full journey with stages and touchpoints.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02, P-04, P-06

### Response 200 OK

```json
{
  "journeyId": "uuid",
  "name": "string",
  "description": "string",
  "journeyType": "string",
  "status": "Active",
  "personaBindings": [{ "personaId": "uuid", "nameAr": "string", "nameEn": "string" }],
  "stages": [
    {
      "stageId": "uuid",
      "sequenceNumber": 1,
      "name": "string",
      "description": "string",
      "customerGoal": "string",
      "expectedEmotion": "excited",
      "durationHint": "2–5 minutes",
      "touchpoints": [
        {
          "touchpointId": "uuid",
          "name": "string",
          "channels": ["IVR", "Web"],
          "importance": "High",
          "isMoT": true,
          "isMandatory": false,
          "isMeasured": true,
          "kpiBindings": [
            { "kpiType": "NPS", "weight": 60, "isPlatformStandard": true },
            { "kpiType": "CSAT", "weight": 40, "isPlatformStandard": true }
          ]
        }
      ]
    }
  ],
  "updatedAt": "2026-06-08T10:00:00Z",
  "updatedBy": "uuid"
}
```

---

## GET /api/v1/journeys/{journeyId}/updated-at

Returns the journey's last update timestamp. Used by the frontend concurrent edit polling hook.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02, P-04, P-06

### Response 200 OK

```json
{
  "updatedAt": "2026-06-08T10:00:00Z",
  "updatedByUserId": "uuid",
  "updatedByName": "string"
}
```

---

## PUT /api/v1/journeys/{journeyId}

Updates journey metadata. Allowed on `Draft`, `Active`, and `Inactive` journeys only.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "name": "string",
  "description": "string",
  "journeyType": "string",
  "personaIds": ["uuid"]
}
```

**Validation**: Same as POST. `Archived` journeys return 403 with `journey.archived_immutable`.

### Response 200 OK

```json
{
  "journeyId": "uuid",
  "name": "string",
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

**M-17 event**: `journey.updated` published in the same transaction.

---

## PATCH /api/v1/journeys/{journeyId}/status

Transitions the journey lifecycle status. P-01 only.

**Required permission**: `journey.publish`
**Required scope**: `organisation`
**Default personas**: P-01

### Request

```json
{
  "status": "Active"
}
```

**Valid transitions**:

| From | To | Allowed |
|------|----|---------|
| `Draft` | `Active` | ✅ |
| `Active` | `Inactive` | ✅ |
| `Inactive` | `Active` | ✅ |
| `Draft` \| `Active` \| `Inactive` | `Archived` | ✅ |
| `Archived` | any | ❌ — terminal |

**Guard**: Archiving a journey that is bound to active surveys returns 409 with `journey.archive_blocked_active_surveys`.

### Response 200 OK

```json
{
  "journeyId": "uuid",
  "status": "Active",
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `journey.invalid_transition` | 422 | Transition not valid for current status |
| `journey.archived_terminal` | 422 | Attempt to transition out of `Archived` |
| `journey.archive_blocked_active_surveys` | 409 | Journey has active survey bindings |

**M-17 event**: `journey.status.changed` published in the same transaction.

---

## POST /api/v1/journeys/{journeyId}/stages

Adds a stage to a journey. The stage is appended at the end of the current sequence.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "name": "string",
  "description": "string",
  "customerGoal": "string",
  "expectedEmotion": "excited",
  "durationHint": "2–5 minutes"
}
```

### Response 201 Created

```json
{
  "stageId": "uuid",
  "sequenceNumber": 3,
  "createdAt": "2026-06-08T10:00:00Z"
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `journey.stage_limit_reached` | 422 | Tenant stage limit exceeded |
| `journey.archived_immutable` | 403 | Journey is Archived |

**M-17 event**: `journey.stage.added` published in the same transaction.

---

## GET /api/v1/journeys/{journeyId}/stages

Returns all stages for a journey, ordered by `sequence_number`.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02, P-04, P-06

### Response 200 OK

```json
{
  "stages": [
    {
      "stageId": "uuid",
      "sequenceNumber": 1,
      "name": "string",
      "touchpointCount": 4
    }
  ]
}
```

---

## PUT /api/v1/journeys/{journeyId}/stages/{stageId}

Updates stage metadata.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "name": "string",
  "description": "string",
  "customerGoal": "string",
  "expectedEmotion": "string",
  "durationHint": "string"
}
```

### Response 200 OK

```json
{ "stageId": "uuid", "updatedAt": "2026-06-08T10:00:00Z" }
```

---

## DELETE /api/v1/journeys/{journeyId}/stages/{stageId}

Deletes a stage. Fails if the stage still contains touchpoints.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Response 204 No Content

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `journey.stage_has_touchpoints` | 409 | Stage has touchpoints; delete or reassign them first |
| `journey.archived_immutable` | 403 | Journey is Archived |

**M-17 event**: `journey.stage.removed` published in the same transaction.

---

## PUT /api/v1/journeys/{journeyId}/stages/reorder

Reorders all stages. The request provides the complete new sequence as an ordered array of stage IDs.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "stageIds": ["uuid-stage-3", "uuid-stage-1", "uuid-stage-2"]
}
```

**Validation**: Array must contain exactly all stage IDs for the journey (no omissions, no duplicates).

### Response 200 OK

```json
{ "journeyId": "uuid", "reorderedAt": "2026-06-08T10:00:00Z" }
```

---

## POST /api/v1/stages/{stageId}/touchpoints

Adds a touchpoint to a stage.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "name": "string",
  "description": "string",
  "channels": ["IVR", "Web"],
  "importance": "High",
  "isMoT": false,
  "isMandatory": false
}
```

### Response 201 Created

```json
{
  "touchpointId": "uuid",
  "createdAt": "2026-06-08T10:00:00Z"
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `journey.touchpoint_limit_reached` | 422 | Tenant touchpoint-per-stage limit exceeded |
| `journey.archived_immutable` | 403 | Parent journey is Archived |

**M-17 event**: `journey.touchpoint.added` published in the same transaction.

---

## PUT /api/v1/touchpoints/{touchpointId}

Updates touchpoint metadata.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Request

```json
{
  "name": "string",
  "description": "string",
  "channels": ["IVR"],
  "importance": "Critical",
  "isMoT": true,
  "isMandatory": true
}
```

### Response 200 OK

```json
{ "touchpointId": "uuid", "updatedAt": "2026-06-08T10:00:00Z" }
```

---

## DELETE /api/v1/touchpoints/{touchpointId}

Deletes a touchpoint and its KPI bindings.

**Required permission**: `journey.write`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Response 204 No Content

**M-17 event**: `journey.touchpoint.removed` published in the same transaction.

---

## POST /api/v1/journeys/{journeyId}/publish

Publishes a new immutable version snapshot of the journey. P-01 only.

**Required permission**: `journey.publish`
**Required scope**: `organisation`
**Default personas**: P-01

### Request

```json
{}
```

(No body required. The snapshot is built from the current live journey configuration.)

### Response 201 Created

```json
{
  "versionId": "uuid",
  "versionNumber": 3,
  "publishedAt": "2026-06-08T10:00:00Z"
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `journey.no_stages` | 422 | Journey has no stages; cannot publish empty journey |
| `journey.archived_immutable` | 403 | Journey is Archived |

**M-17 event**: `journey.version.published` published in the same transaction.

---

## GET /api/v1/journeys/{journeyId}/versions

Returns the list of published versions for a journey, newest first.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02, P-04, P-06

### Query parameters

| Parameter | Type | Default |
|-----------|------|---------|
| `page_size` | integer | 20 |
| `page_token` | string | — |

### Response 200 OK

```json
{
  "items": [
    {
      "versionId": "uuid",
      "versionNumber": 3,
      "publishedAt": "2026-06-08T10:00:00Z",
      "publishedByName": "string"
    }
  ],
  "nextPageToken": "string|null",
  "totalCount": 3
}
```

---

## GET /api/v1/journeys/{journeyId}/versions/{versionNumber}

Returns the full snapshot for a specific published version. Read-only.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02, P-04, P-06

### Response 200 OK

Returns the deserialized `snapshot_payload` as the response body (the full journey tree at publish time). Shape is identical to `GET /api/v1/journeys/{journeyId}` response but marked `"isSnapshot": true` and includes `"snapshotVersion": 3`.

---

## GET /api/v1/journeys/{journeyId}/reports

Returns the report contract metadata for M-07 consumption (also accessible via `IReportContractReader` in-process).

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02, P-04, P-06

### Response 200 OK

```json
{
  "journeyId": "uuid",
  "journeyName": "string",
  "generatedAt": "2026-06-08T10:00:00Z",
  "stages": [
    {
      "stageId": "uuid",
      "name": "string",
      "sequenceNumber": 1,
      "touchpoints": [
        {
          "touchpointId": "uuid",
          "name": "string",
          "isMoT": false,
          "kpiTypes": ["NPS", "CSAT"],
          "isMeasured": true
        }
      ]
    }
  ],
  "scoreDimensions": ["journey_score", "stage_score", "touchpoint_score", "kpi_score"],
  "detectionConfig": {
    "painThreshold": 40,
    "happyThreshold": 75
  }
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `journey.no_report_contract` | 404 | Journey has no stages or contract not yet generated |
