# NABADAT VOC PLATFORM
## Software Requirements Specification
### Customer Journey Mapping Module (M-16)

---

| Field | Value |
|---|---|
| **Version** | 0.5 (Draft) |
| **Status** | Draft — Nabadat Design Applied; P25 Configurable; Margin of Error Removed |
| **Author** | CX R&D |
| **Date** | 2026-06-04 |
| **Module** | M-16 — Customer Journey Mapping |
| **Implementation Order** | 3rd module — after M-11 and M-10 |
| **Document Scope** | Chunk 1 of 2 — Feature Spec + Data Model + Scoring Model |

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Overall Description](#2-overall-description)
3. [System Features](#3-system-features)
4. [Data Model](#4-data-model)
5. [External Interface Requirements](#5-external-interface-requirements)
6. [Non-Functional Requirements](#6-non-functional-requirements)
7. [Dependency & Side-Effect Analysis](#7-dependency--side-effect-analysis)
8. [Out of Scope](#8-out-of-scope)
9. [Open Questions / Decisions Pending](#9-open-questions--decisions-pending)
10. [Glossary](#10-glossary)
11. [Strategic Satisfaction Scoring Model](#11-strategic-satisfaction-scoring-model)
- [Change Log](#change-log)

---

# 1. Introduction

## 1.1 Purpose & Audience

This document specifies the Customer Journey Mapping Module (M-16) for the Nabadat Voice of Customer (VOC) platform. It is the single source of truth for the engineering, QA, design, and product teams building this module.

**Audience:** Backend engineers, frontend engineers, QA engineers, UX designers, product managers, CX consultants.

This document is written under the assumption that the reader has no prior CX domain knowledge. Every CX/VOC term is defined the first time it appears and consolidated in the Glossary (Section 10).

**Implementation context:** M-16 is the third module to be implemented on the Nabadat platform, built on top of M-11 (Tenant Administration) and M-10 (User & Role Management). The RBAC primitives, tenant isolation infrastructure, and audit logging established by M-11 and M-10 are consumed by this module as foundational dependencies.

---

## 1.2 Scope

**In Scope (this document):**

- Persona configuration (optional, reusable across journeys)
- Customer journey configuration (journey → stages → touchpoints)
- Journey-local touchpoints (all touchpoints created and owned within a single journey)
- KPI configuration per touchpoint with weights and normalisation definitions
- Strategic satisfaction scoring model: touchpoint, stage, and journey level (Section 3.3 & Section 11)
- Tenant-configurable scoring parameters (α, β, MOT multiplier, confidence thresholds)
- Journey versioning (hybrid major/minor model)
- Pain point and happy moment detection (signal definitions and configuration; computation delegated to M-06)
- Reporting output definitions (consumed by M-07 Dashboards & Reporting)
- Permissions & role-based access for journey configuration (extends M-10 RBAC)
- Localization (Arabic + English, RTL support)
- Templates, cloning, export
- Optional maker-checker approval workflow

**Out of Scope (this document):**

- Survey builder UI (handled in M-01 Survey and Form Builder)
- Response collection mechanisms (handled by M-02 Channel Management)
- Text analytics / verbatim theme detection algorithms (consumed as input from M-05)
- Shared/reusable touchpoint library — all touchpoints are journey-local in this version
- Survey-to-Journey and Question-to-Touchpoint binding (handled in M-01)
- Score computation execution (delegated to M-06; this module defines configuration only)
- Dashboard UI rendering (handled in M-07 Dashboards & Reporting)
- AI-generated journey suggestions (future roadmap)
- Bulk CSV import of journeys (future)
- Real-time collaborative editing of journeys (future)

---

## 1.3 Definitions & Acronyms

Selected definitions used throughout this document. Full glossary in Section 10.

| Term | Definition |
|---|---|
| **VOC** | Voice of Customer. A structured program to collect, analyse, and act on customer feedback. |
| **Journey** | An end-to-end logical sequence of stages a customer traverses to achieve a goal (e.g., "Apply for a Personal Loan"). |
| **Stage** | A logical phase within a journey (e.g., "Application", "Approval", "Disbursement"). |
| **Touchpoint (TP)** | A discrete interaction point between the customer and the brand within a stage. All touchpoints are journey-local in v1. |
| **Persona** | A representative archetype of a customer segment with shared behaviours and needs. |
| **KPI** | Key Performance Indicator. CX metrics (NPS, CSAT, CES, etc.) configured per touchpoint. M-16 owns configuration; M-06 owns computation. |
| **NPS** | Net Promoter Score. Customer loyalty metric, −100 to +100. |
| **CSAT** | Customer Satisfaction Score. Post-interaction rating, typically 1–5 or 1–10 scale. |
| **CES** | Customer Effort Score. Ease-of-interaction metric, typically 1–7 scale (lower = easier). |
| **MoT** | Moment of Truth. A touchpoint that disproportionately shapes overall customer perception. |
| **TP_score** | The composite satisfaction score of a touchpoint, derived from its KPI scores weighted by KPI weights. |
| **Stage_strategic** | The weighted composite satisfaction score for a stage, derived from its touchpoints using the Strategic Scoring Model (Section 11). |
| **Journey_strategic** | The weighted composite satisfaction score for a journey, derived from its stages. |
| **TP_effective_weight** | The computed weight of a touchpoint in stage scoring, incorporating TP_weight, customer/business importance, MoT multiplier, and confidence factor. |
| **confidence_factor** | A continuous dampening value (0–1.0) applied to a touchpoint's effective weight based on its response volume relative to the journey median. Prevents low-sample touchpoints from distorting scores. |
| **n_TP** | Internal: KPI-weight-averaged response count for a touchpoint. Used only in confidence_factor calculation. Never displayed to users. |
| **Normalised Score** | A KPI score converted to a common 0–100 scale to enable cross-KPI weighting. |
| **Pain Point** | A touchpoint with consistently low satisfaction or high friction. |
| **Happy Moment** | A touchpoint with consistently high satisfaction or delight. |
| **Tenant** | A client organisation using Nabadat. Each tenant's data is fully isolated. |
| **Maker-Checker** | An approval workflow where one user creates/edits and a different user approves before changes go live. |
| **M-06** | CX Metrics & KPI Engine — the Nabadat module responsible for computing scores using KPI configurations defined in M-16. |

---

## 1.4 References

- Nabadat Platform Definition (DOC-01)
- M-10 — User & Role Management SRS (foundational RBAC dependency)
- M-11 — Tenant Administration SRS (tenant isolation, audit log infrastructure)
- M-01 — Survey and Form Builder SRS (sibling — survey/question binding logic)
- M-05 — NLP & Text Analytics SRS (sibling — verbatim themes consumed here)
- M-06 — CX Metrics & KPI Engine SRS (sibling — computation engine and ScoreSnapshot owner)
- M-07 — Dashboards & Reporting SRS (sibling — renders journey report outputs)
- M-09 — Notifications & Alerts Engine SRS (sibling — fires pain/happy alerts)
- Nabadat Brand & Design System
- ISO 8601 — date and time format standard
- WCAG 2.1 Level AA — accessibility standard
- Kahneman, D. — *Thinking, Fast and Slow* (peak-end rule, basis for MoT methodology)

---

# 2. Overall Description

## 2.1 Product Perspective

The Customer Journey Mapping Module (M-16) is a core module within the Nabadat VOC platform, built as the third module in the implementation sequence. It operates on top of the tenant and user management infrastructure established by M-11 and M-10.

**Module interactions:**

- **M-10 User & Role Management (foundational):** Provides RBAC primitives — roles, permissions, scope assignments — that this module extends with journey-specific permissions.
- **M-11 Tenant Administration (foundational):** Provides tenant isolation, tenant-level configuration, and the audit log infrastructure consumed by this module.
- **M-01 Survey and Form Builder (sibling):** Surveys and questions bind to journeys and touchpoints from within M-01. This module exposes picker components (journey picker, touchpoint picker) consumed by M-01.
- **M-05 NLP & Text Analytics (sibling):** Provides verbatim themes per touchpoint used by Pain/Happy Detection Signal 4.
- **M-06 CX Metrics & KPI Engine (sibling):** Consumes KPI configuration, weights, normalisation rules, and the Strategic Scoring Model configuration (Section 11) defined in this module to compute touchpoint, stage, and journey scores.
- **M-07 Dashboards & Reporting (sibling):** Consumes journey-aggregated scores and detection results to render scorecards, journey canvas performance view, trend dashboards, and touchpoint performance tables.
- **M-09 Notifications & Alerts Engine (sibling):** Sends alerts when pain/happy thresholds are crossed and when journey versions await approval.

This module owns the following entities (full data model in Section 4): Persona, Journey, JourneyVersion, Stage, TouchpointInJourney, KPIBinding, JourneyPersonaMap, ScoringConfig, and the supporting configuration entity DetectionConfig.

---

## 2.2 Operating Environment

- Web platform: Latest two versions of Chrome, Edge, Safari, Firefox
- Responsive: Desktop primary; tablet supported; mobile read-only views for executive/frontline personas
- Languages: Arabic and English, with RTL support for Arabic
- Multi-tenant: Strict tenant data isolation enforced at database and API layers

---

## 2.3 Assumptions & Dependencies

- **A1:** M-10 (User & Role Management) is fully operational. RBAC primitives are a prerequisite for the permission model in Section 3.9.
- **A2:** M-11 (Tenant Administration) is fully operational. Tenant provisioning, isolation enforcement, and audit log infrastructure are consumed by this module.
- **A3:** M-01 (Survey and Form Builder) exposes Question entities and their response scales for KPI-eligibility checking.
- **A4:** M-05 (NLP & Text Analytics) exposes verbatim themes per touchpoint for Pain/Happy Detection Signal 4.
- **A5:** M-06 (CX Metrics & KPI Engine) consumes KPI type, weight, normalisation definitions, and ScoringConfig from this module. M-06 must be designed to read and apply the full Strategic Scoring Model (Section 11).
- **A6:** M-09 (Notifications & Alerts Engine) is operational and can receive alert triggers from this module.
- **A7:** Each tenant has at least one user with the CX Program Manager role (P-01) at the time of M-16 onboarding.

---

# 3. System Features

## 3.1 F1 — Persona Management

### 3.1.1 Description & Priority

Allows tenants to optionally define customer personas (archetypes) and reuse them across journeys. Personas are not required; users can create journeys without binding any persona.

**Priority:** Should-have. Module functions without it but loses analytical depth.

### 3.1.2 Functional Requirements

- **FR-1.1:** A user with permission must be able to create a new persona with: name (AR + EN), description (AR + EN), demographic attributes (configurable), behavioural attributes (configurable), and an avatar/icon (selectable from built-in set; no custom image upload in v1).
- **FR-1.2:** Maximum 50 personas per tenant. On attempting the 51st: *"You've reached the maximum of 50 personas. Archive an existing persona to create a new one."*
- **FR-1.3:** Persona statuses: Draft, Active, Archived. Only Active personas can be bound to journeys. Archived personas remain visible in historical reports but cannot be assigned to new journeys.
- **FR-1.4:** Persona edits affect all current and future journey reports immediately (personas are not version-locked to journey versions in v1).
- **FR-1.5:** A persona bound to at least one journey cannot be deleted. The user must unbind or archive it first.
- **FR-1.6:** The system must support listing, searching by name, and filtering personas by status.

### 3.1.3 Business Rules

- **BR-1.1:** Persona names must be unique per tenant per language (enforced on trimmed lowercase).
- **BR-1.2:** Demographic attributes are descriptive only — they do not gate which customers can take which journey.
- **BR-1.3:** Archived personas appear in historical reports with an `[Archived]` suffix label.

---

## 3.2 F2 — Journey, Stage & Touchpoint-in-Journey Configuration

### 3.2.1 Description & Priority

The core feature. Allows configuration of a customer journey, its sequence of stages, and the touchpoints within each stage. All touchpoints in M-16 v1 are journey-local. The UI is structured as: Journey List → Journey Builder (stage/touchpoint outline) → Touchpoint Configuration Panel (right drawer).

**Priority:** Must-have.

> **NOTE:** UI reference: Image 1 shows the Journey List screen (filter by Status and Type, journey cards showing stages/touchpoints count, version, last updated). Image 4 shows the Journey Builder screen with stage accordion rows and touchpoint items. Image 5 shows the Configure Touchpoint right-panel drawer.

### 3.2.2 Functional Requirements — Journey Level

- **FR-2.1:** Create a new journey with: name (AR + EN), description (AR + EN), journey type (Transactional, Lifecycle, Issue-Resolution, Onboarding), owner (user ID), expected total duration (optional, days), and bound personas (zero or more).
- **FR-2.2:** Journey statuses: Draft, Active, Archived. Only Active journeys are used for measurement and reporting.
- **FR-2.3:** A journey is bound to personas via a M:N relationship. For each bound persona, the user may optionally set persona-specific importance overlays at the touchpoint level.
- **FR-2.4:** If no persona-specific overlay is set, the touchpoint's configured importance values apply as defaults.

### 3.2.3 Functional Requirements — Stage Level

- **FR-2.5:** Each stage has: name (AR + EN), customer goal (AR + EN, free text), expected customer emotion (enum: Excited, Neutral, Anxious, Frustrated, Confident, Confused, Relieved), expected duration (optional), and sequence flag (Sequential or Parallel). Stage emotion and customer goal are displayed on the Journey Analytics canvas (Image 2).
- **FR-2.6:** A journey must have at least 1 stage to be published.
- **FR-2.7:** Stages can be reordered via drag-and-drop. Reordering after publish triggers a major version bump.
- **FR-2.8:** Maximum 20 stages per journey. On exceeding: *"You've reached the maximum of 20 stages. Consider splitting this into multiple journeys."*
- **FR-2.9:** Each stage carries a `stage_weight` (integer, default equal weight). The `stage_weight` is used in journey-level score aggregation (see Section 11).

### 3.2.4 Functional Requirements — Touchpoint-in-Journey Level

> **NOTE:** UI reference: Image 4 shows each touchpoint row with channel badge(s), star rating (representing `importance_customer`), MoT badge, and KPI count badge. Image 5 shows the Configure Touchpoint panel with: Name (Arabic shown), Description (EN + AR), Channels (multi-select chips), Importance to Customer (1–5 star selector), Importance to Business (1–5 star selector), Moment of Truth toggle, Mandatory toggle, and KPI Configuration section.

- **FR-2.10:** All touchpoints are journey-local. Users create touchpoints directly within a journey — no library picker.
- **FR-2.11:** Each touchpoint carries: sequence order, channel(s), `importance_to_customer` (1–5), `importance_to_business` (1–5), MoT flag, Mandatory flag, `TP_weight` (integer, user-configured), KPI binding (see F3), and name/description (AR + EN).
- **FR-2.12:** Maximum 30 touchpoints per stage, 300 per journey.
- **FR-2.13:** Mandatory flag: whether every customer is expected to traverse this touchpoint. Consumed by M-06 for response-volume normalisation.
- **FR-2.14:** MoT auto-suggest: when `importance_to_customer` ≥ 4, display non-blocking prompt: *"This touchpoint has high customer importance. Mark as Moment of Truth? [Yes] [No]"*. Does not appear if MoT is already explicitly set.
- **FR-2.15:** `TP_weight` must be a positive integer. The engine normalises all `TP_weight`s within a stage so they sum to 1.0 before scoring. Equal weights are the default.
- **FR-2.16:** `Stage_weight` must be a positive integer. The engine normalises all `Stage_weight`s within a journey. Equal weights are the default.

### 3.2.5 Business Rules

- **BR-2.1:** Journey names must be unique per tenant per language.
- **BR-2.2:** A journey cannot be archived while bound to active surveys. Display: *"This journey is currently bound to active surveys. Update survey bindings before archiving."*
- **BR-2.3:** Archived journey data remains queryable in historical reports; no new responses can be associated.
- **BR-2.4:** Channel enum (non-extensible in v1): Web, Mobile App, Email, SMS, WhatsApp, Phone (Inbound), Phone (Outbound), Branch/In-Person, Chat, IVR, Social Media, Kiosk, Other.
- **BR-2.5:** Persona-specific importance overlays take precedence over journey-level defaults.
- **BR-2.6:** Default KPI assignments by journey type — Transactional: [CSAT, CES]; Lifecycle: [CSAT, NPS]; Issue-Resolution: [CES, FCR]; Onboarding: [CSAT, CES, NPS]. User-overridable.

---

## 3.3 F3 — KPI Configuration per Touchpoint

### 3.3.1 Description & Priority

For each touchpoint, the user configures one or more KPIs with weights. This configuration, combined with the Strategic Scoring Model (Section 11), is consumed by M-06 to compute all satisfaction scores. M-16 owns configuration and normalisation rule definitions; M-06 owns computation.

**Priority:** Must-have.

### 3.3.2 Functional Requirements

- **FR-3.1:** For each touchpoint, add one or more KPIs: NPS, CSAT-5pt, CSAT-10pt, CES-5pt, CES-7pt, FCR (binary or %), Sentiment (0–100 from M-05). Each KPI is assigned a weight as a percentage integer (0–100).
- **FR-3.2:** The sum of all KPI weights for a single touchpoint must equal exactly 100%. Enforced at save time. Error: *"KPI weights must sum to 100%. Current total: [X]%."*
- **FR-3.3:** Maximum 5 KPIs per touchpoint. Error: *"Maximum 5 KPIs per touchpoint. Remove a KPI to add another."*
- **FR-3.4:** When NPS is added as a touchpoint KPI, display non-blocking warning: *"NPS is designed to measure overall loyalty, not single-touchpoint satisfaction. Consider CSAT or CES instead. [Use NPS anyway] [Choose another KPI]"*.
- **FR-3.5:** KPI weights are independent of normalisation. Normalisation formulas (Section 3.3.4) are applied by M-06.
- **FR-3.6:** A touchpoint with zero KPIs does not contribute to stage or journey score (treated as unmeasured by M-06). Flag displayed: `⚠ No KPIs configured`.
- **FR-3.7:** KPI changes on a touchpoint constitute a major version change for the parent journey.

### 3.3.3 Business Rules

- **BR-3.1:** KPI weights must be integers between 0 and 100 inclusive.
- **BR-3.2:** A KPI with weight 0% is treated as not configured — user is prompted to remove or assign a non-zero weight.
- **BR-3.3:** The NPS warning (FR-3.4) is informational only. It is logged to a tenant-level analytics event.
- **BR-3.4:** A KPI with no bound survey questions contributes 0 to M-06 score. Flag: `⚠ No bound questions`.

### 3.3.4 KPI Normalisation Reference

The following formulas are defined here and applied by M-06. They map each KPI's raw scale to a common 0–100 satisfaction index to enable cross-KPI weighting.

| KPI | Raw Scale | Normalisation Formula (applied by M-06) |
|---|---|---|
| **NPS** | −100 to +100 | `(raw + 100) / 2` |
| **CSAT-5pt** | 1–5 | `((raw − 1) / 4) × 100` |
| **CSAT-10pt** | 1–10 | `((raw − 1) / 9) × 100` |
| **CES-5pt** | 1–5 (low effort = better) | `((5 − raw) / 4) × 100` |
| **CES-7pt** | 1–7 (low effort = better) | `((7 − raw) / 6) × 100` |
| **FCR (binary)** | 0/1 | `raw × 100` |
| **FCR (%)** | 0–100 | as-is |
| **Sentiment** | 0–100 (pre-normalised by M-05) | as-is |

---

## 3.4 F4 — Versioning & Journey Lifecycle

**Priority:** Must-have. Manages journey evolution while preserving historical reporting integrity via the Hybrid Major/Minor Versioning model.

### 3.4.1 Major vs Minor Version Classification

| Major Version Trigger | Minor Version Trigger |
|---|---|
| Adding or removing a stage | Renaming journey, stage, or touchpoint |
| Adding or removing a touchpoint | Editing descriptions or customer goals |
| Reordering stages | Toggling the MoT flag |
| Adding or removing a KPI on a touchpoint | Changing importance scores (customer or business) |
| Changing KPI weights on a touchpoint | Channel list changes (default Major; user can override to Minor) |
| Changing `TP_weight` or `Stage_weight` values | Changing α, β, or `MOT_multiplier` (tenant parameters) |

### 3.4.2 Functional Requirements

- **FR-4.1:** Every journey has a current draft state (editable freely) and a series of published immutable versions. Responses attach to the published version active when collected.
- **FR-4.2:** First publish creates version 1.0. Format: `[MAJOR].[MINOR]`.
- **FR-4.5:** On Publish: system analyses diff, classifies each change, presents confirmation dialog: *"This update includes [N] structural changes (Major) and [M] cosmetic changes (Minor). Publishing will create version [X.Y]. [Publish] [Review changes] [Cancel]"*.
- **FR-4.8:** Reports support a 'Compare across versions' mode (side-by-side scorecards for same time range).
- **FR-4.10:** Each published version stores a complete immutable snapshot including all ScoringConfig parameters active at publish time.

### 3.4.3 Business Rules

- **BR-4.1:** A journey in Draft status has no published versions. It can be edited freely and published when ready (creating version 1.0).
- **BR-4.2:** A published version cannot be edited — to change a published journey, the user edits the draft state and publishes again.
- **BR-4.3:** Archiving a journey does not delete its versions. Historical reports continue to function against the snapshots.
- **BR-4.4:** The diff classification logic (FR-4.5) must be deterministic and auditable. The system must log every classification decision to a per-journey audit log.

---

## 3.5 F5 — Pain Point & Happy Moment Detection Configuration

M-16 owns the four detection signal configurations; M-06 owns the computation and result persistence.

### 3.5.1 Detection Signals

- **Signal 1 — Threshold-based:** Touchpoint score below `pain_threshold` (default 50/100) = pain; above `happy_threshold` (default 80/100) = happy.
- **Signal 2 — Volume-weighted impact:** Touchpoint score × response volume × `importance_to_customer` in the bottom/top X% of journey touchpoints. Default X = 20%.
- **Signal 3 — Trend-based:** Touchpoint score moved ≥ `trend_delta` (default 10 points) over comparison window vs previous equal-length period.
- **Signal 4 — Verbatim theme-based:** M-05 reports predominantly negative/positive verbatim themes above configurable minimum volume.

### 3.5.2 Classification Rules

- **Confirmed** pain/happy point: flagged by ≥ 2 signals.
- **Candidate** pain/happy point: flagged by exactly 1 signal.
- Touchpoints with `confidence_factor = 0` (below `n_floor`) are excluded from all detection signals.

### 3.5.3 Business Rules

- **BR-5.1:** Touchpoints with status `insufficient` (per M-06 minimum response threshold) are excluded from detection.
- **BR-5.2:** Detection results are persisted per period by M-06 for historical comparison.
- **BR-5.3:** A touchpoint flagged simultaneously as pain and happy is presented as *"Improving but still painful"* in M-07 reports.
- **BR-5.4:** Action priority = `importance_customer × importance_business × log(response_volume + 1)`, rounded. Higher = higher priority. Computed by M-06.

---

## 3.6 F6 — Reporting Output Definitions

M-16 exposes the following report data outputs via internal API to M-07. These outputs reflect the Strategic Scoring Model (Section 11).

> **NOTE:** UI reference: Image 2 shows the Journey Analytics view: top KPI summary cards (CSAT, CES, NPS, Response Rate), the Journey Map canvas with stage columns and touchpoint lists, Performance Trend chart (CSAT & NPS over 12 weeks), Score by Stage bar chart, and Touchpoint Performance ranking table with KPI badge, MoT badge, and score bar.

**Outputs:**

- **Journey Scorecard:** `Journey_strategic` score, all `Stage_strategic` scores, all `TP_score`s, with confidence indicators and version annotations.
- **Stage Detail:** All touchpoint scores within a stage, KPI-level breakdowns, effective weights.
- **Touchpoint Detail:** KPI-level scores, response volume (n per KPI), `confidence_status`, top verbatim themes from M-05, persona breakdown.
- **Pain/Happy Report:** Computed by M-06 using `DetectionConfig` thresholds.
- **Persona-Filtered View:** Any of the above filtered to a specific persona.
- **Version Comparison:** Side-by-side score outputs for two specified journey versions.

> **CONSTRAINT:** Every aggregated score (TP, Stage, Journey) must surface its `confidence_status` alongside the score value. A `low_sample` or `insufficient` status must be visually distinguished (e.g., grey-out, asterisk, badge). Scores below `n_floor` must never be displayed as a plain number — display as `—` or `Insufficient data`.

---

## 3.7 F7 — Permissions & RBAC

### 3.7.1 Permission Matrix

| Action | P-01 CX PM | P-02 Analyst | P-03 Survey Admin | P-04 Op Mgr | P-05 Frontline | P-06 Exec | P-07 IT Admin | P-08 QBS Tech | P-09 QBS Fin | P-10 QBS Prod |
|---|---|---|---|---|---|---|---|---|---|---|
| View journeys (all) | ✓ | ✓ | ✓ | ✓ | — | ✓ | — | ✓ (grant) | — | — |
| View journeys (own touchpoints) | — | — | — | — | ✓ (scoped) | — | — | — | — | — |
| Create journeys | ✓ | — | — | — | — | — | — | — | — | — |
| Edit / publish journeys | ✓ | — | — | — | — | — | — | — | — | — |
| Archive journeys | ✓ | — | — | — | — | — | — | — | — | — |
| Configure KPIs & weights | ✓ | — | — | — | — | — | — | — | — | — |
| Manage personas | ✓ | — | — | — | — | — | — | — | — | — |
| Approve published versions | ✓ (diff user) | — | — | — | — | — | — | — | — | — |
| View journey reports (full) | ✓ | ✓ | ✓ | ✓ (dept-scoped) | — | ✓ | — | — | — | — |
| View journey reports (own TPs) | — | — | — | — | ✓ | — | — | — | — | — |
| Configure detection thresholds | ✓ | — | — | — | — | — | — | — | — | — |
| Override default KPI weights | ✓ | — | — | — | — | — | — | — | — | — |
| Export journey config (JSON) | ✓ | — | — | — | — | — | — | — | — | — |
| Clone a journey | ✓ | — | — | — | — | — | — | — | — | — |

### 3.7.2 Functional Requirements

- **FR-7.1:** The system must enforce the permission matrix at both UI level (hide unauthorised actions) and API level (return 403 Forbidden on unauthorised requests).
- **FR-7.2:** Touchpoint ownership assignments must be UI-configurable by a CX Program Manager via an "Owners" multi-select on each touchpoint within a journey.
- **FR-7.3:** P-08 (QBS Tech) access grants must be visible to the granting tenant: an "Active Support Sessions" view shows current grants with grantee, start time, end time, and a revoke button.
- **FR-7.4:** All P-08 access actions must be logged to an immutable audit log queryable by the tenant (provided by M-11 audit log infrastructure).
- **FR-7.5:** The maker-checker rule (BR-7.1) must be enforced server-side, not just UI-side.

### 3.7.3 Business Rules

- **BR-7.1:** When maker-checker is enabled, the user who publishes a draft cannot be the same user who approves it. Server-side enforced.
- **BR-7.2:** P-08 grant defaults: maximum duration 4 hours, auto-revokes after that period. Tenant can configure shorter limits.
- **BR-7.3:** Permission changes take effect on the user's next request — no sessions are forcibly invalidated except when explicitly requested by the granting admin.

---

## 3.8 F8 — Localization (Arabic + English, RTL)

### 3.8.1 Functional Requirements

- **FR-8.1:** All user-entered content fields (names, descriptions, customer goals) must be stored bilingually — `field_en` and `field_ar` columns.
- **FR-8.2:** When the user is operating in Arabic UI: all UI labels appear in Arabic; RTL directionality is applied per WCAG and Nabadat brand standards; user-entered content is displayed in `field_ar` where available, falling back to `field_en` with an indicator if `field_ar` is empty.
- **FR-8.3:** Same fallback applies in reverse for English UI.
- **FR-8.4:** All system-generated messages (errors, confirmations, notifications) must be localised — every error message specified in this document must have both EN and AR versions.
- **FR-8.5:** All KPI labels remain in their canonical English form by industry convention, but include an Arabic descriptive label in parentheses.
- **FR-8.6:** All journey diagrams, scorecards, and reports must render correctly in RTL when Arabic is active — including stage ordering (RTL flows right-to-left).

### 3.8.2 Business Rules

- **BR-8.1:** A user cannot create a journey, stage, or touchpoint with only one language populated — both `name_en` and `name_ar` are mandatory.
- **BR-8.2:** Description fields are not mandatory but recommended in both languages.

---

## 3.9 F9 — Templates, Cloning & Export

### 3.9.1 Functional Requirements

- **FR-9.1:** The system must ship with a starter template library including: Banking (Open Account, Apply for Loan, Issue Resolution), Retail (Make Purchase, Return Item), Telecom (Plan Subscription, Service Activation, Complaint Resolution). Templates are not editable globally; a tenant cloning a template gets an editable Draft journey.
- **FR-9.2:** Templates do not include personas — a cloned template starts unbound.
- **FR-9.3:** Templates include suggested KPI configurations per touchpoint, MoT flags, and importance defaults. All can be edited after cloning.
- **FR-9.4:** A user with permission must be able to clone any existing journey within their tenant as a new Draft. The clone copies all stages, touchpoints, KPI configs, MoT flags, and persona bindings.
- **FR-9.5:** A user with permission must be able to export a journey configuration as JSON. The export includes: journey metadata, all stages, all touchpoints, KPI bindings, persona bindings. No response data is included.
- **FR-9.6:** JSON import is out of scope for v1.

### 3.9.2 Business Rules

- **BR-9.1:** Cloned journeys are fully independent — all touchpoints in a cloned journey are journey-local copies.
- **BR-9.2:** Exported JSON does not include any response data — config only.
- **BR-9.3:** Templates are managed by QBS Product (P-10) at the platform level.
- **BR-9.4:** The exported JSON must include a `schema_version` field for forward compatibility when import is added in v2.

---

## 3.10 F10 — Optional Maker-Checker Approval Workflow

### 3.10.1 Functional Requirements

- **FR-10.1:** A tenant-level setting `maker_checker_enabled` (boolean, default false) controls whether the workflow is active. Set by P-01 CX Program Manager.
- **FR-10.2:** When enabled, clicking Publish enters the journey into an `Awaiting Approval` state. The draft is locked from further edits during this state.
- **FR-10.3:** An eligible Approver (another user with the CX Program Manager role for this tenant — not the user who published) must explicitly Approve or Reject the pending version.
- **FR-10.4:** On Approve, the new version goes live. On Reject, the draft returns to editable state; the Rejector must provide a comment. The comment is visible to the original Maker.
- **FR-10.5:** A notification (via M-09) is sent to all eligible Approvers when a journey enters `Awaiting Approval` state.
- **FR-10.6:** A timeout is configurable per tenant (default 7 days). If no decision is made within the timeout, M-09 sends a reminder notification.

### 3.10.2 Business Rules

- **BR-10.1:** A user cannot approve their own draft (enforced server-side).
- **BR-10.2:** If a tenant has only one user with the CX Program Manager role and maker-checker is enabled, the system must display a warning: *"Maker-checker requires at least two users with the CX Program Manager role. Currently only one is assigned."*
- **BR-10.3:** Maker-checker applies to all journey publishes, both Major and Minor versions, when enabled.

---

# 4. Data Model

## 4.1 Entity Relationship Overview

The following entities are owned by M-16. Standard audit fields (`id`, `tenant_id`, `created_at`, `created_by`, `updated_at`, `updated_by`) are present on all entities but omitted from per-entity tables for brevity.

```
Tenant
  ├─ Persona (0..50 per tenant)
  ├─ ScoringConfig  ← tenant-level scoring parameters (1 per tenant)
  ├─ Journey (0..N per tenant)
  │    ├─ JourneyVersion (immutable snapshots)
  │    ├─ JourneyPersonaMap (M:N → Persona)
  │    └─ Stage (1..20 per journey)
  │         └─ TouchpointInJourney (0..30 per stage, ≤300 per journey)
  │              └─ KPIBinding (1..5 per touchpoint)
  └─ DetectionConfig (tenant-level signal thresholds)

M-06 consumes:  KPIBinding + ScoringConfig (tenant-level) → computes scores → persists ScoreSnapshot
M-06 consumes:  DetectionConfig → runs signals → persists DetectionResult
```

---

## 4.2 Entity Specifications

### 4.2.1 Persona

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `name_en` | varchar(100) | No | Unique per tenant per language |
| `name_ar` | varchar(100) | No | Unique per tenant per language |
| `description_en` | text(500) | Yes | — |
| `description_ar` | text(500) | Yes | — |
| `demographics` | jsonb | Yes | Structured demographic attributes |
| `behavioral_attrs` | jsonb | Yes | Structured behavioural attributes |
| `avatar_icon` | varchar(50) | Yes | FK to built-in icon set |
| `status` | enum | No | Draft, Active, Archived |

### 4.2.2 Journey

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `name_en` | varchar(150) | No | Unique per tenant |
| `name_ar` | varchar(150) | No | Unique per tenant |
| `description_en` | text(1000) | Yes | — |
| `description_ar` | text(1000) | Yes | — |
| `journey_type` | enum | No | Transactional, Lifecycle, Issue-Resolution, Onboarding |
| `owner_user_id` | uuid (FK → M-10 User) | No | — |
| `expected_duration_days` | integer | Yes | — |
| `status` | enum | No | Draft, Active, Archived |
| `current_version` | varchar(10) | Yes | e.g., "2.3" — set on first publish |
| `current_draft_state` | jsonb | Yes | Working draft of stages, touchpoints, configs |

### 4.2.3 JourneyVersion

Immutable snapshot of a journey at publish time.

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `journey_id` | uuid (FK) | No | — |
| `version_number` | varchar(10) | No | e.g., "1.0", "1.1", "2.0" |
| `version_type` | enum | No | Major, Minor |
| `published_at` | timestamp | No | — |
| `published_by` | uuid (FK → M-10 User) | No | — |
| `approved_by` | uuid (FK → M-10 User) | Yes | Set when maker-checker is on |
| `approval_status` | enum | No | Approved, Awaiting Approval, Rejected, N/A |
| `rejection_reason` | text | Yes | — |
| `effective_from` | timestamp | No | When this version becomes active |
| `effective_until` | timestamp | Yes | NULL = current/active version |
| `snapshot_payload` | jsonb | No | Full immutable snapshot of journey config at publish time, including ScoringConfig values active at publish |
| `change_diff` | jsonb | No | Structured diff vs prior version, Major/Minor per change |

### 4.2.4 JourneyPersonaMap

M:N between Journey and Persona, with optional per-persona importance overlays.

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `journey_id` | uuid (FK) | No | Unique constraint on (journey_id, persona_id) |
| `persona_id` | uuid (FK) | No | — |
| `importance_overlays` | jsonb | Yes | `{touchpoint_id: {importance_customer, importance_business}, ...}` |

### 4.2.5 Stage

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `journey_id` | uuid (FK) | No | — |
| `name_en` | varchar(100) | No | Unique per journey per language |
| `name_ar` | varchar(100) | No | Unique per journey per language |
| `customer_goal_en` | text(500) | Yes | — |
| `customer_goal_ar` | text(500) | Yes | — |
| `expected_emotion` | enum | Yes | Excited, Neutral, Anxious, Frustrated, Confident, Confused, Relieved |
| `expected_duration_hours` | integer | Yes | — |
| `sequence_flag` | enum | No | Sequential, Parallel |
| `sequence_order` | integer | No | Position within journey |
| `stage_weight` | smallint | No | Raw positive integer. Engine normalises within journey. Default: 1 (equal weight). |

### 4.2.6 TouchpointInJourney

The journey-local instance of a touchpoint. All touchpoints are journey-local in v1.

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `stage_id` | uuid (FK) | No | — |
| `name_en` | varchar(100) | No | Unique per journey per language |
| `name_ar` | varchar(100) | No | — |
| `description_en` | text(1000) | Yes | — |
| `description_ar` | text(1000) | Yes | — |
| `channels` | array of enum | No | At least one. See BR-2.4 for valid values. |
| `importance_customer` | smallint | No | 1–5 |
| `importance_business` | smallint | No | 1–5 |
| `is_mot` | boolean | No | Moment of Truth flag |
| `is_mandatory` | boolean | No | Per FR-2.13; consumed by M-06 for volume normalisation |
| `sequence_order` | integer | No | Position within stage |
| `tp_weight` | smallint | No | Raw positive integer. Engine normalises within stage. Default: 1 (equal weight). |
| `owner_user_ids` | array of uuid | Yes | FK → M-10 Users; drives P-04/P-05 scoping |

### 4.2.7 KPIBinding

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `touchpoint_in_journey_id` | uuid (FK) | No | — |
| `kpi_type` | enum | No | NPS, CSAT-5pt, CSAT-10pt, CES-5pt, CES-7pt, FCR, Sentiment |
| `weight_pct` | smallint | No | 0–100 inclusive; sum across touchpoint must = 100 |

**Constraint:** Per `touchpoint_in_journey_id`, sum of `weight_pct` across all `KPIBinding`s must equal 100.

### 4.2.8 DetectionConfig

Tenant-level configuration for pain/happy detection signals.

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `tenant_id` | uuid (FK → M-11 Tenant) | No | One config row per tenant |
| `pain_threshold` | numeric(5,2) | No | Default 50.00; score below this = pain signal 1 |
| `happy_threshold` | numeric(5,2) | No | Default 80.00; score above this = happy signal 1 |
| `volume_pct_threshold` | smallint | No | Default 20; bottom/top X% for signal 2 |
| `trend_delta` | numeric(5,2) | No | Default 10.00; score change threshold for signal 3 |
| `trend_window_days` | integer | No | Default 30; comparison window for signal 3 |
| `verbatim_min_volume` | integer | No | Minimum theme volume for signal 4 to trigger |
| `min_response_threshold` | integer | No | Default 5; consumed by M-06 for KPI sufficiency check |

### 4.2.9 ScoringConfig (NEW — Tenant-Level)

Stores the scoring model parameters at the tenant level. One row per tenant. Owned by M-16, configured via the M-11 tenant settings surface, and consumed by M-06 for all score computation across all journeys belonging to that tenant.

> **NOTE:** ScoringConfig is tenant-scoped, not journey-scoped. This ensures scoring methodology is consistent and cross-journey comparable within a tenant. A change to α or `MOT_multiplier` applies to all subsequent M-06 computation cycles for all journeys of that tenant.

> **NOTE:** Snapshotting: `JourneyVersion.snapshot_payload` must include a copy of the tenant's ScoringConfig values at publish time. This ensures that historical score recomputation for a given version uses the parameters that were active when that version was live — not the current tenant config.

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `tenant_id` | uuid (FK → M-11 Tenant) | No | One ScoringConfig per tenant. Unique constraint on `tenant_id`. |
| `alpha` | numeric(4,3) | No | Customer importance blend. Range 0.000–1.000. Default 0.500. Check: `alpha BETWEEN 0.0 AND 1.0`. |
| `mot_multiplier` | numeric(3,1) | No | MOT amplification. Range 1.0–2.0. Default 1.5. Check: `mot_multiplier BETWEEN 1.0 AND 2.0`. |
| `n_floor` | integer | No | Hard minimum response count. Default 5. Check: `n_floor >= 1`. |
| `flag_percentile` | integer | No | The k in P_k used to compute `n_flag_threshold`. Default 25. Valid range 1–49. Check: `flag_percentile BETWEEN 1 AND 49`. |
| `rolling_window_days` | integer | No | Window for P_k and median computation; also the cold-start period length. Default 30. Check: `rolling_window_days >= 7`. |

> **NOTE:** `beta` is not stored — always derived as `1 - alpha` at computation time. Storing beta would risk α + β ≠ 1.0 through a partial update.

---

## 4.3 Entities Owned by Sibling Modules (consumed here)

| Entity | Owner Module | Role in M-16 |
|---|---|---|
| **ScoreSnapshot** | M-06 | Computed scores at touchpoint/stage/journey level. Consumed by M-16 for report display. |
| **DetectionResult** | M-06 | Pain/happy detection results per touchpoint per period. Computed by M-06 using DetectionConfig from M-16. |
| **SurveyJourneyBinding** | M-01 | Links a survey to a journey. Owned and managed in M-01. |
| **QuestionTouchpointBinding** | M-01 | Links a survey question to a touchpoint with a KPI tag. Owned and managed in M-01. |

---

# 5. External Interface Requirements

## 5.1 UI Requirements — Journey Analytics View

The Journey Analytics view (Image 2) is the primary reporting surface for a journey. It is rendered by M-07 using data from M-16/M-06 APIs. The following UI elements must be supported by the data contracts:

- **Top summary cards:** Journey-level CSAT%, avg CES score, NPS score, Response Rate — each with delta vs last period and n= count.
- **Journey Map canvas:** Horizontal stage columns, each showing stage satisfaction % badge (top right), stage emotion icon + label, customer goal quote, stage metadata (Sequential/Parallel, TP count, KPI count), and touchpoint list with MoT indicator dots.
- **Performance Trend chart:** Multi-metric trend over configurable period (default 12 weeks).
- **Score by Stage bar chart:** `Stage_strategic` scores for all stages, horizontally ordered.
- **Touchpoint Performance table:** All touchpoints ranked by `TP_score`, showing Stage, KPI badge count, MoT badge, and score bar. Rows with `low_sample` or `insufficient` `confidence_status` must be visually differentiated.

> **NOTE:** Image 3 shows the touchpoint hover tooltip on the canvas: displays touchpoint name (bilingual), channel tag, Importance to Customer stars, Importance to Business stars, MoT badge, Mandatory badge, and KPI list. This is read-only display — editing opens the Configure Touchpoint panel (Image 5).

---

## 5.2 API Requirements

### Scoring Model Endpoints (added v0.4)

These are tenant administration endpoints surfaced in the M-11 settings surface:

- `GET /api/tenant/scoring-config` — Read current tenant scoring parameters.
- `PUT /api/tenant/scoring-config` — Update tenant scoring parameters. Validates α ∈ [0.0, 1.0] and `MOT_multiplier` ∈ [1.0, 2.0]. Returns `400 INVALID_ALPHA_BETA_SUM` if α is outside range. Returns `400 MOT_MULTIPLIER_OUT_OF_RANGE` if `MOT_multiplier` is outside [1.0, 2.0]. **Permission:** P-01 CX Program Manager only.

The journey-level scoring-config endpoints from v0.3 (`/api/journeys/{id}/scoring-config`) are removed.

### Personas

- `GET /api/personas` — List, paginated
- `POST /api/personas` — Create
- `GET /api/personas/{id}` — Read
- `PUT /api/personas/{id}` — Update
- `DELETE /api/personas/{id}` — Archive (no hard delete)

### Journeys

- `GET /api/journeys` — List
- `POST /api/journeys` — Create draft
- `GET /api/journeys/{id}` — Read (current draft state + version metadata)
- `PUT /api/journeys/{id}/draft` — Update draft
- `POST /api/journeys/{id}/publish` — Publish (creates version; may enter awaiting-approval state)
- `POST /api/journeys/{id}/approve` — Approve a pending version (maker-checker)
- `POST /api/journeys/{id}/reject` — Reject a pending version
- `GET /api/journeys/{id}/versions` — List published versions
- `GET /api/journeys/{id}/versions/{v}` — Read a specific version snapshot
- `POST /api/journeys/{id}/clone` — Clone
- `GET /api/journeys/{id}/export` — Export JSON

### Detection Configuration

- `GET /api/detection-config` — Read tenant detection configuration
- `PUT /api/detection-config` — Update tenant detection configuration

### Report Data (consumed by M-07)

- `GET /api/reports/journey-scorecard?journey_id=...&version=...&period=...&persona=...`
- `GET /api/reports/pain-happy?journey_id=...&period=...&persona=...`
- `GET /api/reports/version-compare?journey_id=...&v1=...&v2=...&period=...`

### Picker Components (consumed by M-01)

- `GET /api/pickers/journeys`
- `GET /api/pickers/touchpoints?journey_id=...`
- `GET /api/pickers/kpi-eligibility?question_scale=...`

### Error Response Conventions

All endpoints follow a consistent error response shape:

```json
{
  "error": {
    "code": "string — machine-readable, e.g. 'JOURNEY_HAS_ACTIVE_BINDINGS'",
    "message_en": "string — human-readable English message",
    "message_ar": "string — human-readable Arabic message",
    "details": {}
  }
}
```

| Status Code | Meaning |
|---|---|
| 400 | Validation errors (KPI weights do not sum to 100, missing required field) |
| 403 | Permission denied |
| 404 | Entity not found |
| 409 | Conflict (uniqueness violation, attempting to delete in-use entity) |
| 422 | Semantic error (attempting to publish a journey with empty stages) |
| 500 | Internal error |

---

## 5.3 Integration with M-06 (CX Metrics & KPI Engine) — Updated

**M-16 provides to M-06:**

- `ScoringConfig` per tenant (α, `MOT_multiplier`, `n_floor`, `flag_percentile`, `rolling_window_days`) — via `GET /api/tenant/scoring-config`. M-06 reads this once per computation cycle per tenant.
- `TP_weight` per `TouchpointInJourney`
- `Stage_weight` per `Stage`
- `n_KPI` per KPI per touchpoint per period (stored by M-04 Response Collection Engine, read by M-06)

**M-06 is responsible for:**

- Computing `n_TP` (internal only) using the KPI-weighted formula from Section 11.3
- Computing `confidence_factor` for each touchpoint per rolling window, using `flag_percentile` as k
- Computing `TP_effective_weight` for each touchpoint
- Computing `Stage_strategic` and `Journey_strategic` using the formulas in Section 11
- Attaching `confidence_status` to every `ScoreSnapshot`
- Respecting the `n_TP < n_floor` exclusion rule (exclude from numerator AND denominator)

---

# 6. Non-Functional Requirements

NFR-1 through NFR-23 from v0.2 remain unchanged. The following additions apply:

- **NFR-24:** Scoring computation (Levels 1–4) must complete within the M-06 computation SLA. No score computation occurs inside M-16's request path.
- **NFR-25:** `ScoringConfig` updates take effect on the next M-06 computation cycle. Changes do not retroactively alter historical `ScoreSnapshot`s.
- **NFR-26:** The α + β = 1.0 constraint must be enforced at both API validation and database constraint level. A check constraint on the `ScoringConfig` table ensures `alpha BETWEEN 0.0 AND 1.0`.

---

# 7. Dependency & Side-Effect Analysis

All analysis from v0.2 Section 7 remains valid. The following additions apply:

| Dimension | Finding |
|---|---|
| **Data Model** | `ScoringConfig` entity moved from per-journey to per-tenant (1 row per tenant, FK = `tenant_id`). New fields: `tp_weight` on `TouchpointInJourney`, `stage_weight` on `Stage`. No existing entities removed or broken. |
| **API Contracts** | Journey-level scoring-config endpoints removed. Two new tenant-level endpoints added (`GET`/`PUT /api/tenant/scoring-config`). All v0.2 endpoints unchanged. |
| **M-11 Tenant Admin** | M-11 settings surface must render the ScoringConfig editing UI: α/β slider, `MOT_multiplier` input, `n_floor` input, `flag_percentile` input, `rolling_window_days` input. M-11 does not own the `ScoringConfig` entity — M-16 does. M-11 renders the UI; M-16 API persists it. |
| **M-06 Interface** | M-06 reads `ScoringConfig` once per tenant per computation cycle. M-06 must: implement `confidence_factor` formula using `flag_percentile` (P_k), implement `TP_effective_weight`, attach `confidence_status` to every `ScoreSnapshot`. **Critical coordination item before M-16 development starts.** |
| **M-07 Display** | `ScoreSnapshot` carries `confidence_status` per score. M-07 implements three-state display rules from Section 11.8. |
| **M-09 Alerts** | Alert engine checks `trend_significant` before firing trend-based alerts. |

---

# 8. Out of Scope

All items from v0.2 Section 8 remain out of scope. Additional out-of-scope items:

- Automatic/AI-driven configuration of α, β, `TP_weight`, or `Stage_weight` (future ML feature)
- Cross-journey weight benchmarking (comparing `ScoringConfig` parameters across tenants)
- Journey 'what-if' simulation (changing weights and previewing impact on scores)

---

# 9. Open Questions / Decisions Pending

| # | Required By | Question |
|---|---|---|
| **Q1** | Engineering kickoff | Pre-module data migration: no migration for v1. Historical data remains journey-less. **CONFIRMED.** |
| **Q2** | Before F2 implementation | Touchpoint ownership scope for P-04: Department entity not yet in M-10. Coordinate with M-10 team. |
| **Q3** | Before F1 UI | Built-in icon set for personas: number of icons and MENA cultural appropriateness. Coordinate with brand team. |
| **Q4** | Before F2 implementation | Stage emotion enum: fixed for v1, extensible in v2. **CONFIRMED.** |
| **Q5 ★ CRITICAL** | Before F3 + Section 11 implementation | M-06 KPI configuration and Scoring Model interface: The `ScoringConfig` schema and all Level 1–4 formulas in Section 11 must be agreed with the M-06 team before M-16 development starts. M-06 must expose `ScoreSnapshot` with `confidence_status` field. |
| **Q6** | Before F5 GA | Detection threshold defaults: Pain ≤ 50, Happy ≥ 80 globally for v1. **CONFIRMED.** Document for post-launch tuning. |
| **Q7** | Before F9 implementation | Template content: Final template catalogue needs CX consultant review per industry. |
| **Q8** | Post-launch | Persona version-locking: Acceptable not to version-lock in v1. Revisit if tenants request. |
| **Q9** | Before F9 | JSON export schema: Must include `schema_version` field for v2 import compatibility. |
| **Q10** | Before M-16 GA | Customer-Persona association: M-03 must support persona tagging for persona-filtered scoring in M-06. |
| **Q11 ✓ RESOLVED** | — | ScoringConfig scope: Confirmed as tenant-level only. One `ScoringConfig` row per tenant. No per-journey overrides. All journeys within a tenant share the same scoring parameters. `ScoringConfig` values are snapshotted in `JourneyVersion.snapshot_payload` at publish time for historical integrity. |
| **Q12 ✓ RESOLVED** | — | Cold-start display: No special state required. During the first `rolling_window_days`, the system falls back to `n_floor` as the flat threshold and displays scores normally for touchpoints with n ≥ `n_floor`. Users expect data approximately one month after launch, aligning with the default 30-day window. No `early_data` badge or 'calibrating' label needed. The `early_data` confidence_status value has been removed from the spec. |

---

# 10. Glossary

All terms from v0.2 Section 10 remain. The following terms are added in v0.3–v0.5:

| Term | Definition |
|---|---|
| **Channel** | The medium through which a customer interaction occurs (Web, Mobile App, Email, SMS, WhatsApp, Phone, Branch, Chat, IVR, Social Media, Kiosk, Other). |
| **CES** | Customer Effort Score. Ease-of-interaction metric. Lower scores = easier interactions. Typically 1–5 or 1–7 scale. |
| **CSAT** | Customer Satisfaction Score. Post-interaction satisfaction rating, typically 1–5 or 1–10 scale. |
| **confidence_factor** | A continuous value (0–1.0) applied as a dampening multiplier on a touchpoint's effective weight based on response volume relative to the journey median. System-computed; never user-facing. |
| **confidence_status** | A categorical label (`reliable`, `low_sample`, `insufficient`) attached to every score surfaced to M-07. Three values only. The sole user-facing reliability signal — no margin of error is displayed. |
| **Detractor** | An NPS respondent scoring 0–6. |
| **FCR** | First Contact Resolution. Whether a customer's issue was resolved on the first contact. Binary or percentage. |
| **flag_percentile** | The percentile k (tenant-configurable, default 25) used in `n_flag_threshold = max(n_floor, P_k(...))`. A touchpoint whose `n_TP` falls below the k-th percentile of the journey's response distribution is flagged as `low_sample`. Stored in `ScoringConfig`. |
| **Happy Moment** | A touchpoint flagged as consistently high-scoring or delight-inducing. |
| **Importance to Business** | A 1–5 score of how much a touchpoint matters to business outcomes (revenue, retention). |
| **Importance to Customer** | A 1–5 score of how much a touchpoint matters to the customer. |
| **Journey** | An end-to-end logical sequence of stages a customer traverses to achieve a goal. |
| **Journey-Local Touchpoint** | A touchpoint created within and exclusive to a single journey. All touchpoints are journey-local in v1. |
| **KPI** | In this module: a CX metric (NPS, CSAT, CES, FCR, Sentiment) bound to a touchpoint with a weight. Configuration owned by M-16; computation performed by M-06. |
| **M-06** | CX Metrics & KPI Engine — the Nabadat module responsible for computing scores and running pain/happy detection using configurations defined in M-16. |
| **Maker-Checker** | An optional approval workflow where one user publishes and a different user approves. |
| **Major Version** | A journey version reflecting a structural change. |
| **Minor Version** | A journey version reflecting a cosmetic change. |
| **MoT** | Moment of Truth. A touchpoint that disproportionately shapes the customer's overall perception of the brand. |
| **n_floor** | Hard minimum response count below which a touchpoint score is excluded from scoring and detection. Tenant-configurable; default 5. Serves as the flat confidence threshold during the cold-start period. |
| **n_full_confidence** | The median `n_TP` across all touchpoints in a journey over the rolling window. A touchpoint at this response count receives `confidence_factor = 1.0`. Falls back to `n_floor` logic during the cold-start period. |
| **n_TP** | Internal: KPI-weight-averaged response count for a touchpoint. Used only in `confidence_factor` calculation. Never surfaced to users under any label. |
| **Normalised Score** | A KPI score converted to a 0–100 scale to enable cross-KPI weighting. Formulas defined here; applied by M-06. |
| **NPS** | Net Promoter Score. Loyalty metric −100 to +100. Promoters = 9–10, Passives = 7–8, Detractors = 0–6. |
| **Pain Point** | A touchpoint flagged as consistently low-scoring or high-friction. |
| **Persona** | A representative archetype of a customer segment with shared behaviours and needs. |
| **Relational Survey** | A periodic survey measuring overall relationship health. Owned by M-01. |
| **Response Volume** | The count of responses contributing to a score in a given reporting period. |
| **Sentiment** | A 0–100 score derived from M-05 text analytics on open-ended verbatim responses. |
| **ScoringConfig** | The M-16 entity storing scoring model parameters at the tenant level (one row per tenant). Fields: `alpha`, `mot_multiplier`, `n_floor`, `flag_percentile`, `rolling_window_days`. Consumed by M-06. Snapshotted in `JourneyVersion` at publish. |
| **Stage** | A logical phase within a journey containing one or more touchpoints. |
| **Stage_confidence** | The proportion of a stage's touchpoints that passed the `n_floor` threshold. Applied as a dampening multiplier on `Stage_weight` in journey-level aggregation. |
| **Stage_effective_weight** | `Stage_weight × Stage_confidence`. The actual weight a stage contributes to `Journey_strategic` score. |
| **Stage_strategic** | The weighted composite satisfaction score for a stage, computed using `TP_effective_weight` across all contributing touchpoints. |
| **Tenant** | A client organisation using Nabadat; all data is isolated per tenant (enforced by M-11). |
| **Touchpoint** | A discrete interaction point between the customer and the brand within a stage. |
| **TP_effective_weight** | The composite strategic weight of a touchpoint, combining `TP_weight`, blended importance (α×customer + β×business), `MOT_multiplier`, and `confidence_factor`. |
| **TP_score** | The composite satisfaction score for a touchpoint, computed as the KPI-weight-averaged sum of normalised KPI scores. |
| **TP_weight** | The CX Program Manager's configured strategic prominence score for a touchpoint within its stage. Distinct from `importance_to_customer` and `importance_to_business`. |
| **Transactional Survey** | A survey triggered by a specific interaction. Owned by M-01. |
| **Verbatim** | A customer's free-form text response to an open-ended survey question. |
| **Verbatim Theme** | A topic extracted by M-05 from grouped verbatims. |
| **Version Snapshot** | An immutable record of a journey's full configuration at a point in time, created at each publish. |

---

# 11. Strategic Satisfaction Scoring Model

> This section is the authoritative specification of the multi-level satisfaction scoring model used by Nabadat M-16. The configuration defined here is owned by M-16 and consumed by M-06 for all score computation. **M-06 must implement this model exactly as specified.**

> **NOTE:** This section must be read end-to-end by M-06 engineers before implementation. The model has four levels (KPI → Touchpoint → Stage → Journey) with explicit confidence mechanics at every level.

---

## 11.1 Model Overview

Satisfaction is calculated bottom-up: KPI scores roll up to a Touchpoint score, touchpoint scores roll up to a Stage score, and stage scores roll up to a Journey score. Each level introduces strategic weighting informed by business priorities and statistical confidence.

---

## 11.2 Level 1 — KPI Score

Each KPI produces a normalised score (0–100) using the formulas in Section 3.3.4. The raw response count (`n_KPI`) is stored per KPI per touchpoint per reporting period.

```
KPI_score_i  →  native formula per Section 3.3.4 (CSAT, NPS normalised, CES normalised, custom)
Store per KPI:  n_KPI_i  (count of valid responses for this KPI at this touchpoint in the period)
```

---

## 11.3 Level 2 — Touchpoint Score

The touchpoint score is a KPI-weight-averaged composite of its normalised KPI scores.

```
TP_score = Σ(KPI_score_i × KPI_weight_i) / Σ(KPI_weight_i)
```

The internal representative response count for the touchpoint is computed as a KPI-weight-average (not a minimum):

```
n_TP = Σ(KPI_weight_i × n_KPI_i) / Σ(KPI_weight_i)
```

> **CONSTRAINT:** `n_TP` is an **internal calculation variable ONLY**. It must never be displayed in any UI, dashboard, API response, export, or log accessible to users. Its sole purpose is to feed the `confidence_factor` calculation (Level 3). It must not appear under any label in any user-facing context.

> **NOTE:** Rationale for KPI-weighted `n_TP` over minimum: Using `min(n_KPI)` would allow a single low-weight, low-sample KPI to make the entire touchpoint appear unreliable when its primary KPI is well-sampled. The KPI-weighted average gives higher-weight KPIs proportional influence on the touchpoint's representative sample count.

---

## 11.4 Level 3 — Stage Score (Strategic)

This is the most sophisticated level. The stage score uses a Strategic Effective Weight formula that combines four inputs per touchpoint: the program manager's configured `TP_weight`, the blended importance (customer + business), the MoT multiplier, and a statistical confidence dampening factor based on response volume.

### 11.4.1 Confidence Tier 1 — Flag Threshold (Binary)

Determines whether a touchpoint is flagged as low-confidence for display purposes.

```
n_flag_threshold = max(n_floor, P_k(n_TP across all touchpoints in journey, rolling window))
```

Where:

- **P_k** = the k-th percentile of `n_TP` values across all touchpoints in the current journey, over the tenant-configured rolling window. **k is tenant-configurable via `flag_percentile` (default 25)**, meaning the bottom quarter of touchpoints by response volume is flagged. Tenants may raise this to e.g. 30 or 32 to apply a stricter flagging standard.
- **n_floor** = hard minimum floor (tenant-configurable, default 5). Below this, a score is never treated as reliable regardless of the journey's own distribution. This prevents degenerate cases where all touchpoints have very low volumes and P_k itself lands below a meaningful threshold.
- If `n_TP_i < n_flag_threshold` → touchpoint score is flagged as `low_sample` in the UI.
- **Cold start:** During the first `rolling_window_days` of a new journey or touchpoint, fall back to `n_floor` as the flat threshold (P_k cannot be computed without history).

### 11.4.2 Confidence Tier 2 — Confidence Factor (Continuous)

A continuous dampening value used in the effective weight formula. Uses sqrt for a smooth curve without cliff effects.

```
n_full_confidence = median(n_TP across all touchpoints in the journey, rolling window)

confidence_factor(n_i):
  = 0                                          if n_TP_i < n_floor
  = min(1.0, sqrt(n_TP_i / n_full_confidence)) otherwise
```

**Behaviour examples** (where `n_full_confidence = median`):

| n_TP relative to median | confidence_factor |
|---|---|
| `n_TP = median` | **1.0** (full weight) |
| `n_TP = median / 4` | **0.5** (half weight) |
| `n_TP = median / 16` | **0.25** (quarter weight) |
| `n_TP > median` | **1.0** (capped — no super-weighting) |
| `n_TP < n_floor` | **0** (excluded from scoring entirely) |

> **NOTE:** Median is used rather than mean to robustly handle outlier touchpoints (e.g., a single very high-volume mandatory touchpoint that would inflate the mean and unfairly dampen all others). High-volume touchpoints are capped at 1.0 — their advantage is already reflected in the statistical reliability of their KPI score.

### 11.4.3 Effective Weight Formula

The effective weight of each touchpoint in stage scoring combines all four strategic inputs:

```
TP_effective_weight_i = TP_weight_i
                      × (α × importance_to_customer_i + β × importance_to_business_i)
                      × MOT_multiplier_i
                      × confidence_factor_i
```

**Variable definitions:**

| Variable | Definition | Who configures it |
|---|---|---|
| `TP_weight_i` | The CX Program Manager's explicit strategic decision about this touchpoint's prominence in the CX programme. Independent of the empirical importance scores. | CX Program Manager (P-01) per touchpoint-in-journey |
| `importance_to_customer_i` | How much customers care about this touchpoint — their emotional/practical stakes. Based on research, persona data, or domain knowledge. Scale 1–5. | CX Program Manager (P-01) per touchpoint-in-journey |
| `importance_to_business_i` | How much the business cares — revenue impact, compliance exposure, brand risk. Scale 1–5. | CX Program Manager (P-01) per touchpoint-in-journey |
| `α (alpha)` | Blend weight for customer importance. Hard constraint: α + β = 1.0. Configurable at tenant level. | CX Program Manager. UI: single slider (moving α auto-adjusts β). Tenant settings surface. |
| `β (beta)` | Blend weight for business importance. Always derived as β = 1 − α. | Derived from α. Not independently editable. |
| `MOT_multiplier_i` | 1.0 for non-MoT touchpoints. Tenant-configured value [1.0–2.0] for MoT-flagged touchpoints. | Tenant-level. Range: 1.0–2.0. Default: 1.5. Configured in M-11 tenant settings. |
| `confidence_factor_i` | Continuous dampening from Section 11.4.2. Range 0–1.0. | System-computed. Not user-configurable. |

> **CONSTRAINT:** α + β = 1.0 is a **hard system constraint** enforced at the API and database layer. The UI must present α and β as a single linked slider, not two independent numeric inputs. Any API call where α + β deviates from 1.0 by more than 0.001 floating-point tolerance must be rejected with error `INVALID_ALPHA_BETA_SUM`.

> **CONSTRAINT:** `MOT_multiplier` is validated to the range [1.0, 2.0] inclusive. Values outside this range must be rejected with error `MOT_MULTIPLIER_OUT_OF_RANGE`. This cap ensures that even the most extreme MoT amplification (2.0×) combined with high importance scores does not produce unbounded weight dominance.

### 11.4.4 Operationalising the Three Separate Concerns

`TP_weight`, `importance_to_customer`, and `importance_to_business` are three distinct inputs that must not be conflated. The UI must present them with distinct labels and tooltips:

| Variable | What it captures | When it changes | Risk if confused |
|---|---|---|---|
| `TP_weight` | Programme editorial decision: how much has the organisation invested in and prioritised this touchpoint? | When programme strategy shifts | Understates/overstates which touchpoints the programme is actively managing |
| `importance_to_customer` | Evidence input: how much do customers care about this moment in their experience? | When customer behaviour or persona research changes | Programme misses touchpoints customers care about most |
| `importance_to_business` | Evidence input: revenue impact, compliance exposure, or brand risk of this touchpoint | When business strategy changes | Programme ignores commercially critical touchpoints |

> **NOTE:** The key insight: a touchpoint can have low `TP_weight` (the programme has not invested heavily here) but high `importance_to_customer` (customers care deeply). That tension is meaningful signal — it tells the program manager where the programme is underinvesting. If all three variables were collapsed into one, this insight disappears.

### 11.4.5 Stage Strategic Score

```
Stage_strategic = Σ(TP_score_i × TP_effective_weight_i) / Σ(TP_effective_weight_i)
```

Only touchpoints where `confidence_factor_i > 0` contribute to the stage score. A touchpoint with `n_TP_i < n_floor` is assigned `confidence_factor = 0` and is fully excluded from both numerator and denominator — it is **not treated as zero**.

---

## 11.5 Level 4 — Journey Score (Strategic)

The journey score aggregates stage scores, with each stage's contribution dampened by a stage-level confidence factor that reflects how much of its data is usable.

```
Stage_confidence_i     = (touchpoints with confidence_factor > 0 in stage_i)
                         / (total touchpoints configured in stage_i)

Stage_effective_weight_i = Stage_weight_i × Stage_confidence_i

Journey_strategic      = Σ(Stage_strategic_i × Stage_effective_weight_i)
                         / Σ(Stage_effective_weight_i)
```

A stage where all touchpoints have been excluded (`Stage_confidence = 0`) contributes nothing to the journey score and is flagged in the UI as fully excluded from the current period's calculation.

---

## 11.6 Complete Formula Set Reference

The following is the canonical reference for M-06 implementation:

```
─── LEVEL 1: KPI Score ──────────────────────────────────────────────────────
  KPI_score_i        → native formula (Section 3.3.4)
  Store: n_KPI_i per KPI

─── LEVEL 2: Touchpoint Score ───────────────────────────────────────────────
  TP_score           = Σ(KPI_score_i × KPI_weight_i) / Σ(KPI_weight_i)
  n_TP               = Σ(KPI_weight_i × n_KPI_i) / Σ(KPI_weight_i)   ← INTERNAL ONLY

─── LEVEL 3: Stage Score ────────────────────────────────────────────────────
  n_full_confidence  = median(n_TP_i across all TPs in journey, rolling_window)
  n_flag_threshold   = max(n_floor, P_k(n_TP_i across all TPs, rolling_window))
                       k = flag_percentile (tenant-level, default 25)

  confidence_factor_i:
    = 0                                            if n_TP_i < n_floor
    = min(1.0, sqrt(n_TP_i / n_full_confidence))   otherwise

  TP_effective_weight_i = TP_weight_i
                        × (α × importance_to_customer_i
                           + β × importance_to_business_i)
                        × MOT_multiplier_i
                        × confidence_factor_i

  Stage_strategic    = Σ(TP_score_i × TP_effective_weight_i)
                       / Σ(TP_effective_weight_i)

─── LEVEL 4: Journey Score ──────────────────────────────────────────────────
  Stage_confidence_i       = (TPs with confidence_factor > 0 in stage_i)
                             / (total TPs in stage_i)
  Stage_effective_weight_i = Stage_weight_i × Stage_confidence_i
  Journey_strategic        = Σ(Stage_strategic_i × Stage_effective_weight_i)
                             / Σ(Stage_effective_weight_i)
```

---

## 11.7 Tenant-Level Scoring Parameters

These parameters are stored in the `ScoringConfig` entity (Section 4.2.9), which is scoped to the tenant — one row per tenant, not per journey. All journeys within a tenant share the same scoring parameters. Parameters are configurable by the CX Program Manager (P-01) from the tenant settings surface (rendered in M-11).

> **NOTE:** Rationale: A single tenant-level configuration keeps scoring methodology consistent and comparable across all journeys within a tenant. A banking tenant whose standard is α = 0.7 applies that consistently to all journeys, making cross-journey comparisons meaningful. Per-journey overrides would undermine that comparability and are deferred to a future version if a genuine business case emerges.

> **CONSTRAINT:** α and β are always presented as a linked pair via a single slider. Direct input of β is not supported. The system derives β = 1 − α at all times.

| Parameter | Role | Valid Range | Default |
|---|---|---|---|
| `α (alpha)` | Customer importance blend weight. β is always derived as 1 − α. Applies to all journeys in the tenant. | 0.0 – 1.0 | 0.5 |
| `β (beta)` | Business importance blend weight. Always β = 1 − α. Not independently configurable. | Derived | 0.5 |
| `MOT_multiplier` | Score amplification multiplier for MoT-flagged touchpoints. Applies uniformly across all journeys in the tenant. | 1.0 – 2.0 | 1.5 |
| `n_floor` | Hard minimum response count below which a touchpoint is excluded from scoring. | Integer ≥ 1 | 5 |
| `flag_percentile` | The percentile k used in `n_flag_threshold = max(n_floor, P_k(...))`. A touchpoint whose `n_TP` falls below the k-th percentile of the journey's response distribution is flagged as `low_sample`. Default 25 (P25). Tenants with stricter confidence standards may raise to e.g. 30 or 32. | Integer 1–49 | 25 |
| `rolling_window_days` | Window for computing P_k and median of `n_TP`. Also defines the cold-start period — adaptive thresholds activate after this many days of response data. | Integer ≥ 7 | 30 |
| `KPI_weight_i` | Per-KPI weight at touchpoint level. Raw integers; engine normalises to sum = 1.0. Configured per touchpoint by P-01. | Integer 1–100; sum must = 100% | Equal weights |
| `TP_weight_i` | Per-touchpoint strategic prominence within its stage. Raw integers; engine normalises to sum = 1.0 within stage. Configured per touchpoint-in-journey by P-01. | Positive integer | Equal weights |
| `Stage_weight_i` | Per-stage weight within its journey. Raw integers; engine normalises to sum = 1.0 within journey. Configured per stage by P-01. | Positive integer | Equal weights |

---

## 11.8 Confidence Display Specifications for M-07

Every score surfaced in M-07 carries a `confidence_status` field. **Three values only** — no margin of error is displayed to the user. Score display is a single number with a confidence badge; statistical precision is an internal computation concern.

| confidence_status | Condition | UI Display Behaviour |
|---|---|---|
| `reliable` | `n_TP ≥ n_flag_threshold` AND `n_TP ≥ n_floor` | Score shown normally. Response count (n=) shown alongside score. |
| `low_sample` | `n_TP < n_flag_threshold` but `≥ n_floor` | Score shown with a low-sample visual badge. Tooltip: *"Low sample — treat with caution (n=[x])"*. |
| `insufficient` | `n_TP < n_floor` | Score displayed as `—`. Tooltip: *"Fewer than [n_floor] responses — score not computed."* |

> **NOTE:** Score display rule: users see the score value and the n= count. No margin of error, no confidence interval, no ± notation. The `confidence_status` badge is the sole reliability signal. This is a deliberate UX simplicity decision.

### Cold-Start Behaviour (no special state required)

During the first `rolling_window_days` of a journey's life, the adaptive thresholds (P_k and median) cannot yet be computed from historical data. System behaviour during this period:

- Fall back to `n_floor` as the flat confidence threshold for both flagging and `confidence_factor` computation.
- Any touchpoint with `n_TP ≥ n_floor` receives `confidence_status = 'reliable'` and `confidence_factor = 1.0`.
- Any touchpoint with `n_TP < n_floor` receives `confidence_status = 'insufficient'` and is excluded from scoring as normal.
- No special label, badge, or 'calibrating' message is shown. Users launching a new journey expect to see meaningful data approximately one month after go-live, aligning with the default 30-day window.
- Adaptive thresholds (P_k and median) activate automatically once `rolling_window_days` of response data exist. No manual trigger required.

---

# Change Log

| Version | Date | Changes |
|---|---|---|
| 0.1 | 2026-05-10 | Initial draft. |
| 0.2 | 2026-05-24 | Removed touchpoint library (journey-local only). Removed survey binding scope (to M-01). Removed scoring engine (to M-06). Renumbered features F1–F10. Added DetectionConfig entity. |
| 0.3 | 2026-06-04 | Added Section 11: Strategic Satisfaction Scoring Model (4-level formula set with confidence mechanics). Added ScoringConfig entity (per-journey at that stage). Added `tp_weight` to TouchpointInJourney. Added `stage_weight` to Stage. Updated M-06 integration contract. Added confidence display specs for M-07. Added Q11 and Q12. |
| 0.4 | 2026-06-04 | Q11 RESOLVED: ScoringConfig elevated from per-journey to per-tenant. Q12 RESOLVED: Cold-start simplified — no `early_data` state. `confidence_status` enum reduced to three values (`reliable`, `low_sample`, `insufficient`). |
| **0.5** | 2026-06-04 | Nabadat brand design applied throughout (navy/teal palette, Calibri, teal left-border H1, navy table headers, teal accent strip on cover). `flag_percentile` added as tenant-configurable parameter (default 25, range 1–49) — P25 replaced with P_k(`flag_percentile`) in all formula references and the ScoringConfig entity. `flag_percentile` added to ScoringConfig data model. Margin of error display removed entirely — score + `confidence_status` badge is the only user-facing signal. All references to `margin_of_error`, ± notation, and 'trend inconclusive / statistical noise' alert logic removed. M-07 display spec and M-06 responsibilities updated accordingly. |

---

*Confidential — NabadatCX — M-16 SRS v0.5*

*End of Chunk 1. Chunk 2 (UI/UX Wireframe Briefs) to follow upon approval of this spec.*
