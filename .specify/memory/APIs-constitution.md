# Nabadat Platform — APIs Constitution

**Project:** Nabadat — multi-tenant Voice-of-Customer / CX SaaS platform.
**Source:** HLD Chapter 5 "Communication Protocols" + Chapter 4 auth, reconciled to `constitution.md` (router, v1.6.0) — cursor-only pagination, `correlation_id`+`tenant_id` envelope, M-17 event log.
**Status:** Governing principles for the API and integration layer. A change that contradicts an article requires an explicit amendment.

---

## Article 1 — API Architectural Style

1. **REST over HTTPS, JSON by default.** Resources addressed by URL; standard HTTP verb semantics; JSON bodies.
2. **HTTPS only.** Plain HTTP is never served; a non-HTTPS request never reaches the application.
3. **Two addressable surfaces, shared conventions.** Admin portal API and the public survey renderer API share conventions and handlers where appropriate, but are addressable separately (different inbound paths, different rate limits).
4. **Predictable resource naming.** Plural-noun collections; `/api/v{version}/{resource}` and `/{resource}/{id}`; one-to-many nested sub-resources.
5. **Standard verb semantics.** `GET`/`POST`/`PUT`/`PATCH`/`DELETE`; verb-in-URL only where an action does not map to a resource lifecycle (e.g. `/surveys/{id}/publish`).
6. **Consistency is a contract.** Knowing one endpoint predicts the shape of any other of the same resource class.

---

## Article 2 — Content, Encoding, and Versioning

1. **Declared content types only.** Requests `application/json` (or `multipart/form-data` for attachments/imports). Responses `application/json` (or PDF, the spreadsheet type, `application/octet-stream`, `text/event-stream`). Unsupported request type → **415**.
2. **UTF-8 everywhere.** The only accepted encoding; carries Arabic (all four dialects), English, and all permitted content.
3. **Major version in the path** (`/api/v1/`). Non-breaking changes ship within a major version; breaking changes require a new major version.
4. **Deprecation window.** Both versions served concurrently for the agreed window — typically **12 months** from GA.

---

## Article 3 — Authentication

1. **Authentication on every request,** by surface:
   - **Admin portal API** — JWT issued by M-10 after Customer AD federation, `Authorization: Bearer <token>`.
   - **Survey renderer API** — one-time token issued by M-02 at dispatch, `X-Survey-Token` (or URL parameter for the initial form GET).
   - **API key clients** — `X-API-Key` (where issued).
   - **Customer AD callback** — signed SAML/OIDC assertion as the body.
2. **Federated identity; local passwords gated by feature flag.** Customer AD (SAML 2.0 / OIDC) is default and preferred; no local password store by default. `ENABLE_LOCAL_AUTH` may enable local passwords per deployment (see `constitution.md` §10). When `true`:
   - bcrypt (cost ≥ 12) or Argon2id; never plaintext or weak hashes.
   - Password policy (min length ≥ 12, complexity) enforced at the API boundary on every set/reset.
   - Local accounts follow the same JWT lifecycle, RBAC, and audit rules as federated accounts.
   - MFA enforcement shifts to the platform; a configurable MFA policy MUST be defined before the flag is enabled in any customer-facing environment.
   - The flag is reverted to `false` once Customer AD is operational; co-existence of both modes requires a recorded amendment.
3. **MFA at the IdP** when federated; Nabadat implements no separate layer and no config can bypass Customer-side MFA.
4. **JWT lifecycle is bounded.** RS256 signed with the customer-owned CMK in the cloud-provider KMS; 15-minute TTL; silent refresh re-validating AD group membership; 8-hour max session; 30-minute idle timeout; browser memory only (never `localStorage`/`sessionStorage`); HTTPS-only.
5. **Survey respondents are not platform users.** A per-dispatch one-time token authorizes exactly one response to one survey by one contact; it is not an account.
6. **Scoped service-to-service identity.** mTLS where required, scoped shared secrets otherwise; each identity holds only the grants its calls require.

---

## Article 4 — Authorization

1. **Authorization after authentication, on every admin-portal call.** The survey renderer is anonymous beyond the one-time token, whose binding to survey + contact + dispatch is its scope.
2. **RBAC = roles × permission modules × data scope.** The action's mode must be permitted **and** the target entity must fall within scope. Personas are defined canonically in `constitution.md` Section 8.
3. **Default deny.** No permission, out-of-scope, or invalid JWT → no access.
4. **Every module re-checks at its own boundary.** No module assumes an upstream check; M-10 is the enforcement point.
5. **Scope is applied server-side before data leaves the API.** For analytical queries the user's permission and data scope are applied as filter clauses in `nabadat-api` before any query reaches Elasticsearch. The data store is never the authorization point.
6. **Indistinguishable absence.** Out-of-scope resources return the same **404** as non-existent ones; an in-scope authorization failure returns **403** with an audit event.

---

## Article 5 — Status Codes and Error Format

1. **Use HTTP status codes as intended; invent none.** Defined set: `200`, `201` (+`Location`), `202`, `204`, `400`, `401`, `403`, `404`, `409`, `410`, `415`, `422`, `429` (+`Retry-After`), `500` (+correlation id), `502/503/504`.
2. **Uniform structured error envelope.** Every non-2xx response (except 204) returns:
   ```json
   { "error": { "code": "string", "message": "string", "correlation_id": "UUID", "tenant_id": "UUID" } }
   ```
   `code` is a stable dot-namespaced string; `message` is localized (Arabic/English); `correlation_id` is the platform-wide trace identifier (Article and error-handling constitution); `tenant_id` is present for tenant-scoped requests. Optional structured `details` may be included.
3. **Errors never leak internals.** No stack traces, database text, internal field names, or any server-side identifier beyond `correlation_id`.

---

## Article 6 — Collection Behavior: Pagination, Filtering, Sorting

1. **All collections use cursor-based pagination.** `page_size` defaults to 50, capped at 200; `page_token` is a cursor; responses include `items`, `next_page_token`, `total_count`. **Offset-based pagination is not permitted.**
2. **Declared filters only.** Query-string parameters with defined patterns (equality, comma-OR multi-value, range, free-text `q` to Elasticsearch, `sort`/`order`). Invalid values → **400**. Consumers do not invent undeclared parameters.
3. **Stable, documented ordering.** Each endpoint documents its sortable fields and a stable default order.

---

## Article 7 — Safety: Idempotency, Concurrency, and Bounds

1. **Idempotency on sensitive writes.** An `Idempotency-Key` replays the original response within the retention window (24h default) without re-executing. Honored on response submission, bulk import initiation, manual case creation, and notification dispatch.
2. **Optimistic concurrency via ETag.** Mutable resources return an `ETag`; updates send `If-Match`; mismatch → **409**. Not applied to append-only operations.
3. **Everything is bounded.** Rate limits, page sizes, request body sizes, and idempotency windows are bounded. Body caps enforced at the boundary (e.g. 1 MB non-attachment; 50 MB public / 100 MB internal attachment-bearing).
4. **Rate limiting is enforced and signaled.** **429** with `Retry-After`, keyed by authenticated identity (JWT subject, one-time token, or source IP). Ignored `Retry-After` → sustained 429s and an operator alert.

---

## Article 8 — Internal Communication

1. **Cross-module coordination is interface-or-event, never direct.** Synchronous needs go through a module's **published interface**; cross-module side effects are recorded as domain events via **M-17 (Event Log)**, durable as a PostgreSQL write to `event_log`, delivered by `nabadat-worker`. No module references another's concrete types or tables, and there is no direct network call between modules. (See architecture Article 3.)
2. **Stateless application tier.** `nabadat-api` is stateless; the load balancer distributes across application servers (least-connections) with retry/backoff on connection failure or 5xx, and no session affinity.
3. **Internal links carry application-layer identity.** Source restriction governs which component may reach an internal port; the JWT or one-time token is the application-layer identity. The application-to-Elasticsearch path is HTTPS on 9200.

---

## Article 9 — Governance and Amendment

Concrete defaults (TTLs, page sizes, rate limits, deprecation window, size caps) are tunable per deployment. Any change that violates an article — serving plain HTTP, offset pagination, authorizing at the data store, leaking internals in an error body, or a direct cross-module call outside a published interface — requires an explicit, recorded amendment.
