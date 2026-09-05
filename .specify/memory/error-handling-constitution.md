# Nabadat Platform — Error Handling & Logging Constitution

**Project:** Nabadat — multi-tenant Voice-of-Customer / CX SaaS platform.
**Source:** HLD Chapter 8 "Error Handling and Logging", reconciled to `constitution.md` (router, v1.6.0) — M-17 event/audit ownership, `correlation_id` standard, corrected numbering.
**Status:** Governing principles for how the platform produces, classifies, handles, observes, and investigates errors and logs. A change that contradicts an article requires an explicit, recorded amendment.

---

## 1. Three Distinct Records of Truth

Three separate streams, never conflated:

- **Operational logs** — operational ground truth, in the central logging stack.
- **Audit log** — compliance ground truth, in PostgreSQL (`audit_log`, owned by **M-17**), retained 7 years.
- **Event log** — cross-module coordination ground truth, in PostgreSQL (`event_log`, owned by **M-17**).

The central logging stack is operational only; it holds no business data, audit records, or domain events.

---

## 2. Log Categories and Content

| Category | What it captures |
| --- | --- |
| Application logs | Lifecycle/outcome of operations (INFO / WARN / ERROR / FATAL) |
| Access logs | Every inbound request (higher volume, shorter retention) |
| Audit logs | Compliance record of consequential actions (in PostgreSQL `audit_log`, M-17) |
| Security logs | Security-relevant events; forwarded to the central stack **and** Customer SIEM |
| Integration logs | Every outbound call and inbound callback |
| Infrastructure logs | Server and cloud-platform operational state |

**Content discipline (mandatory):** application logs never contain plaintext user content, response answers, attachment binaries, or PII beyond the contact UUID. Sensitive content is referenced by identifier and retrieved through the audited admin-portal surface — never read out of a log.

---

## 3. Error Handling Patterns

### 3.1 Consistent error categories
| Category | Handling |
| --- | --- |
| Input validation failure | **400** at the API boundary; no partial work |
| Authentication failure | **401** (admin) / token error (renderer); security event |
| Authorization failure | **403**; security event with attempted action + identity |
| Resource not found / out of scope | **404** (same code so existence is not leaked) |
| Conflict | **409** with a structured reason |
| Rate limit exceeded | **429** with `Retry-After` |
| External integration failure | Retry/dead-letter pattern (see 3.2) |
| Internal application error | **500** with `correlation_id`; full context at ERROR; alert if 500-rate exceeds threshold |
| Database error | Mapped per 3.3 |
| Infrastructure error | Container runtime restart; critical alert if recurring |

### 3.2 External integration failure handling
- **Counterpart unreachable** → exponential backoff (30s, 2m, 10m, 1h, 4h, 12h), dead-letter on exhaustion, operator alert; the triggering operation is decoupled from integration success where possible.
- **Transient error (5xx / rate limit)** → retry with the same backoff, respecting `Retry-After`.
- **Permanent error (4xx other than rate limit)** → no retry; record as failed; surface to producing module + operator.
- **Success but unreported downstream** → resolved by delivery-status callback where available; else recorded as "submitted to counterpart."
- **Authentication failure** → pause the integration, operator alert; recovery requires credential refresh, not automatic.

### 3.3 Database error handling
- **Connection timeout** → bounded in-thread retry; on exhaustion **503**.
- **Deadlock** → bounded transaction retry; persistent → warning + **409**.
- **Unique constraint violation** → domain error (usually **409**).
- **Foreign-key violation** → treated as a programming defect (cross-module references carry no FKs); ERROR + **500**.
- **Primary unavailable** → **503**, operator alert, documented failover.
- **Replica lag over threshold** → eventually-consistent reads may use the replica; fresh-data queries route to the primary; operator alert.

### 3.4 Idempotency and replay protection
- **Survey response submission** — one-time token consumed at first submission; reuse → **409**.
- **Sensitive operations** — `Idempotency-Key`; repeats within 24h return the first response without re-execution.
- **Event subscribers** — track processed `event_id` and skip duplicates (effectively exactly-once).
- **Outbound retries** — stable idempotency key per logical dispatch where the counterpart supports it.

### 3.5 Dead-letter handling
| Source | Dead-letter store |
| --- | --- |
| Failed event deliveries (M-17) | `event_log` dead-letter partition |
| Failed audit forwarding to Customer SIEM | `audit_log` dead-letter partition |
| Failed notifications | `notification_log` (failed state) |
| Failed survey dispatches | M-02 dispatch state, marked permanently failed |

Dead-letter records follow standard retention and are reviewable in the admin portal by authorized operators. Replay is operator-initiated and audited.

---

## 4. Correlation and Investigation

1. **The `correlation_id` is the spine.** A UUID `correlation_id` threads every log entry, audit record, event, and integration call from a single user action; it appears in the error envelope (`correlation_id` + `tenant_id`) and on every audit record. One value retrieves the entire trail.
2. **Investigation discipline.** Investigations run through audited surfaces and identifiers; sensitive content is retrieved through the audited admin-portal path, never pulled from operational logs.

---

## 5. Governance and Amendment

Concrete defaults (retention windows, backoff schedule, log-volume baseline) are tunable per deployment. Any change that violates an article — logging plaintext sensitive content, leaking internals in an error body, removing idempotency from a sensitive write, or letting an integration failure cascade — requires an explicit, recorded amendment.
