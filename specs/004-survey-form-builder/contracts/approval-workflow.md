# Contract — Approval Workflow (F15, US2)

**Related**: [surveys.md](./surveys.md) · [spec.md § F15](../spec.md#f15--survey-approval--publishing-workflow)

**Base path**: `/api/v1/surveys/{id}`

These endpoints implement the P-03 → Draft → PendingReview → Publish loop with the `PublishOwnSurveys` grant escape hatch (BR-15.2).

---

## POST /api/v1/surveys/{id}/submit

**Purpose**: US2 — P-03 submits a Draft for review. Transitions `Draft → PendingReview`.

- **P**: `survey.submit` · **S**: `own` (Q8 team-owned) · **Personas**: P-03, P-01.
- **Headers**: `If-Match: W/"<survey.row_version>"`.
- **Response 200**: updated Survey view + new ETag.
- **Side effects**:
  - Emits `IEventLogWriter.WriteAsync(EventType.SurveySubmittedForReview, …)` (audit) with actor, timestamp, correlation_id.
  - Broadcasts M-09 notification (Q7) — one notification per user holding `survey.publish` in the tenant. M-01 calls `INotificationDispatcher.BroadcastAsync(scope=tenant, permission="survey.publish", deep_link=/surveys/{id}, template="survey.submitted_for_review")`. **Note**: `INotificationDispatcher` is M-09's published interface; M-01 depends on it via `Domain/Interfaces/`.
- **Response 409** `survey.status.invalid_transition` — the survey is not in `Draft`.
- **Response 409** `survey.publish.requires_content` — BR-1.7 also applies to the submit path (defensive: catches submitting empty surveys that would fail later at Publish).

## POST /api/v1/surveys/{id}/publish

**Purpose**: Publish. Transitions `Draft → Active` (P-01 direct), `PendingReview → Active` (reviewer OR P-03 with grant).

- **P**: `survey.publish` · **S**: `organisation` · **Personas**: P-01 always; P-03 only with the `PublishOwnSurveys` grant AND on a survey they personally authored (`owner_user_id == caller`).
- **Headers**:
  - `If-Match: W/"<survey.row_version>"`.
  - `Idempotency-Key: <uuid>` — required (governance action).
- **Request body**: `{ "remarks": "optional note recorded in the audit log" }`.
- **Response 200**: updated Survey view + new ETag.
- **Side effects**:
  - Publish content-gate (BR-1.7) enforced: `PublishGateService.EnsureContent(...)`.
  - Emits `survey.published` via M-17 (constitution § 4).
- **Response 403** `survey.publish.forbidden` — P-03 without grant, or with grant but not the personal author.
- **Response 409** `survey.status.invalid_transition` — survey not in Draft or PendingReview.
- **Response 409** `survey.publish.requires_content` — BR-1.7 (details.missing_sections / .missing_questions).

## POST /api/v1/surveys/{id}/return-to-draft

**Purpose**: Reviewer sends the survey back to the author. Transitions `PendingReview → Draft` (P-01), or **destructive** `Active → Draft` / `Paused → Draft` (see BR-1.6 in [surveys.md](./surveys.md) — but the destructive form uses `POST /status`, not this endpoint).

- **P**: `survey.review` · **S**: `organisation` · **Personas**: P-01.
- **Headers**: `If-Match: W/"<survey.row_version>"`.
- **Request body**: `{ "remarks": "Fix Arabic name" }` (required — FR-15.3 records remarks in the audit log).
- **Response 200**: updated Survey view + new ETag.
- **Side effects**: audit-log entry via M-17 with actor, timestamp, remarks.
- **Response 409** `survey.status.invalid_transition` — survey not in PendingReview.
- **Note**: this endpoint is **only** for the non-destructive PendingReview → Draft flow. The destructive Active/Paused → Draft path uses `POST /status {to:"Draft", confirm:true}` (BR-1.6) — it is a different action.

---

## Edit-lock behaviour on PendingReview

**BR-15.1**: while a survey is in PendingReview, the submitter (P-03) cannot edit. This is enforced by an `EditLockFilter` on every write endpoint (PUT / PATCH / DELETE / status endpoints except the reviewer flows above):

- If `caller.role == "P-03"` AND `survey.status == "PendingReview"` AND `caller.user_id == survey.submitted_by` → **403** `survey.edit_locked_by_pending_review`.
- The **reviewer** (P-01) MAY edit while PendingReview (BR-15.1) — the filter permits P-01 writes. If P-01 edits, the response includes a warning header `X-Warning: survey.edit_during_review` so the UI shows a subtle indicator.
