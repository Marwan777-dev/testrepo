# Nabadat Platform — Scalability & Performance Constitution

**Project:** Nabadat — multi-tenant Voice-of-Customer / CX SaaS platform.
**Source:** HLD Chapter 7 "Scalability and Performance", reconciled to `constitution.md` (router, v1.6.0).
**Status:** Performance targets are the contract every design decision is measured against.

---

## Article 1 — Performance Objectives Are the Contract

| Operation | Target (95th percentile) | Measurement point |
| --- | --- | --- |
| Survey response submission (synchronous) | Under 3 seconds | Edge request received → HTTP 200 to beneficiary |
| Dashboard load (routine) | Under 3 seconds | Browser request → first meaningful render |
| Report generation (typical) | Under 10 seconds | Request → first byte streamed |
| Report generation (large) | Under 30 seconds | Request → first byte streamed |
| User login (full AD flow) | Under 5 seconds (excluding IdP latency) | Portal redirect → JWT issued |
| API permission check | Under 10 milliseconds (steady state) | Inside the API request thread |

---

## Article 2 — Known Bottlenecks Have Defined Mitigations

| Bottleneck | Mitigation |
| --- | --- |
| PostgreSQL write throughput on response submission | PgBouncer pooling, write-optimized indexes, partitioned `audit_log`/`event_log`, vertical scaling of the primary |
| Elasticsearch indexing latency | Parallel `nabadat-worker` instances; index rotation by time period past a volume threshold |
| KMS round-trip on data-key cache miss | 5-minute in-process data-key cache; >95% hit-rate target in steady state |
| Customer SMTP relay rate | Rate-limit outbound dispatch to the agreed rate; queue during peak |
| Large report generation | Bound concurrency; stream output; long-running reports fail with a defined error code rather than holding the connection past timeout |
| `nabadat-worker` event-log throughput | Parallel workers; add workers when the backlog accumulates |
| Analytical query latency | Analytical queries run against Elasticsearch; PostgreSQL serves operational reads only |

---

## Article 3 — Caching Boundary

The platform has **no general caching layer** (no Redis; no in-memory caching of analytics data — router AD-03). The only permitted in-process cache is the **5-minute KMS data-key cache** in Article 2: it caches unwrapped *data keys*, never analytics, response, or tenant business data, and exists solely to avoid a KMS round-trip per cryptographic operation. No spec may introduce any other cache.
