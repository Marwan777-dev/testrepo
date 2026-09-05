# Nabadat Platform — Security Constitution

**Project:** Nabadat — multi-tenant Voice-of-Customer / CX SaaS platform.
**Source:** HLD Chapter 4 "Security Considerations", reconciled to `constitution.md` (router, v1.6.0).
**Status:** Governing security principles. Every specification, control, and implementation MUST uphold these articles. A control that contradicts an article is revised; a principle found impractical is revisited in a documented amendment — never silently bypassed.

---

## Article 1 — The Four Founding Principles

1. **Defense in depth.** Every security guarantee is supported by more than one control (network, application, identity, encryption, audit). Compromise of a single control does not compromise the deployment.
2. **Least privilege.** Every account, service identity, and operational role holds exactly the permissions its work requires. No standing administrative access; grants are scoped to data and time.
3. **Default deny.** A user, service, or request without an explicit grant has no access. Permission decisions fail closed.
4. **Audit by default.** Every consequential action produces an audit record as part of the action. **Failure to record is failure to act.**

---

## Article 2 — Identity, Authentication, and Authorization

1. **Federated identity; local passwords gated by `ENABLE_LOCAL_AUTH`.** Customer AD (SAML 2.0 / OIDC) is the default and preferred source of platform-user identity; Nabadat holds no local password store unless `ENABLE_LOCAL_AUTH` is explicitly `true` (see `constitution.md` §10). When enabled: passwords are hashed with bcrypt (cost ≥ 12) or Argon2id; the platform enforces a configurable MFA policy; the flag MUST be reverted once Customer AD is operational; co-existence of both modes requires a recorded amendment.
2. **MFA at the IdP.** When federated, MFA is enforced at Customer AD; no Nabadat configuration can bypass it.
3. **Bounded JWT.** RS256 signed via the customer-owned CMK in the cloud-provider KMS; 15-minute TTL; silent refresh that re-validates AD group membership; 8-hour max session; 30-minute idle timeout; held in browser memory only; HTTPS-only. AD-group removal takes effect within 15 minutes.
4. **RBAC = roles × permission modules × data scope.** Effective authorization is the conjunction of all three; the action's mode (read/write) must be permitted and the target entity must fall within the user's hierarchy subtree. Persona definitions are maintained canonically in `constitution.md` Section 8.
5. **Default deny is enforced at every module boundary.** M-10 is the enforcement point; no module assumes an upstream check. Out-of-scope access returns 403 with an audit event; existence is never leaked (out-of-scope = 404). (Verified by **GP-04** — tenant/scope isolation.)
6. **Scoped service identities.** Service-to-service auth uses mTLS where required and scoped shared secrets otherwise; each identity holds only the grants its calls need.
7. **Respondents are token-authorized, not accounts.** A per-dispatch one-time token (unique, single-use, expiring) authorizes exactly one response.

---

## Article 3 — Cryptography and Key Management

1. **Standard primitives only.** Well-reviewed algorithms aligned to the tenant's applicable compliance baseline (e.g. NCA ECC where the jurisdiction requires it); deprecated algorithms (DES, 3DES, RC4, MD5, SHA-1) disabled. No custom cryptography.
2. **Encryption in transit is universal.** Every data-carrying connection is TLS-encrypted (1.2+); legacy protocols upgraded to TLS before integration is enabled.
3. **Two layers of encryption at rest.** Universal AES-256 storage-level encryption plus selective application-layer envelope encryption under the customer CMK for high-sensitivity fields. Selectivity is deliberate — universal field encryption would break indexing/search/aggregation.
4. **Key hierarchy with a single controlling key.** The CMK wraps every data key; a revoked CMK renders all dependent ciphertext unreadable. The CMK never leaves the cloud-provider KMS; plaintext key material never exists at rest in Nabadat; plaintext data keys exist only in Zone 2 process memory for the cache TTL. (Verified by **GP-02**.)
5. **CMK lifecycle is controlled and visible.** Rotation, disable, re-enable, and schedule-deletion are audited and forwarded to Customer SIEM.
6. **Application identity cannot manage keys.** The cloud-provider **IAM** operational identity holds only Encrypt/Decrypt on the customer CMK — never `DisableKey`, `ScheduleKeyDeletion`, `CreateKey`, or grants on any other CMK; lifecycle grants are held by a separate, dual-confirmation-gated identity set.

---

## Article 4 — Application Security

1. **Validate at the boundary.** Every input crossing the application boundary is validated in `nabadat-api` before reaching module logic; invalid input returns 400 with no attacker-useful detail.
2. **Injection defense by construction.** Structured APIs at every output boundary, no string concatenation, escape on render — uniformly.
3. **Session and cross-site discipline.** SPA holds the JWT in memory; XSS, output-encoding, and CSP discipline apply to admin portal and survey renderer (the renderer is more permissive on framing only for tenant-operated host embedding).
4. **Security is enforced server-side.** Sensitive operations carry extra application-layer protection beyond the standard permission check; a client that bypasses them locally still fails the server check. Security logic is never relied upon at the edge or client.
5. **Continuous vulnerability management.** Dependencies and images are scanned at build, release, and continuously in production; base images refreshed on cadence; vulnerabilities remediated against defined SLAs (criticals via out-of-cycle releases).

---

## Article 5 — Audit, Monitoring, and Security Event Detection

1. **Everything consequential is audited.** Any action that changes state, accesses sensitive data, exercises a permission, or affects security posture produces an audit record.
2. **Metadata, not data.** Audit records capture who/when/what/outcome, never the data handled.
3. **Immutable, append-only.** The `audit_log` table (owned by **M-17**) is append-only; corrections are new rows referencing the prior; nothing is overwritten.
4. **Action and audit commit together.** The audit write is in the same transaction as the action; an action that cannot record its audit fails as a whole.
5. **Resilient SIEM forwarding.** A background forwarder ships records to Customer SIEM with retry/backoff and dead-lettering; a SIEM outage accumulates records locally for replay; SIEM latency never affects the user. Forwarding is one-way to SIEM.
6. **Retention is protected.** Audit retention defaults to 7 years; shortening it is itself a sensitive, audited operation, with the audit of the change retained for the longer of the old and new windows.

---

## Article 6 — Governance and Amendment

Concrete defaults (JWT TTLs, patching SLAs, retention windows) are tunable per deployment without amending the principles above. Any change that violates an article — standing admin access, a clear-text data link, granting the application identity key-management rights, removing dual-confirmation on CMK lifecycle, weakening default-deny, or logging audited data content — requires an explicit, recorded amendment.
