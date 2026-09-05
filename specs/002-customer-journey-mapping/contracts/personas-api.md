# API Contract: Personas (M-16)

**Module**: M-16 Customer Journey Mapping
**Base path**: `/api/v1/personas`
**Date**: 2026-06-08

Personas are reusable customer archetypes. Only `Active` personas may be bound to journeys. Persona creation and lifecycle management (status transitions) are restricted to P-01; P-02 may read personas.

---

## GET /api/v1/personas

Returns a paginated list of personas for the tenant.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Query parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `status` | string | (all) | Filter: `Draft`, `Active`, `Inactive`, `Archived` |
| `page_size` | integer | 50 | Max 200 |
| `page_token` | string | — | Cursor |

### Response 200 OK

```json
{
  "items": [
    {
      "personaId": "uuid",
      "nameAr": "العميل الرقمي",
      "nameEn": "Digital Customer",
      "status": "Active",
      "journeyBindingCount": 3,
      "updatedAt": "2026-06-08T10:00:00Z"
    }
  ],
  "nextPageToken": "string|null",
  "totalCount": 5
}
```

---

## POST /api/v1/personas

Creates a new persona with status `Draft`. P-01 only.

**Required permission**: `journey.personas.write`
**Required scope**: `organisation`
**Default personas**: P-01

### Request

```json
{
  "nameAr": "العميل الرقمي",
  "nameEn": "Digital Customer",
  "descriptionAr": "عملاء يفضلون القنوات الرقمية",
  "descriptionEn": "Customers who prefer digital channels"
}
```

**Validation**:
- `nameAr` and `nameEn`: both required, 1–255 characters.

### Response 201 Created

`Location: /api/v1/personas/{personaId}`

```json
{
  "personaId": "uuid",
  "status": "Draft",
  "createdAt": "2026-06-08T10:00:00Z"
}
```

**M-17 event**: `persona.created` published in the same transaction.

---

## GET /api/v1/personas/{personaId}

Returns full persona details.

**Required permission**: `journey.read`
**Required scope**: `organisation`
**Default personas**: P-01, P-02

### Response 200 OK

```json
{
  "personaId": "uuid",
  "nameAr": "العميل الرقمي",
  "nameEn": "Digital Customer",
  "descriptionAr": "string",
  "descriptionEn": "string",
  "status": "Active",
  "journeyBindings": [
    { "journeyId": "uuid", "journeyName": "Customer Onboarding Journey" }
  ],
  "createdAt": "2026-06-08T10:00:00Z",
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

---

## PUT /api/v1/personas/{personaId}

Updates persona metadata. Allowed for all non-`Archived` personas. P-01 only.

**Required permission**: `journey.personas.write`
**Required scope**: `organisation`
**Default personas**: P-01

### Request

```json
{
  "nameAr": "string",
  "nameEn": "string",
  "descriptionAr": "string",
  "descriptionEn": "string"
}
```

### Response 200 OK

```json
{
  "personaId": "uuid",
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `persona.archived_immutable` | 403 | Persona is Archived; metadata updates not allowed |

**M-17 event**: `persona.updated` published in the same transaction.

---

## PATCH /api/v1/personas/{personaId}/status

Transitions the persona lifecycle status. P-01 only.

**Required permission**: `journey.personas.publish`
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

**Archiving guard**: A persona may only be archived if it has no active journey bindings. If bound to journeys, the caller must unbind first (or the system auto-proposes unbinding in the UI).

### Response 200 OK

```json
{
  "personaId": "uuid",
  "status": "Active",
  "updatedAt": "2026-06-08T10:00:00Z"
}
```

### Errors

| Code | HTTP | Condition |
|------|------|-----------|
| `persona.invalid_transition` | 422 | Transition not valid for current status |
| `persona.archived_terminal` | 422 | Attempt to transition out of `Archived` |
| `persona.archive_blocked_active_bindings` | 409 | Persona is bound to one or more journeys; unbind first |

**M-17 event**: `persona.status.changed` published in the same transaction.

**Side effect on `Inactive` transition**: Any journey that was bound to this persona retains the binding record. The persona simply stops appearing in the binding selector for new journeys. Existing bindings are not automatically removed.

**Side effect on `Archived` transition**: The persona is removed from all journey binding selectors. Existing journey-persona binding records are retained in `journey_persona_bindings` for historical reporting but the persona is no longer bindable.

---

## DELETE /api/v1/personas/{personaId}

Hard deletion is not supported. Archiving (`PATCH .../status`) is the terminal action.

Returns **405 Method Not Allowed** with `persona.use_archive_instead`.

---

## Notes on Persona Visibility in Binding Selector

The journey builder persona selector calls `GET /api/v1/personas?status=Active` to populate the list of bindable personas. Personas in `Draft`, `Inactive`, or `Archived` status do NOT appear in the binding selector. This prevents binding non-`Active` personas to journeys (FR-005), consistent with the service-layer guard in `PersonaBindingService`.
