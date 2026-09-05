# Contract — Report & Analytics (F13, F14)

**Related**: [surveys.md](./surveys.md) · [research.md § 3](../research.md#3-elasticsearch-client--query-patterns-for-report--analytics) · [spec.md § F13-F14](../spec.md#f13--survey-report)

Both endpoints are **read-only** and route directly to Elasticsearch (`tenant_{tenantId}_analytics` and `tenant_{tenantId}_responses`). No PostgreSQL query serves these — AD-04.

**Base paths**:
- `/api/v1/surveys/{id}/report`
- `/api/v1/surveys/{id}/analytics`
- `/api/v1/surveys/{id}/preview` (F12 — client-side render, but the payload endpoint is here for shared parity)

---

## GET /api/v1/surveys/{id}/report

**Purpose**: F13 Survey Report — metric cards, KPI gauges, per-question views.

- **P**: `survey.report.read` · **S**: `organisation` for P-01/P-02; **`region` / `branch`** filtering applied server-side per the caller's data scope (APIs-constitution Article 4.5). · **Personas**: P-01, P-02, P-03, P-06 (P-06 sees summary — the same payload, UI shows less).
- **Query params**:
  - `period` — `last_1_day` \| `last_7_days` \| `last_month` \| `last_3_months` \| `last_6_months` \| `last_9_months` \| `last_year` \| `custom`.
  - `from` / `to` — required when `period = custom` (ISO-8601 timestamps).
- **Response 200**:
  ```json
  {
    "period": { "resolved_from": "2026-07-07T00:00:00Z", "resolved_to": "2026-07-14T00:00:00Z" },
    "metric_cards": {
      "responses": 4820,
      "completion_rate": 0.67,
      "median_time_seconds": 92,
      "touchpoints": 5
    },
    "headline_kpis": {
      "csat": { "value": 78.5, "target": 80.0, "delta_pp": -1.5 },
      "nps":  { "value": 42,   "target": 40,   "delta_pp": +2.0 },
      "ces":  { "value": 3.1,  "target": 3.0,  "delta_pp": -0.1 }
    },
    "per_question": [
      { "question_id": "…", "type": "KPI",
        "view": { "kind": "bar_distribution_plus_gauge",
                  "distribution": [{"label":"…","count":123}], "gauge": {"value":78.5,"target":80} },
        "responses_count": 812 },
      { "question_id": "…", "type": "MultiSelect",
        "view": { "kind": "bar_with_counts_and_pct",
                  "respondents_base": 4820,
                  "options": [{"label":"…","count":1500,"pct_of_respondents":31.1}] },
        "responses_count": 4820 },
      { "question_id": "…", "type": "InputField", "subtype": "Text",
        "view": { "kind": "verbatim_sample",
                  "sample": [{"response_id":"…","channel":"email","submitted_at":"…","text":"…"}],
                  "total_available": 245, "sample_size_default": 5, "sample_size_max": 100 },
        "responses_count": 245 }
    ]
  }
  ```
- **Response 400** `report.period.invalid` when `period = custom` and `from`/`to` are missing or invalid.
- **Response window semantics**: `ResponseWindowFilter` excludes responses submitted after the survey's active-period elapsed (FR-13.6). Post-expiry responses (BR-3.1 ON) live in the M-07 store, not here.
- **No ETag** — read-only.

## GET /api/v1/surveys/{id}/report/verbatims

**Purpose**: F13 "show more" verbatim expansion (up to 100 latest responses per FR-13.7).

- **P**: `survey.report.read` · **S**: `organisation` · **Personas**: P-01, P-02, P-03, P-06
- **Query params**: `question_id` (required), `limit` (default 20, max 100).
- **Response 200**: newest-first `sample` array of the same shape as above.

## GET /api/v1/surveys/{id}/analytics

**Purpose**: F14 Analytics — funnel, per-channel breakdown, trend line.

- **P**: `survey.analytics.read` · **S**: `organisation` · **Personas**: P-01, P-02, P-03, P-06
- **Query params**:
  - `period` — same enum as `/report`.
  - `from` / `to` — required when `period = custom`.
  - `granularity` — `daily` \| `weekly` \| `monthly` (default resolved by `TrendGranularityResolver`).
- **Response 200**:
  ```json
  {
    "period": { "resolved_from": "…", "resolved_to": "…", "granularity": "daily" },
    "funnel": {
      "sent":     { "count": 200, "delta_pct": +100.0 },
      "opened":   { "count": 160, "pct_of_sent": 80.0, "delta_pp": +5.0, "conversion_from_prev_stage_pct": 80.0 },
      "started":  { "count": 130, "pct_of_sent": 65.0, "delta_pp": +8.0, "conversion_from_prev_stage_pct": 81.25 },
      "finished": { "count": 120, "pct_of_sent": 60.0, "delta_pp": +10.0, "conversion_from_prev_stage_pct": 92.31 }
    },
    "overall_completion_rate": { "value_pct": 60.0, "delta_pp": +10.0 },
    "channels": [
      { "channel": "email",    "sent": 100, "completion_rate": 0.65, "delta_pp": +5.0 },
      { "channel": "whatsapp", "sent":  60, "completion_rate": 0.72, "delta_pp": +3.0 }
    ],
    "trend": [
      { "bucket_start": "2026-07-07", "sent": 30, "finished": 18, "completion_rate": 0.60 },
      { "bucket_start": "2026-07-08", "sent": 40, "finished": 25, "completion_rate": 0.625 }
    ]
  }
  ```
  Deltas are `null` when no previous-period data exists (FR-14.5) — no `+0%` misleading placeholder.
- **Response 400** `analytics.period.invalid` / `analytics.granularity.invalid`.

## GET /api/v1/surveys/{id}/preview

**Purpose**: F12 Multi-channel preview payload — a light-weight version of `GET /surveys/{id}` that also inlines the resolved theme and every rendered locale bundle.

- **P**: `survey.read` · **S**: `organisation` · **Personas**: P-01, P-02, P-03, P-06
- **Query params**: `channel` — `desktop` (default) \| `mobile` \| `whatsapp` \| `email`. `locale` — `en` (default) \| `ar` \| …
- **Response 200**: the survey view with resolved theme tokens (inherited from `ITenantDesignGuidelinesReader` where not customized), plus the resolved locale bundle inlined for quick rendering.
- **Response 400** `preview.channel.invalid`.
- **Client behaviour**: the SPA re-renders channel chrome around the same payload (FR-12.1); pagination follows `survey.layout` (FR-12.3); sections render their titles above their questions (FR-12.4).
