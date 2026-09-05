# Nabadat Platform — Integrations Constitution

**Project:** Nabadat — multi-tenant Voice-of-Customer / CX SaaS platform.
**Source:** HLD Chapter 6 "Integration Points" + Chapter 5.4, reconciled to `constitution.md` (router, v1.6.0) — cloud-provider IAM naming, NLP per Section 9.
**Status:** Governing principles for every integration between Nabadat and external systems. A new integration is a documented architectural change; deviations require an explicit, recorded amendment.

---

## Article 1 — A Bounded Integration Surface

1. **Seven integrations, named and documented:** Customer Active Directory, Customer SMTP Relay, Customer SIEM, Customer SMS Gateway, cloud-provider KMS, cloud-provider IAM, and the NLP service. Five are Customer-managed (AD, SMTP, SIEM, SMS gateway, NLP); two are hosting-platform (KMS — with its controlling key owned by Customer — and IAM).
2. **Six external, one internal.** Six are external integrations from Zone 2; the **NLP service is internal** to the deployment network (Zone 2), called through a fixed contract (see Article 4.5).
3. **The surface is closed.** A new integration is a documented architectural change, never an ad-hoc addition.

---

## Article 2 — Direction of Trust

1. **Outbound by default.** Integrations are predominantly outbound from Zone 2.
2. **One inbound system endpoint.** The Customer AD authentication callback is the only inbound system-to-system path, terminating at the auth endpoint with no data access. SMTP, SIEM, SMS gateway, KMS, and IAM have **no** inbound path; inbound traffic from them is blocked at the perimeter.

---

## Article 3 — Common Rules for Every Outbound Integration

Every outbound call MUST:

1. **Be allow-listed** — on the perimeter outbound allow-list; any other destination is blocked.
2. **Be TLS-protected** — TLS 1.2+.
3. **Be authenticated** — a credential appropriate to the counterpart.
4. **Store credentials encrypted at rest** — in configuration with application-layer envelope encryption under the customer CMK; never clear text.
5. **Be audited** — action, counterpart, outcome, latency.
6. **Be resilient** — retry with backoff on transient failure; defined dead-letter / failed-log / operator-alert behavior on persistent failure.
7. **Hold the counterpart endpoint in configuration** — migrating to a new counterpart is a config + credential-rotation change, not a code change.

---

## Article 4 — The Integration Counterparts

1. **Customer Active Directory** — the controlling identity store; SAML/OIDC trust with signed assertions; AD returns the assertion via the single inbound callback. MFA is enforced at AD.
2. **Customer SMTP Relay** — outbound channel for all email; authenticated submission on port 587. The relay enforces its own SPF/DKIM/DMARC/content scanning.
3. **Customer SIEM** — one-way destination for the continuous audit/security-event stream (syslog/HTTPS). No inbound flow.
4. **Customer SMS Gateway** — optional outbound channel for SMS distribution and, where configured, operational SMS; survey vs. operational use separate sender identities.
5. **Cloud-provider KMS** — holds the customer-owned CMK; every encrypt/decrypt reaches KMS. The CMK never leaves KMS; the IAM identity Nabadat uses holds no key-management grants — Customer owns and exercises those.
6. **Cloud-provider IAM** — refreshes the operational credential used for KMS and other cloud services; carries no Customer business data.
7. **NLP service** — called through the fixed contract `POST /analyze` (request `{text, language}`; response `{sentiment, confidence, themes, keywords, detected_language, detected_dialect}`). The implementation is environment-specific (on-prem CAMeLBERT; SaaS provider NLP) and selected by the `NLP_ENDPOINT` flag. Specs call the contract, never a specific NLP provider directly.

---

## Article 5 — Contained Failure

Each integration has a defined failure mode so no single failure cascades:

- **AD outage** → existing JWTs honored until expiry; automated reconnection retried.
- **SMTP outage** → retries accumulate, dead-letter on exhaustion; platform continues without email.
- **SIEM outage** → audit records accumulate locally for replay; all operations continue.
- **SMS outage** → retries accumulate; non-SMS channels continue.
- **KMS unreachable** → encrypt/decrypt-dependent operations fail with defined error codes; reconnection retried.
- **CMK disabled / scheduled-for-deletion** → detected via health check; Severity 1; Customer security notified within 15 minutes.
- **IAM issue** → automated credential refresh retried; cached credentials used until expiry.

The platform handles what it can (retry, accumulate, dead-letter, degraded-mode); Customer-side action restores each counterpart.

---

## Article 6 — Governance and Amendment

Concrete defaults (notification windows, retry schedules, protocols) are tunable per deployment. Any change that violates an article — an unlisted outbound destination, an inbound system path other than the AD callback, clear-text integration credentials, granting the IAM identity key-management rights, or letting an integration failure cascade — requires an explicit, recorded amendment.
