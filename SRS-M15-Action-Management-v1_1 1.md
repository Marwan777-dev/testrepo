# Software Requirements Specification
## M-15: Action Management — Nabadat VOC Platform

**Version:** 1.1 | **Status:** **Final — approved for Speckit** | **Date:** 21 July 2026
**Module code:** M-15 | **Platform phase:** Phase 2
**Sources:** Stakeholder business requirements (per-page briefs, gap-resolution decisions, and the final 18-point ruling of 21 Jul 2026), approved HTML prototype `nabadat-m15-action-management-mockup.html` (v6), Nabadat Platform Definition V1, Nabdat Design System (`CLAUDE.md`, `index.css`).

**Revision history**

| Version | Date | Change |
|---|---|---|
| 1.0 | 21 Jul 2026 | First complete draft; 4 open questions, 12 assumptions |
| 1.1 | 21 Jul 2026 | All 18 outstanding points ruled by stakeholder and folded in: permissions matrix confirmed (interim); all alerting postponed to the M-09 module spec; VAL-210 confirmed; Created-by filter **removed** (no assignee in v1); SET-1 guard confirmed; **Completed actions are read-only**; **Archived is a standalone status** (exclusive of Planned/Active/Completed) while measurement continues; Archive added to SCR-03 header; evaluated-target row variant on Active actions defined; zero-eligible-target Active card fallback defined; End-Date edit warning dialog (DLG-4) added; search/filters confirmed across all four tabs; tenant-timezone day-granularity rule added; all former assumptions ratified; open-questions register closed |

> **Traceability convention:**
> - **[BR]** — explicit stakeholder business requirement (including the 21 Jul 2026 final ruling).
> - **[HTML]** — element or behaviour present in the approved HTML prototype.
> - **[Derived from UI]** — behaviour inferred from the prototype, subsequently ratified by the stakeholder.
> Conflicts were resolved in favour of explicit business requirements; every superseded decision is recorded in §10.4.

> **Consistency note (self-review):** vocabulary is normalised throughout to the final decisions — **Completed** (never Expired/Past), **More Details**, **Archive/Unarchive** (no Clone, no action-level Delete, no "Deactivate/End early"), **Archived as a standalone status**, and no references to review-cadence functionality.

---

## 1. Introduction

### 1.1 Purpose
This SRS is the single, implementation-ready source of truth for the **M-15 Action Management** module of the Nabadat Voice-of-Customer (VOC) platform. It merges the stakeholder's business requirements with every element and behaviour of the approved HTML prototype. It is written so that a build system (Speckit) or a developer with no prior VOC knowledge can implement the entire module without referring back to the HTML or the original business notes.

### 1.2 Scope

**In scope**
- Three screens: **SCR-01 All Actions** (main page), **SCR-02 Add / Edit Action**, **SCR-03 Action Details**.
- The **Action Measurement Model**: baseline capture, threshold targets, score/time progress, timer states, outcome evaluation, and lowest-performing-target selection (§3).
- Action status lifecycle — the four statuses **Planned → Active → Completed**, plus the standalone **Archived** status (§10).
- KPI Target lifecycle (Active / Deactivated / Force-deactivated) (§10.2).
- The **Settings → Actions** subsection with two tenant-level parameters (§11).
- Cross-module contracts with M-06 (KPI Engine) and M-07 (Dashboards & Reporting), the confirmed interim permissions matrix (§13), and the audit-trail data requirement (F-M15-07) (§12).

**Explicitly out of scope**
- **Clone / duplicate action** — removed by stakeholder decision. [BR]
- **Permanent deletion of an Action** — replaced by Archive. Target-level deletion remains in scope. [BR]
- **Editing a Completed action** — Completed actions are read-only (BR-023). [BR]
- **All user alerting/notifications** — postponed in full to the M-09 Notifications module spec; M-15 v1 ships only the in-app toasts and confirmation dialogs of §15. [BR]
- Linkage of actions to journeys (M-16), cases (M-14), or AI recommendations — v1 links actions to KPIs only. [BR]
- An audit-trail *viewing* UI (the data requirement is in scope; the screen is not).
- The permissions engine itself (M-10) — the interim matrix in §13 applies until M-10 refines it.
- Implementation of the M-07 trend chart — only M-15's overlay contract (§12.2).
- Global platform chrome (sidebar navigation, theme toggle) — documented for context only (§4.4); owned by the platform shell.

### 1.3 Definitions & Acronyms

| Term | Definition |
|---|---|
| **VOC (Voice of Customer)** | The structured, ongoing program of collecting, analysing, and acting on customer feedback across an organisation. |
| **KPI (Key Performance Indicator)** | A customer-experience metric managed in module M-06 (e.g., NPS, CSAT). In Nabadat, **all KPIs are standardised so that a higher score is always better** — including effort metrics (CES is phrased as "how easy was it"). [BR] |
| **NPS (Net Promoter Score)** | Loyalty metric based on "How likely are you to recommend us?" (0–10); Promoters (9–10) minus Detractors (0–6). |
| **CSAT (Customer Satisfaction Score)** | Post-interaction satisfaction rating. |
| **CES (Customer Effort Score)** | Ease-of-interaction metric; in Nabadat, phrased so higher = easier = better. |
| **FCR** | First Contact Resolution rate. |
| **VFM** | Value-for-Money perception score. |
| **CHS** | Customer Health Score. |
| **Agent Score** | Agent-performance CX metric. |
| **Action** | An improvement initiative (e.g., a training program) defined, tracked, and measured in M-15 against one or more KPI Targets. |
| **KPI Target (Target)** | A per-KPI measurement attached to an Action: the chosen KPI, a Target Date, and Lower/Upper Thresholds. One Target per KPI per Action. |
| **Baseline Score (Baseline, B)** | The KPI's score captured automatically on the **Action Start Date**. All thresholds are deltas added over this value. |
| **Current Score (C)** | The KPI's live score at the moment of viewing (supplied by M-06). |
| **Final Score** | The KPI's score on the Target Date; used for outcome evaluation. |
| **Lower Threshold (L)** | A delta (points over Baseline). Reaching Baseline + L on the Target Date = *Partially Successful*. |
| **Upper Threshold (U)** | A delta (points over Baseline), U ≥ L and U > 0. Reaching Baseline + U on the Target Date = *Successful*. |
| **Lower / Upper Threshold Point** | Absolute values: Baseline + L and Baseline + U respectively. |
| **Action Start Date** | User-entered date the action work begins; the Baseline is captured on this date. |
| **Action End Date** | User-entered date the action work completes. |
| **Target Start Date** | **System-derived, never user-entered:** Action End Date + 1 day. The monitoring clock starts here. |
| **Target Date** | Per-Target, user-entered evaluation date on which the outcome is determined. |
| **Score Progress** | (Current − Baseline) ÷ (Upper Threshold Point − Baseline). See §3.4. |
| **Time Progress** | (Current Date − Target Start Date) ÷ (Target Date − Target Start Date). See §3.4. |
| **Lowest-Performing Target** | The Action's active, still-evaluable Target with the lowest raw Score Progress (§3.6). |
| **X — Maximum Upper Threshold** | Tenant setting: the threshold slider's maximum (default **20**). §11. |
| **PAD — Slider Padding** | Tenant setting: points of track padding on zone sliders (default **3**, positive integer). §11. |
| **Archived** | A **standalone action status**, exclusive of Planned/Active/Completed. Entering it is non-destructive: measurement keeps computing normally; the action is presented view-only in the Archived tab until unarchived, at which point its status recomputes from its dates. [BR] |
| **Tenant** | One customer organisation's isolated workspace within the multi-tenant platform. |
| **Tenant timezone** | The timezone configured for the tenant in platform settings; anchors all day-boundary logic in M-15 (BR-022). |
| **Closed-Loop Management** | Following up individual feedback cases (module M-14 domain). M-15 acts *beyond* individual case resolution, at the initiative level. |

### 1.4 References
- Nabadat Platform Definition V1 — M-15 feature clusters.
- SRS-M06 KPI Engine and Settings v1.0 (Active KPI registry, live/normalised/historical scores).
- M-10 User and Role Management — Pre-Spec v0.1 (future refinement of §13).
- Approved prototype: `nabadat-m15-action-management-mockup.html` (v6, 20 Jul 2026 fixed reference date).
- Nabdat Design System: `CLAUDE.md` guidelines and `index.css` tokens (brand palette, D1–D5 semantic palette, typography, radius scale, RTL rules).

---

## 2. Overall Description

### 2.1 Product Perspective
M-15 closes the "act" stage of the VOC loop at the *initiative* level: where M-14 closes the loop on individual feedback cases, M-15 lets CX teams **define, track, and measure improvement actions triggered by CX insights beyond individual case resolution** (Platform Definition). Feature-cluster mapping:

| Cluster | Name | Covered in |
|---|---|---|
| F-M15-01 | Action definition | §7 (SCR-02), §3 |
| F-M15-02 | Action assignment | **No assignee exists in v1.** The creator is auto-captured (`created_by`) for audit attribution only; it is not exposed as a filter or a field. Role-based assignment arrives with M-10. [BR] |
| F-M15-03 | Action lifecycle | §10 |
| F-M15-04 | Source linkage | §12.1 — v1 links actions to **KPIs only** (M-06). Linkage to journeys/cases/AI recommendations: out of scope v1. [BR] |
| F-M15-05 | Outcome measurement | §3, §6–§8 visualisations |
| F-M15-07 | Action audit trail | §12.4 (data requirement) |

### 2.2 User Classes & Characteristics

| Persona | Description | Frequency | Primary goal in M-15 | Access (confirmed interim — §13) |
|---|---|---|---|---|
| CX Program Manager | Owns improvement initiatives for the tenant | Daily | Create actions, set KPI targets, monitor pace, react to red timers | Full control |
| CX Analyst | Analyses KPI movement and action effectiveness | Weekly | Review score vs. time progress, evaluate outcomes | View-only |
| Executive / Viewer | Consumes results | Monthly | Scan cards and outcomes | View-only |

### 2.3 Operating Environment
Web application (desktop-first, responsive), evergreen browsers (Chrome, Edge, Firefox, Safari — last 2 versions). Light and dark themes (navy-tinted dark, never pure black). Arabic-first production build (RTL) with full EN/AR parity; the approved prototype is the LTR English reference (§16.1).

### 2.4 Assumptions & Dependencies (module-level)
- M-06 exposes, per tenant: (a) the Active KPI registry, (b) each KPI's live current score, (c) each KPI's normalised 0–100 index, (d) historical daily scores (for baseline capture/recapture and retro-dating), and (e) KPI-deactivation/reactivation events. §12.1.
- All KPIs are higher-is-better by M-06 standardisation. [BR]
- Authentication, tenancy, the tenant-timezone platform setting, and the platform shell exist.
- All former assumptions were ratified by the stakeholder on 21 Jul 2026; the register is in §19.

---

## 3. The Action Measurement Model
*This section is normative for every calculation in the module. Stakeholder-mandated: the model must be "expressed deeply along with the whole process".* [BR]

### 3.1 Concept — narrative and worked example
A CX manager decides to run a training program for call-center agents.

- The training is planned to run **17/7 → 19/7**.
- **Action Start Date = 17/7.** On this date the system automatically snapshots each targeted KPI's score — the **Baseline**. Anchoring the baseline at the *start* (not the end) means the action is credited with its **total effect**, including any lift that happens while the work is still executing (e.g., calls already improving on day 2 of training).
- **Action End Date = 19/7.** The work is finished. This date has **no measurement role of its own**; it only derives the next anchor.
- **Target Start Date = 20/7** — always **Action End Date + 1 day, system-derived, never entered by the user**. The monitoring window opens; the **time clock starts at zero here**, not at the start date, so the countdown never runs while the team is still executing.
- **Target Date = 20/9** (chosen per KPI Target). On this date the KPI's score is compared to the thresholds and the outcome — *Successful / Partially Successful / Unsuccessful* — is determined.

This is the **two-anchor model**: the **score axis** is anchored at the Start Date (full credit for the action's effect) while the **time axis** is anchored at the Target Start Date (the countdown covers only the monitoring window). Both anchors together resolve what a single-anchor model cannot: a single anchor either under-credits the action (end-date baseline absorbs in-execution lift) or runs the clock during execution (start-date clock penalises long executions).

### 3.2 Date anchors — normative table

| # | Anchor | Entered by | Rule | Role |
|---|---|---|---|---|
| D1 | **Action Start Date** | User | Required | Work begins; **Baseline snapshot** taken for every KPI Target from M-06's score for that date |
| D2 | **Action End Date** | User | Required; ≥ D1 | Work complete; boundary only |
| D3 | **Target Start Date** | **System** | = D2 + 1 day | Clock zero for Time Progress; shown read-only on SCR-03 |
| D4 | **Target Date** | User, per Target | Required; > D2 (equivalently ≥ D3) | Outcome evaluated; per-Target timer endpoint |
| D5 | **Current Date** | System | Live | Drives Time Progress, Score Progress inputs, and status computation |

- **BR-D1. Retro-dating is allowed.** Any of D1, D2, D4 may be earlier than today, so actions can be documented after they were performed. A retro-dated action is simply *born into* whatever status its dates compute to (§10.1), with the Baseline captured from M-06's **historical** score for D1. [BR]
- **BR-D2. Ordering:** D1 ≤ D2 < D4 for every Target (D3 = D2 + 1 always holds).
- **BR-D3. Day granularity & timezone.** All date comparisons, baseline captures, phase transitions, and outcome evaluations operate at **day granularity in the tenant's configured timezone** (BR-022). [BR]

### 3.3 Baseline capture and recapture
- **BR-B1.** On (or retroactively for) the Action Start Date, the system captures **Baseline = KPI score on the Action Start Date** for each KPI Target, from M-06. The stakeholder explicitly resolved an earlier end-date-baseline phrasing in favour of the Start Date. [BR]
- **BR-B2. Recapture on edit (Planned/Active only — Completed is read-only, BR-023).** If the **Action Start Date** of an already-started action is edited, the system **automatically re-snapshots the Baseline from M-06 history** for the new date, after the user confirms warning dialog DLG-2. Editing the **Action End Date** of a started action moves the Target Start Date and recalculates Time Progress everywhere — guarded by DLG-4. Editing **thresholds** mid-monitoring is guarded by DLG-3. Every such change is written to the audit trail (§12.4). [BR]
- **BR-B3.** Before the Start Date is reached (Planned actions), no Baseline exists; SCR-02 shows the KPI's live **Current Score** in its place, labelled as the value that *will be* captured (§7.6), and sliders render without a B flag (§5.1, §6.5).

### 3.4 Formulas (normative)

**Score Progress** — canonical stakeholder form:

> **Score Progress = (Current Score − Baseline Score) ÷ (Upper Threshold Point − Baseline Score)**

where Upper Threshold Point = Baseline + U. Because thresholds are stored as deltas, this is algebraically identical to (Current − Baseline) ÷ U. Both readings are valid; the canonical form is used in all UI tooltips and documentation. [BR]

**Time Progress:**

> Passed Time = Current Date − Target Start Date
> Full Time = Target Date − Target Start Date
> **Time Progress = Passed Time ÷ Full Time**

**Timer colour mapping** (per Target; the Action card uses its Lowest-Performing Target's values):

| Condition (raw values) | Timer state | Colour token |
|---|---|---|
| Score Progress > Time Progress | **Green** — ahead of pace | `--d2` |
| Score Progress = Time Progress (equality within ±0.005) | **Yellow** — on pace | `--d3` |
| Score Progress < Time Progress | **Red** — behind pace | `--d5` |
| Monitoring complete (Completed actions / evaluated targets) | **Grey**, ring full | `--nb-stone` |
| Not started (Planned) or Target deactivated | **Empty** — icon only, no ring fill | — |

- **BR-F1. Execution-phase rule.** While D1 ≤ Current Date ≤ D2 (work still executing), Time Progress raw = **0** by definition (the clock has not started). The ring renders empty-fill; colour follows the table: any positive lift → Green, no movement → Yellow, regression → Red. [BR — ratified]
- **BR-F2. Clamp what is drawn, never what is computed.** Raw Score/Time Progress values drive all logic (colour, ranking); **displayed** values clamp to 0–100 % (ring fill, percentage labels). A regressing KPI (raw negative) therefore always shows Red; an early overshoot (raw > 100 %) shows a full ring and Green. [BR]
- **BR-F3. Division guard.** Saving a Target with U = 0 is blocked by VAL-210 (**stakeholder-confirmed**), keeping the Score Progress denominator non-zero. [BR]

### 3.5 Outcome evaluation (on each Target Date)

| Rule | Condition (Final Score on Target Date) | Outcome | Colour |
|---|---|---|---|
| BR-O1 | Final ≥ Baseline + U | **Successful** | `--d2` dot / text |
| BR-O2 | Baseline + L ≤ Final < Baseline + U | **Partially Successful** | `--d3` |
| BR-O3 | Final < Baseline + L | **Unsuccessful** | `--d5` |

- **BR-O4.** U = L is a **valid saved state** (equality allowed): the Partially-Successful band collapses and the outcome is binary Successful/Unsuccessful. [BR]
- **BR-O5.** Deactivated Targets are **never evaluated** and produce no results (§10.2).
- **BR-O6.** Outcomes are computed from stored data (Baseline, L, U, Final Score), not stored as hardcoded labels. Because Completed actions are read-only (BR-023) and evaluated Targets can no longer be re-parameterised, an evaluated outcome is **effectively immutable**; re-derivation can only occur through pre-completion edits guarded by DLG-2/3/4. [BR]

### 3.6 Lowest-Performing Target — selection and normalisation
*Stakeholder-confirmed on condition of a thorough in-depth explanation.* [BR]

**Definition.** For an Active action, the Lowest-Performing Target is the Target with the **lowest raw (unclamped) Score Progress** among eligible Targets.

**Why raw Score Progress is the right normaliser.**
1. **It is dimensionless.** Score Progress divides a score delta by a score delta of the same KPI, cancelling the unit and the scale. 40 % progress on an NPS target (say +2.4 of +6 needed) is directly comparable to 40 % on a CSAT target (+2 of +5 needed), even though NPS spans −100…+100 and CSAT 0…100. Comparing raw scores (73 vs 84) or raw deltas (+4 vs +6) across KPIs would be meaningless; comparing *fractions of each target's own ambition* is not.
2. **It respects each target's ambition.** A target asking for +10 points is harder than one asking for +2; dividing by the Upper Threshold Point − Baseline bakes that difficulty in.
3. **It must be the raw value, not the display-clamped one.** A KPI that regressed below its Baseline has *negative* raw progress. Clamping before ranking would tie it at 0 % with a merely-stagnant KPI; using the raw value guarantees the regressing target ranks worst — which is exactly the target that deserves the card's spotlight.

**Eligibility.** A Target is eligible if and only if: it is active (not deactivated, §10.2), its Baseline exists, and its Target Date ≥ Current Date (still evaluable — already-evaluated Targets are excluded).

**Tie-breaks (in order):** (1) least remaining time (earliest Target Date); (2) KPI name, ascending alphabetical — a deterministic final tie-break so the featured target never flickers between renders. [BR — ratified]

**Planned actions** have no Baselines, so Score Progress is undefined. Fallback: feature the Target whose KPI has the **lowest current score on M-06's normalised 0–100 index** (normalisation makes cross-KPI comparison valid). The card labels this "Lowest current score" rather than "Lowest performing target". [BR]

**Zero eligible Targets** (all deactivated, or all already evaluated while a later — deactivated-target or none — date keeps the action Active): the featured slot is replaced per FR-111. [BR]

**Uses.** The Lowest-Performing Target drives: the Active card's featured KPI, slider, timer, and meta line (§6.4); and the highlighted row + "Lowest performing" badge on SCR-03 (§8.4).

### 3.7 Adaptive slider padding (zone sliders)
For every stepped zone slider (§5.1), with anchor A (= Baseline; for Planned renders, **provisionally the Current Score — stakeholder-confirmed**):

> **Track minimum = min(A, Current) − PAD**
> **Track maximum = max(A + U, Current) + PAD**

- If Current < Baseline (regression), the red zone extends PAD points **below the Current Score**, so the C flag stays on the track. [BR]
- If Current > Upper Threshold Point (overshoot), the green zone extends PAD points **above the Current Score**. [BR]
- PAD default = 3 (positive integer), tenant-configurable in Settings → Actions (§11). This rule supersedes the earlier "pin C to the track edge" behaviour — the track adapts and the C flag never pins. [BR]
- Zone boundaries are unchanged by padding: Red [min → Baseline+L], Yellow [Baseline+L → Baseline+U], Green [Baseline+U → max], **hard edges, no blending**. [BR]

### 3.8 Action phase timeline (structured text; no diagram)
1. **Planned** — Current Date < Action Start Date. No Baseline. Timers empty. Card/detail render C-only sliders.
2. **Execution** — Start ≤ Current ≤ End. Baseline captured at phase entry. Time Progress = 0 (BR-F1); ring empty; colour by lift.
3. **Monitoring** — Target Start ≤ Current ≤ latest Target Date. Ring fills with Time Progress; colour by Score vs Time. Individual Targets whose own Target Date passes are **evaluated** (BR-O1–O3) and thereafter render as evaluated rows (§8.4) while later Targets keep running.
4. **Completed** — Current Date > latest Target Date (all Targets evaluated). Grey full timers; outcomes displayed; the action becomes **read-only** (BR-023).
The standalone **Archived** status can be entered from any of the above and exited back to the date-computed status (§10.3); measurement computation never pauses while Archived.

---

## 4. Navigation Overview

### 4.1 Screen hierarchy & routes  [Derived from UI]
```
Actions (module root)
├─ SCR-01  All Actions ............... /actions
├─ SCR-02  Add Action ................ /actions/new
│          Edit Action ............... /actions/:id/edit   (same screen, pre-filled; Planned/Active, non-archived only)
└─ SCR-03  Action Details ............ /actions/:id
```
SCR-03 is a **full page route, never a dialog** (design-system rule). Deep links to all four routes must resolve directly. A deep link to `/actions/:id/edit` for a Completed or Archived action redirects to `/actions/:id` (SCR-03) with an explanatory toast (NTF-6). [Derived from UI — ratified via BR-023]

### 4.2 Breadcrumb  [HTML]
Topbar breadcrumb: `Actions / {All Actions | Add Action | Action Details}` — the second segment updates per screen; "Actions" is static module context.

### 4.3 Entry / exit points

| From | Trigger | To |
|---|---|---|
| SCR-01 | **Add Action** button | SCR-02 (blank) |
| SCR-01 Active/Completed/Archived card | **More Details** (primary) | SCR-03 |
| SCR-01 Planned card | **Edit** (primary) | SCR-02 (pre-filled) |
| SCR-01 Planned card kebab | **More Details (preview)** | SCR-03 (planned rendering) |
| SCR-01 Active card kebab | **Edit** | SCR-02 (pre-filled) |
| SCR-02 | **← Back to Actions** / **Cancel** / successful **Save action** | SCR-01 |
| SCR-03 | **← Back to Actions** | SCR-01 |
| SCR-03 header | **Edit** (Planned/Active, non-archived) | SCR-02 (pre-filled) |
| SCR-03 header | **Archive** (non-archived) | stays on SCR-03, refreshed as Archived |
| SCR-03 header | **Unarchive** (Archived) | stays on SCR-03, refreshed |

Back navigation is via the explicit "← Back to Actions" link on SCR-02/SCR-03 [HTML]; browser back must behave identically [Derived from UI].

### 4.4 Platform chrome (context only — out of module scope)
Sidebar (Dashboard, Journeys, Surveys, Cases, **Actions** ← active, Settings) with the Nabadat wordmark and Mint→Cyan pulse dot; topbar with breadcrumb and a light/dark theme toggle. [HTML] These are owned by the platform shell; M-15 only requires that "Actions" is highlighted while any M-15 route is open.

---

## 5. Shared UI Components
*Specified once here; the screen sections reference these definitions.*

### 5.1 Stepped Zone Slider (cards & SCR-03 rows)  [BR + HTML]
Purpose: visualise one KPI Target's position against its threshold zones.

**Anatomy (top → bottom):**
1. *(Reference variant only — SCR-03)* **L / U reference flags**: coloured bold text `L {LowerPoint}` (red `--d5`) and `U {UpperPoint}` (green `--d2`) with a downward triangle beneath the text, positioned at their absolute points, **above the tick numbers**, font ≈ 12 px. Purely informational; no effect on calculations; tooltips: "Lower threshold point {v} (baseline + {L})" / "Upper threshold point {v} (baseline + {U})". [BR + HTML]
2. **Tick numbers**: integer scale values across the track span, sitting **close above the track** (≈ 6 px gap); step 1 (span ≤ 16), 2 (span ≤ 32), else 4; the ticks equal to round(Baseline) and round(Current) render **bold** and slightly larger. [BR + HTML]
3. **Track**: 14 px tall, fully rounded (`border-radius: 999px`), width 100 %. Background: **hard-edged** zones — Red `--d5` [min → Baseline+L], Yellow `--d3` [Baseline+L → Baseline+U], Green `--d2` [Baseline+U → max]. No gradient blending. Track bounds per §3.7.
4. **B and C markers** on the track: vertical bars ≈ 5 × 22 px, rounded. **B** = solid navy bar (light border in dark mode); **C** = card-coloured bar with 2 px navy border. Tooltips: "Baseline {v} — captured on Action Start Date" / "Current {v}".
5. **B / C letter labels** below the track at the same positions, bold ≈ 11.5 px.

**Variants:** *Planned* — no B marker/label; anchor A = Current Score provisionally (**confirmed**); tick bolding applies to Current only. *Reference (SCR-03)* — adds row 1. *Card default (SCR-01)* — rows 2–5 only.
**States:** static (no direct interaction); values update on data refresh. Accessible as `role="img"` with an `aria-label` naming the KPI, baseline (if any), current value, and zone bounds. [HTML]

### 5.2 Threshold Slider (SCR-02, per KPI Target)  [BR + HTML]
Purpose: set Lower/Upper Thresholds (deltas, scale 0 → X) with two-way binding to the numeric fields.

**Anatomy (top → bottom):** two **draggable flags** above; **tick numbers** (0…X step 2) close above the track; 14 px fully-rounded **track**. Each flag = bold coloured text label, downward triangle (9 px), and a 7 × 20 px white-bordered **stem handle** overlapping the track. Lower flag red `--d5`; Upper flag green `--d2` — same anatomy family and curvature as §5.1. [BR]

**States & behaviour:**
- **Default (L = 0 and U = 0):** track is **plain grey** (`--gs-grey` mix token); flags show **text-only labels** "Lower Threshold" / "Upper Threshold" at illustrative 24 % / 76 % positions. [BR + HTML]
- **Set (either value ≠ 0):** flags show values "L +{v}" / "U +{v}" positioned at v ⁄ X; track shows **hard-edged** zones recomputed on every change: Red [0 → L], Yellow [L → U], Green [U → X]. [BR — supersedes both earlier gradient treatments; §10.4]
- **Two-way binding:** typing in the Lower/Upper fields moves the flags and re-zones the track; dragging a flag updates the fields live. [BR]
- **Auto-sync rule:** from the moment L first leaves 0 (typed **or dragged**), U mirrors L until the user sets U independently; thereafter U is "touched" and L is clamped to ≤ U. [BR]
- **Constraint:** 0 ≤ L ≤ U ≤ X, equality allowed; U > 0 to save (VAL-210); decimals to one decimal place (inputs step 0.5; drag rounds to 0.1). [BR]
- **Keyboard:** each flag is focusable (`role="slider"` with `aria-valuemin/max/now`); ←/→ adjust by 0.5. [HTML]
- **Pointer:** `pointerdown` on a flag begins a drag; value = pointer x mapped 0→X, clamped; drag ends on `pointerup`. [HTML]
- **Disabled:** entire slider inert and faded when its Target is deactivated (§7.7).

### 5.3 Timer Ring  [BR + HTML]
44 × 44 px component: background ring (muted token), progress arc (radius 15.5, stroke 4.5, rounded caps, drawn from 12 o'clock clockwise) whose **fill = displayed Time Progress**, and a centred 17 px stopwatch icon. Ring **starts empty and fills as time passes**. Colour state per §3.4 table; icon inherits the state colour. Tooltip: "Time {t}% of monitoring window elapsed · Score progress {s}%" (Active); "Not started — monitoring begins the day after the Action End Date" (Planned); "Monitoring complete — all target dates have passed" (Completed); "Monitoring complete" (evaluated Target on an Active action); "Deactivated — excluded from results" (deactivated Target). All percentages are display-clamped (BR-F2).

### 5.4 Badges, chips & labels
| Element | Appearance | Meaning / copy | Source |
|---|---|---|---|
| **Status badge** | Pill with 13 px circle icon. Active: `--d2-light` bg / `--d2-dark` text; Planned: navy-100 bg / navy text; Completed: muted bg / muted text; **Archived: dashed-border pill, muted text** (dark-mode variants per tokens) | "Active" / "Planned" / "Completed" / "Archived" — exactly **one** status badge is shown at a time; Archived is standalone and never co-displayed with another status | [BR + HTML] |
| **Archived card badge** | Dashed-border pill, muted text, beside the card title | Marks cards in the Archived tab | [HTML] |
| **KPI chip** | Accent bg, border, radius-sm, bold 12.5 px | KPI name (featured target / row identity) | [HTML] |
| **KPI mini labels** | Row prefixed "Targets:"; tiny bordered chips per KPI; deactivated ones **struck through at 60 % opacity**, tooltip "Deactivated — excluded from results"; active ones tooltip "Active target" | Every KPI on the action, on Active & Planned cards | [BR + HTML] |
| **"Lowest performing" badge** | Cyan-100 bg / cyan-700 text pill (brand emphasis, not a status colour) | Marks the featured target row on SCR-03 | [HTML] |
| **"Lowest performing target" / "Lowest current score" label** | Dashed-border muted pill next to the featured KPI chip on cards | Active / Planned wording respectively | [HTML] |
| **"Deactivated" badge** | Bordered muted pill | On deactivated target rows (SCR-03) | [HTML] |
| **Outcome label** | Dot (`--d2/--d3/--d5`) + bold coloured text "Successful" / "Partially successful" / "Unsuccessful"; tooltip carries the full sentence (§8.5) | Evaluated target rows (Completed actions and evaluated Targets on Active actions) | [BR + HTML] |
| **Outcome chip** | Bordered chip: coloured dot + KPI name; tooltip = full outcome sentence | Completed **cards** — one per KPI | [BR + HTML] |
| **Pace text** | Bold coloured inline text: "ahead of pace" (`--d2-dark`), "on pace" (`--d3-dark`), "behind pace" (`--d5`) | Active card meta line | [HTML] |

### 5.5 Kebab menu, tabs, search, toast
- **Kebab (⋮)**: 34 px icon button per card; opens a 190 px dropdown of text actions; one open at a time; outside-click closes; items per tab in §6.7. `aria-haspopup="menu"`. [HTML]
- **Tabs**: underline style with per-tab **count pills**; active tab cyan underline + tinted count. [HTML]
- **Search input**: leading magnifier icon, placeholder "Search actions across all tabs…". [HTML]
- **Toast**: single bottom-centred pill, ~2.6 s auto-dismiss, `role="status"`; exact copies in §15.1. [HTML]
- **Buttons**: primary (cyan, one per screen region), outline, ghost; disabled at 45 % opacity, not-allowed cursor. [HTML]

---

## 6. Functional Requirements — SCR-01: All Actions (main page)

### 6.1 Purpose, actors, objective
The module's landing page. Lists every Action of the tenant in four status tabs, surfaces each Active action's **Lowest-Performing Target** so attention lands where it is needed first, and is the sole entry point to SCR-02 and SCR-03. Actors: all module users per §13 (write actions gated to Program Managers). Business objective: at-a-glance pace awareness — a manager should identify behind-pace initiatives (red timers) within seconds.

### 6.2 Layout  [HTML]
- **Page header:** H1 "Actions"; subtitle "Improvement initiatives measured against KPI targets. Each card tracks its lowest-performing target — the one that needs attention first."; **Add Action** primary button (plus icon) at the inline-end.
- **Toolbar:** search field; KPI filter; date-range filter; full-width match-hint line (conditional). **There is no Created-by filter and no Status filter** (both removed by stakeholder decision). [BR]
- **Tab bar:** Active · Planned · Completed · Archived, each with a live count pill.
- **Card grid:** responsive `auto-fill minmax(430px, 1fr)`, 18 px gap; single column below 940 px.

### 6.3 FR list — page level

| ID | Requirement | Source |
|---|---|---|
| FR-101 | The page SHALL present exactly four tabs — **Active, Planned, Completed, Archived** — each showing only the actions belonging to it (grouping rules FR-102/103). Switching tabs swaps the visible grid; no cross-tab bleed-through. | [BR + HTML] |
| FR-102 | For non-archived actions, tab/status grouping is **computed from dates**, never stored: Planned = Action Start Date > Current Date; Completed = latest Target Date < Current Date; otherwise Active (day granularity, tenant timezone — BR-022). | [BR] |
| FR-103 | An action in the **Archived** status appears **only** in the Archived tab. Archived is a standalone status, exclusive of Planned/Active/Completed (§10.3); measurement computation continues while Archived. | [BR] |
| FR-104 | Each tab label SHALL show a count pill of the actions currently in that tab. Counts update immediately after archive/unarchive and after status transitions. | [HTML] |
| FR-105 | An Active action SHALL move to Completed automatically the moment its **latest** Target Date passes (no user step, no page reload dependency beyond next render), becoming read-only (BR-023). | [BR] |
| FR-106 | **Search** filters cards by case-insensitive substring match on Action Name, applied **across all four tabs simultaneously** (stakeholder-confirmed to include Archived). While a query is active, a hint line shows "**{n} match{es} across all tabs — switch tabs to see them all**"; per-tab counts remain the unfiltered totals. Clearing the query restores all cards. | [BR + HTML] |
| FR-107 | **Filters** apply across all four tabs, combined with search by AND: **KPI** (multi-select of the tenant's KPIs — prototype rendered single-select as a simplification; multi-select is the requirement) and **Date range** (from–to date pickers over the Action Start Date; the prototype showed "from" only — the "to" field is required). There is **no Status filter** (tabs carry that role) and **no Created-by filter** (removed with the no-assignee decision). | [BR] |
| FR-108 | **Empty state** per tab: a full-width card reading "No {active/planned/completed/archived} actions." For the three status tabs, append guidance "Create one with **Add Action**." (design-system guided empty state). | [HTML + Derived from UI] |
| FR-109 | **Loading state:** card grid renders skeleton cards (design-system rule) until data resolves. | [Derived from UI / design system] |
| FR-110 | Default landing tab = **Active**; card ordering within a tab = **newest-created first**; lists beyond one viewport paginate / infinite-scroll. | [BR — ratified] |
| FR-111 | **Zero-eligible-targets fallback (Active card).** If an Active action has no eligible Target (§3.6 — all deactivated and/or all already evaluated), its card SHALL show: the name, the KPI mini labels, the text "**No active targets to feature**" in place of the featured KPI row + slider, and a timer computed against the **latest remaining Target Date** (grey full ring if none remains). Footer and kebab unchanged. | [BR] |

### 6.4 Active card — content spec  [BR + HTML]
Per Active action:

| # | Element | Spec |
|---|---|---|
| 1 | Action Name | H3, 15.5 px semibold |
| 2 | **Timer Ring** | §5.3, fed by the Lowest-Performing Target: fill = its display Time Progress; colour = its pace state (FR-111 fallback when none) |
| 3 | Kebab | Items per §6.7 |
| 4 | Featured row | KPI chip of the Lowest-Performing Target + dashed label "Lowest performing target" |
| 5 | **Stepped Zone Slider** | §5.1 card variant for that same Target |
| 6 | **KPI mini labels** | "Targets:" + one mini chip per KPI on the action; deactivated struck through (§5.4) |
| 7 | Meta line | "Target date {d MMM yyyy}" · "Score {s}% · Time {t}% — {ahead of pace / on pace / behind pace}" (display-clamped values; pace per §3.4) |
| 8 | Footer | **More Details** primary button → SCR-03 |

### 6.5 Planned card — differences  [BR + HTML]
- Featured Target = KPI with the **lowest current score** (M-06 normalised — §3.6); label reads "**Lowest current score**".
- Slider renders **without a Baseline flag** (not configured yet) — Current flag only; zones provisionally anchored at Current (stakeholder-confirmed).
- Timer: **icon with no progress ring** (empty state) — no time has passed.
- Meta line: "Starts {date} · baseline will be captured on the start date".
- Footer primary = **Edit** → SCR-02 pre-filled. Includes KPI mini labels row.

### 6.6 Completed card — differences  [BR + HTML]
- **No slider.** All assigned KPIs render as **outcome chips**: coloured dot signalling each KPI's outcome on its Target Date (Successful green / Partially Successful amber / Unsuccessful red), tooltip = full outcome sentence (§8.5).
- Timer: **full ring, grey** — all time has passed. Tooltip "Monitoring complete — all target dates have passed".
- Meta line: "Evaluated on each target date · latest {latest Target Date}".
- Footer primary = **More Details**. No Edit anywhere (BR-023).

### 6.6b Archived card  [BR + HTML]
Renders with the **layout of its underlying date-computed shape** (Active-style with live timer/slider, Planned-style, or Completed-style — measurement continues), plus the Archived card badge beside the title. Primary = **More Details**; kebab = **Unarchive** only. View-only: no Edit.

### 6.7 Card actions matrix (footer primary + kebab)  [BR]

| Tab | Primary | Kebab items |
|---|---|---|
| Active | More Details | Edit · Archive |
| Planned | Edit | More Details (preview) · Archive |
| Completed | More Details | Archive |
| Archived | More Details | Unarchive |

**Removed by stakeholder decision (must NOT appear): Clone (all tabs), Delete (action level), Status filter, Created-by filter, any "Deactivate/End early" action, any Edit on Completed or Archived actions.** [BR]

### 6.8 Buttons — SCR-01

| Button | Location | Visibility | Action | Success | Failure |
|---|---|---|---|---|---|
| Add Action | Page header (primary, plus icon) | Program Manager (§13) | Navigate to SCR-02 blank | SCR-02 opens with one empty Target | — |
| More Details | Card footer / Planned kebab | Per §6.7 | Navigate to SCR-03 for that action | SCR-03 renders | Action not found → ERR-6 |
| Edit | Planned footer / Active kebab | Per §6.7; Program Manager | Navigate to SCR-02 pre-filled | Form loaded with the action's values | — |
| Archive | Kebab (Active/Planned/Completed) | Non-archived; Program Manager | Enter Archived status; no confirmation (non-destructive); write audit event | Card moves to Archived tab instantly; toast NTF-4; counts update | ERR-7 |
| Unarchive | Kebab (Archived) | Archived only; Program Manager | Exit Archived; status recomputes from dates; audit event | Card returns to its date-computed tab; toast NTF-5 | ERR-7 |
| Kebab (⋮) | Card top-right | Always | Toggle menu | Menu opens; outside click / item click closes | — |

### 6.9 Workflows — SCR-01
- **W-1 Monitor pace (happy path):** user opens /actions → Active tab default → scans timers → red timer → clicks More Details → SCR-03.
- **W-2 Search:** type query → cards filter live in all four tabs → hint shows total matches → clear → restore.
- **W-3 Archive:** kebab → Archive → action enters the standalone Archived status, audit logged, card relocates, toast NTF-4. The action **keeps computing normally** (timers keep running in the Archived tab). [BR]
- **W-4 Unarchive:** Archived tab → kebab → Unarchive → status recomputes from dates; the action returns to that tab with original dates, editable again if Planned/Active; toast NTF-5. [BR]
- **W-5 Automatic completion:** clock passes latest Target Date (tenant timezone) → next render shows the action in Completed with outcome chips (FR-105), read-only.

### 6.10 Acceptance criteria — SCR-01
- **AC-1.1** GIVEN 9 actions of which 2 compute Active, 3 Planned, 3 Completed and 1 is Archived, WHEN SCR-01 renders, THEN the tab counts read 2/3/3/1 and each tab shows only its own cards.
- **AC-1.2** GIVEN an Active action whose Lowest-Performing Target has raw Score 66.7 % and raw Time 84.2 %, WHEN its card renders, THEN the timer is red, the ring fill is 84 %, and the meta reads "Score 67% · Time 84% — behind pace".
- **AC-1.3** GIVEN an Active action whose latest Target Date was yesterday (tenant timezone), WHEN SCR-01 renders today, THEN the action appears in Completed with one outcome chip per KPI, a full grey timer, and no Edit affordance anywhere.
- **AC-1.4** GIVEN the query "training", WHEN typed in search, THEN only name-matching cards remain visible in every tab — including Archived — and the hint states the cross-tab match count.
- **AC-1.5** GIVEN an Active action, WHEN Archive is clicked, THEN it appears only in the Archived tab with the single status "Archived", its timer continues to reflect live Score/Time Progress, and an audit event exists.
- **AC-1.6** GIVEN an archived action whose dates compute Planned, WHEN Unarchive is clicked, THEN it reappears in Planned (dates unchanged) and its primary button is Edit.
- **AC-1.7** GIVEN an action with a deactivated Target, WHEN its card renders, THEN the mini-labels row lists that KPI struck through and the featured target is never the deactivated one.
- **AC-1.8** GIVEN an Active action whose only Targets are all deactivated, WHEN its card renders, THEN it shows "No active targets to feature" with no slider, the mini labels struck through, and a timer per FR-111.

---

## 7. Functional Requirements — SCR-02: Add / Edit Action

### 7.1 Purpose, entry, exit
Create a new Action or edit an existing one. **Edit mode is the same screen pre-filled** — "it's gonna look more like the Add Action view page". **Editing is available only for Planned and Active, non-archived actions; Completed actions are read-only (BR-023) and Archived actions must be unarchived first.** [BR] Entry: Add Action button (blank) or any Edit control (pre-filled). Exit: ← Back to Actions, Cancel, or successful Save → SCR-01.

### 7.2 Layout  [HTML]
Back link "← Back to Actions" · H1 "**Add Action**" / "**Edit Action**" · subtitle: "Define the initiative, then set a measurable target per KPI. The baseline score is captured automatically on the Action Start Date; monitoring begins the day after the Action End Date. Dates may be in the past to document an action retrospectively."
Panel 1 "**Action details**" (sub: "Name and dates drive the measurement timeline — see the derived Target Start Date on the details page.") — first grid row = **Action Name (wide, ≈2.1fr) · Action Start Date · Action End Date side by side**; Description full-width below with counter. [BR]
Panel 2 "**KPI Targets**" with header-right button **Add KPI Target** (sub: "Thresholds are points added over the baseline. Reaching the upper threshold on the Target Date means Successful; the lower one, Partially Successful. At least one active target is required.") — repeatable Target subsections.
Footer: **Cancel** (ghost) · **Save action** (primary).

### 7.3 Action-level fields

| Attribute | Action Name | Action Start Date | Action End Date | Description |
|---|---|---|---|---|
| Field name | `action_name` | `action_start_date` | `action_end_date` | `description` |
| Label | Action Name * | Action Start Date * | Action End Date * | Description |
| Type | text | date | date | multiline plain text |
| Required | Yes | Yes | Yes | No |
| Editable | Planned/Active, non-archived | idem — triggers DLG-2 if a Baseline exists | idem — triggers DLG-4 on started actions (moves Target Start) | idem |
| Default | empty | empty | empty | empty |
| Placeholder | "e.g. Training of Call Center Agents" | — | — | "What is this action, who runs it, and what should it improve?" |
| Max length | 120 | — | — | 500, with live counter "{n}/500" |
| Validation | VAL-201, VAL-202 | VAL-203 | VAL-203, VAL-204 | VAL-205 |
| Tooltip | — | "Baseline score is captured on this date" | "Monitoring starts the day after this date" | — |
| Past dates | — | Allowed (BR-D1) | Allowed | — |
| Source | [BR + HTML] | [BR + HTML] | [BR + HTML] | [BR + HTML] |

### 7.4 KPI Target subsection — structure  [BR + HTML]
Header row: title "**Target {n}**" (1-based, renumbered on delete) · spacer · **Delete** ghost button (visible only while deactivated) · **Active/Activate toggle switch** (green when on; label text "Active" ↔ "Activate").
Body grid: **KPI** select · **Target Date** · **Lower Threshold** · **Upper Threshold** · full-width slider block = **score label** (§7.6) + **Threshold Slider** (§5.2) + scale note "Scale 0–{X} · maximum configured in Settings → Actions · decimals allowed · drag the flags or type the values".

### 7.5 Target-level fields

| Attribute | KPI | Target Date | Lower Threshold | Upper Threshold |
|---|---|---|---|---|
| Field name | `kpi_id` | `target_date` | `lower_threshold` | `upper_threshold` |
| Type | select | date | number (step 0.5, decimals, 1 dp) | number (step 0.5, decimals, 1 dp) |
| Required | Yes | Yes | Yes (0 permitted) | Yes (**> 0 — VAL-210, stakeholder-confirmed**) |
| Placeholder / first option | "**Select**" (empty value) | — | — | — |
| Allowed values | The tenant's **Active KPIs from M-06**; options already chosen in *other* Targets of this action are **disabled** (one Target per KPI per action, live cross-refresh on every selection) | Any date, > Action End Date; past allowed | 0 ≤ L ≤ U | L ≤ U ≤ X |
| Default | empty | empty | 0 | 0 |
| Label hint | — | — | "— points over baseline" | "— points over baseline" |
| Dependencies | Drives the score label (§7.6) and cross-target exclusion | VAL-206 vs Action End Date | Auto-sync with U (§5.2) | Touch state ends auto-sync |
| Editable | While the parent action is editable (§7.1) and the Target is not deactivated (§7.7) | idem | idem | idem |
| Source | [BR + HTML] | [BR + HTML] | [BR + HTML] | [BR + HTML] |

### 7.6 Current-score / Baseline label  [BR + HTML]
Rendered **above the slider**, hidden until a KPI is selected; reacts live to both the KPI select and the Action Start Date field:
- Start Date empty or > today → "**Current Score · {live score from M-06}**" + note "**Captured on the action start date as the baseline score**". The value keeps updating with M-06's live score for as long as the Start Date has not been reached.
- Start Date ≤ today → label flips to "**Baseline · {score}**" + the same note; in edit mode of a started action this is the **stored Baseline**; in retro-dated creation it is M-06's historical score for that date.

### 7.7 Target activate / deactivate / delete  [BR + HTML]
- Toggling the switch **off** deactivates the Target: the whole subsection body renders **faded read-only** (≈50 % opacity, greyscale, inputs inert); only the **Activate** switch and the now-visible **Delete** button remain operable.
- Toggling back on restores editability. Manual deactivation and force-deactivation (§10.2) both expose Delete. [BR]
- **Delete** removes the Target after confirmation dialog DLG-1; remaining Targets renumber; its KPI returns to the other selects' available options; toast NTF-3.
- Deactivated Targets: excluded from results, outcome evaluation, and lowest-performing selection; still saved with the action. [BR]

### 7.8 Validation catalogue — SCR-02 (exact messages)

| ID | Rule | Error message | Source |
|---|---|---|---|
| VAL-201 | Action Name required (non-blank after trim) | "Action Name is required" | [BR + HTML] |
| VAL-202 | Action Name unique per tenant, case-insensitive, **across all statuses including Archived** | "An action with this name already exists" | [BR — ratified] |
| VAL-203 | Start and End dates required | "Action Start Date is required" / "Action End Date is required" | [BR] |
| VAL-204 | End ≥ Start | "Action End Date must be on or after the Action Start Date" | [BR-D2] |
| VAL-205 | Description ≤ 500 chars, plain text (hard cap; counter shown) | (input-limited; no submit error) | [BR — ratified] |
| VAL-206 | Every Target Date > Action End Date | "Target Date must be after the Action End Date" | [BR-D2] |
| VAL-207 | ≥ 1 **active** Target with a KPI selected | "At least one active KPI target is required" | [BR + HTML] |
| VAL-208 | KPI required per active Target | "Select a KPI for Target {n}" | [Derived from UI] |
| VAL-209 | 0 ≤ L ≤ U ≤ X, ≤ 1 decimal place | (prevented by the control; typed overshoot clamps) | [BR] |
| VAL-210 | U > 0 on every active Target (division guard, BR-F3) | "Upper Threshold must be greater than zero" | [BR — confirmed] |
| VAL-211 | One Target per KPI per action | (prevented — options disabled) | [BR] |

Validation errors surface as toasts on Save (prototype behaviour) **and** inline at the offending field (design-system requirement); focus moves to the first invalid field. [HTML + Derived from UI]

### 7.9 Edit-mode specifics  [BR]
- Available only for **Planned and Active, non-archived** actions (BR-023). Deep links to edit for Completed/Archived actions redirect per §4.1 with toast NTF-6.
- All fields pre-filled, including deactivated Targets (rendered in their faded state, `edited` slider mode with values/zones shown).
- Guarded edits on started actions: **Start Date → DLG-2** (baselines re-snapshot from M-06 history for the new date; all progress recomputes); **End Date → DLG-4** (Target Start moves; all Time Progress recomputes); **thresholds mid-monitoring → DLG-3**. Cancel in any dialog reverts the field. All confirmed edits are audit-logged field-level. [BR-B2]
- Editing may change the computed status (retro-dating in either direction); the action simply re-groups on SCR-01 (FR-102). An edit whose dates would compute **Completed** is legal (the action is *born* Completed on save and becomes read-only thereafter).

### 7.10 Buttons — SCR-02

| Button | Location | Action | Success | Failure |
|---|---|---|---|---|
| Add KPI Target | Panel 2 header (outline, plus icon) | Append a blank Target subsection | New "Target {n}" appears; **disabled** when every tenant KPI is already used | — |
| Delete (target) | Target header, deactivated only | Open DLG-1 → remove Target | Renumber; KPI freed; toast NTF-3 | — |
| Active/Activate switch | Target header | Toggle Target active state | §7.7 visual state | — |
| Cancel | Footer (ghost) | Discard changes, navigate SCR-01 | — | — |
| Save action | Footer (primary) | Run VAL-201…211; persist; audit event (create or field-level edit) | Toast NTF-2; navigate SCR-01; action grouped per FR-102 | First error toast + inline; stay on page |
| ← Back to Actions | Top | Same as Cancel | — | — |

### 7.11 Workflows — SCR-02
- **W-6 Create (happy path):** Add Action → name/dates/description → select KPI (score label appears) → set Target Date → type L (U mirrors) → drag U right → zones re-colour → Add KPI Target for a second KPI (first KPI disabled in its select) → Save → toast → SCR-01.
- **W-7 Retro-dated create:** dates set in the past → Baseline pulled from M-06 history → on save the action is born Active or Completed per FR-102 (if Completed, immediately read-only). [BR-D1]
- **W-8 Edit an Active action's dates:** Edit → change Start → DLG-2 → confirm → re-baseline + recompute → (optionally change End → DLG-4) → Save.
- **W-9 Deactivate & delete a target:** switch off → subsection fades, Delete appears → Delete → DLG-1 → confirm → removed.

### 7.12 Acceptance criteria — SCR-02
- **AC-2.1** GIVEN a blank form, WHEN Save is clicked, THEN toast "Action Name is required" and focus moves to the name field; nothing persists.
- **AC-2.2** GIVEN L = 0 and U = 0, WHEN the slider renders, THEN the track is plain grey and the flags read text-only "Lower Threshold"/"Upper Threshold".
- **AC-2.3** GIVEN U untouched, WHEN L is dragged from 0 to 4.5, THEN U becomes 4.5, both flags read "L +4.5"/"U +4.5", and the track shows hard red [0–4.5] and green [4.5–X] with no yellow band.
- **AC-2.4** GIVEN U was set to 6, WHEN L is dragged toward 8, THEN L stops at 6 (clamped ≤ U).
- **AC-2.5** GIVEN KPI "NPS" chosen in Target 1, WHEN Target 2's KPI select opens, THEN "NPS" is disabled there; deleting Target 1 re-enables it.
- **AC-2.6** GIVEN a KPI is selected and Start Date is empty, WHEN the label renders, THEN it reads "Current Score · {v} — Captured on the action start date as the baseline score"; setting Start Date to yesterday flips it to "Baseline · {v}".
- **AC-2.7** GIVEN an Active action, WHEN its Start Date is changed and DLG-2 confirmed, THEN Baselines re-snapshot for the new date, Score Progress everywhere recomputes, and an audit event records old/new values.
- **AC-2.8** GIVEN a Target Date equal to the Action End Date, WHEN Save is clicked, THEN VAL-206 blocks with its message.
- **AC-2.9** GIVEN all tenant KPIs already targeted, WHEN the form renders, THEN Add KPI Target is disabled.
- **AC-2.10** GIVEN a deactivated Target, WHEN the form renders, THEN its body is faded and inert, only Activate and Delete operate, and Save succeeds provided another active Target exists (VAL-207).
- **AC-2.11** GIVEN a Completed action, WHEN any Edit path is attempted (card, details, deep link), THEN no edit form opens; deep links redirect to SCR-03 with toast NTF-6.
- **AC-2.12** GIVEN an Active action being edited, WHEN the End Date is changed, THEN DLG-4 appears; confirming moves Target Start to End + 1 and recomputes every Target's Time Progress and timer state.
- **AC-2.13** GIVEN an active Target with L = 0 and U = 0, WHEN Save is clicked, THEN VAL-210 blocks with "Upper Threshold must be greater than zero".

---

## 8. Functional Requirements — SCR-03: Action Details

### 8.1 Purpose & entry
Full breakdown of one Action: identity, status, the four-date timeline, and one row per KPI Target with the complete visualisation set. Entry: More Details (any card), More Details (preview) on Planned, or deep link /actions/:id. **Full page route, never a dialog.**

### 8.2 Header block  [HTML]

| Element | Spec |
|---|---|
| Back link | "← Back to Actions" → SCR-01 |
| H1 | Action Name |
| Status badge | Exactly one of Active / Planned / Completed / **Archived** (§5.4) — Archived is standalone and replaces the date-computed badge while set |
| **Edit** button | Outline, pencil icon; visible only for **Planned/Active, non-archived** actions (BR-023); → SCR-02 pre-filled |
| **Archive** button | Outline; visible on all **non-archived** actions (added for symmetry with the cards, stakeholder-confirmed); enters Archived status in place; toast NTF-4 |
| **Unarchive** button | Outline; visible only while **Archived**; exits to the date-computed status in place; toast NTF-5 |
| Description | Muted paragraph |
| Date row | Four labelled values: "**Action Start · baseline captured**" {date} · "**Action End**" {date} · "**Target Start (derived)**" {date, primary colour, tooltip "System-derived: Action End Date + 1 day. Monitoring clock starts here."} · "**Latest Target Date**" {max Target Date across Targets} |

### 8.3 Action Targets list — common row grid  [HTML]
Section heading "**Action Targets**". One row card per Target, 3-column grid (KPI zone ≈130 px · slider flexes · side zone ≈210 px); collapses to one column < 940 px.

### 8.4 Row variants — Active action  [BR + HTML]
**(a) Active, unevaluated Target:**
- KPI zone: KPI chip; **"Lowest performing"** badge on the featured Target (§3.6); row additionally highlighted with a 2 px cyan focus ring.
- Slider: §5.1 **reference variant** — B & C bold markers/labels **plus** L/U reference flags with values above the numbers. Reference flags are informational only. [BR]
- Side zone (end-aligned facts + timer): "**{Target Date}** / Target date" · "**{s}%** / Score progress" (display-clamped) · Timer Ring (own Target Date ⇒ each row has its **own** timer). Tooltip "Time {t}% · Score {s}%".

**(b) Evaluated Target on a still-Active action (stakeholder-confirmed):** when a Target's own Target Date has passed while later Targets keep the action Active, its row renders **exactly like a Completed row (§8.5)** — outcome label, C flag inside its outcome zone, side zone "**{Target Date}** / Evaluated", full grey timer (tooltip "Monitoring complete") — and it is excluded from lowest-performing selection. [BR]

### 8.5 Row variant — Completed action (every Target evaluated)  [BR + HTML]
- KPI zone: KPI chip + **outcome label** (dot + text, §5.4).
- Slider: reference variant with the **C flag landing visually inside its outcome zone** — green zone for Successful, yellow band for Partially Successful, red for Unsuccessful — so the result is readable **on the slider itself**. [BR]
- Side zone: "**{Target Date}** / Evaluated" · full grey timer (tooltip "Monitoring complete").
- Full outcome sentences (tooltips): "Successful — reached or exceeded the upper threshold on the target date" / "Partially successful — reached the lower threshold on the target date" / "Unsuccessful — did not reach the lower threshold on the target date".
- **No Activate/Delete controls anywhere** — Completed actions are read-only (BR-023).

### 8.6 Row variant — Planned action  [BR + HTML]
Mirrors the Planned card: slider without B flag and **without** L/U reference flags (no baseline to anchor them); side zone "**{Target Date}** / Target date" + **empty timer** (tooltip "Monitoring not started").

### 8.7 Row variant — deactivated Target (Planned/Active actions only)  [BR + HTML]
- Entire row faded grey (≈50 % opacity + greyscale) but showing **the same full details** (slider, dates) frozen. [BR — "same details but with fady grey view"]
- KPI zone adds the **"Deactivated"** badge and two always-operable buttons: **Activate** (outline; disabled with tooltip "KPI is inactive in M-06" while force-deactivated and the KPI remains inactive — §10.2) and **Delete** (ghost; opens DLG-1).
- Side zone: "**—** / Excluded from results" + empty timer (tooltip "Deactivated — excluded from results").
- On Completed or Archived actions these controls are hidden (read-only / view-only); the faded row still renders.

### 8.8 Buttons — SCR-03

| Button | Visibility | Action | Success |
|---|---|---|---|
| Edit | Planned/Active, non-archived; Program Manager | → SCR-02 pre-filled | Form loads |
| Archive | Non-archived; Program Manager | Enter Archived status; audit | Page refreshes in place: badge becomes "Archived", Edit hidden, Unarchive shown; toast NTF-4 |
| Unarchive | Archived; Program Manager | Exit Archived; status recomputes; audit | Page refreshes in place: date-computed badge restored, Edit restored when Planned/Active; toast NTF-5 |
| Activate (target) | Deactivated Target rows on Planned/Active non-archived actions; enabled per §10.2 | Reactivate the Target; audit | Row un-fades; Target re-enters results & lowest-performing pool |
| Delete (target) | Deactivated Target rows on Planned/Active non-archived actions | DLG-1 → remove; audit | Row disappears; toast NTF-3 |
| ← Back to Actions | Always | → SCR-01 | — |

### 8.9 Acceptance criteria — SCR-03
- **AC-3.1** GIVEN an Active action with three unevaluated Targets of differing Target Dates, WHEN SCR-03 renders, THEN each row shows its own timer computed against its own Target Date, and exactly one row carries the "Lowest performing" badge + ring highlight — the one with the lowest raw Score Progress among eligible Targets.
- **AC-3.2** GIVEN a Completed action with outcomes Successful/Partial/Unsuccessful, WHEN it renders, THEN each row's C flag sits inside the matching colour zone, the outcome label matches, timers are full grey, and no Edit/Activate/Delete control exists.
- **AC-3.3** GIVEN a deactivated Target on an Active action, WHEN the page renders, THEN the row is faded with slider and dates visible, marked "Deactivated", shows Activate + Delete, and displays "Excluded from results".
- **AC-3.4** GIVEN an Archived action, WHEN SCR-03 renders, THEN the single status badge reads "Archived", Edit is absent, Unarchive is present, and all values still compute live.
- **AC-3.5** GIVEN the date row, WHEN rendered, THEN Target Start always equals Action End + 1 day and carries the explanatory tooltip.
- **AC-3.6** GIVEN a Target force-deactivated because its KPI was deactivated in M-06, WHEN its KPI is still inactive, THEN the Activate button is disabled with the explanatory tooltip; once the KPI is Active again, Activate is enabled.
- **AC-3.7** GIVEN an Active action where one Target's Target Date passed yesterday, WHEN SCR-03 renders, THEN that row shows its outcome label, its C flag inside the outcome zone, "Evaluated {date}", and a full grey timer, while the other rows keep live timers and the evaluated row is never the "Lowest performing" one.
- **AC-3.8** GIVEN a non-archived action on SCR-03, WHEN Archive is clicked, THEN the page refreshes in place showing the "Archived" badge alone, Edit disappears, Unarchive appears, and the SCR-01 counts reflect the move.

---

## 9. Cross-Screen Business Rules (consolidated)

| ID | Rule | Source |
|---|---|---|
| BR-001 | One KPI Target per KPI per Action; enforced live by disabling already-chosen KPIs in every other Target's select. | [BR] |
| BR-002 | Only **Active** KPIs from M-06 are offered in the KPI select. | [BR] |
| BR-003 | Thresholds are **deltas over the Baseline**, range 0 → X, decimals allowed (doubles, 1 dp), U ≥ L with equality allowed, U > 0 (VAL-210). | [BR] |
| BR-004 | Auto-sync: from L's first change off 0 (typed or dragged), U mirrors L until U is independently set; afterwards L ≤ U is clamped in both directions. | [BR] |
| BR-005 | Baseline = KPI score on the **Action Start Date** (recapture per BR-B2). | [BR] |
| BR-006 | Target Start Date = Action End Date + 1 day, system-derived, displayed read-only, never editable. | [BR] |
| BR-007 | Date ordering: Start ≤ End < every Target Date. Retro-dating permitted on all user dates. | [BR] |
| BR-008 | For non-archived actions, status is computed, date-driven: Planned (Start > now) → Active → Completed (latest Target Date passed). This **supersedes** the earlier "at least one KPI under its upper threshold" Active condition (§10.4). | [BR] |
| BR-009 | **Archived is a standalone status**, exclusive of Planned/Active/Completed, and non-destructive: measurement computation, timers, evaluations, and phase logic continue unchanged while Archived; presentation is view-only in the Archived tab; unarchiving recomputes the status from dates and restores editability where Planned/Active. Archiving requires no confirmation and is available from every other status, on cards and on SCR-03. | [BR] |
| BR-010 | Deactivated Targets are excluded from: outcome evaluation, results display eligibility, and lowest-performing selection; they render faded read-only with full frozen details wherever shown. | [BR] |
| BR-011 | M-06 KPI deactivation **force-deactivates** all its Targets across all actions: faded read-only, deletable, re-activatable **only** once the KPI is Active in M-06 again. | [BR] |
| BR-012 | Delete exists only at **Target** level, only while the Target is deactivated (manual or forced), only on Planned/Active non-archived actions, and always behind confirmation DLG-1. No action-level delete. | [BR] |
| BR-013 | Lowest-performing selection per §3.6 (raw unclamped Score Progress; eligibility excludes deactivated and already-evaluated Targets; tie-breaks: earliest Target Date, then KPI name). Zero-eligible fallback per FR-111. | [BR] |
| BR-014 | All drawn values clamp to 0–100 % / track bounds; all logic uses raw values (BR-F2) with the §3.7 adaptive padding guaranteeing the C flag stays on-track. | [BR] |
| BR-015 | Timer equality band: abs(Score − Time) ≤ 0.005 ⇒ Yellow (deterministic "on pace" rendering). | [BR — ratified] |
| BR-016 | Search: case-insensitive Action-Name substring, **all four tabs** at once, with cross-tab match hint. Filters (KPI multi-select, Start-Date range from–to) AND-combine with search across all four tabs. No Status filter; no Created-by filter. | [BR] |
| BR-017 | Every KPI on an action appears as a mini label on its Active/Planned card; deactivated ones struck through. Completed cards satisfy this via outcome chips. | [BR + HTML] |
| BR-018 | Edit mode = the Add Action layout pre-filled. Editing is available **only** for Planned and Active non-archived actions; every field is editable there. **Completed actions are read-only** (BR-023); Archived actions are view-only until unarchived. | [BR] |
| BR-019 | All KPIs are higher-is-better (M-06 standardisation); no inverted-KPI handling exists anywhere in M-15. | [BR] |
| BR-020 | X (max upper threshold, default 20) and PAD (slider padding, default 3, positive integer) are tenant-configurable in Settings → Actions and apply module-wide. | [BR] |
| BR-021 | Removed features must not resurface: Clone, action-level Delete, Status filter, **Created-by filter**, "Deactivate/End early", Expired/Past vocabulary, editing of Completed actions, review-cadence functionality. | [BR] |
| BR-022 | **Timezone & granularity:** every day-boundary comparison in the module — baseline capture (D1), Target Start derivation (D3), Planned→Active and Active→Completed transitions, and outcome evaluation (D4) — operates at **day granularity in the tenant's configured timezone**. | [BR] |
| BR-023 | **Completed actions are read-only.** No edit form, no field changes, no target activate/deactivate/delete. Permitted operations: view (SCR-03), Archive, Unarchive. Consequently evaluated outcomes cannot be rewritten and a Completed action can never be resurrected to Active by edits. | [BR] |

## 10. Status Lifecycle

### 10.1 Action statuses

| Status | Condition | Visual | Tab | Editability |
|---|---|---|---|---|
| **Planned** | Non-archived; Action Start Date > Current Date | Navy badge; empty timers; C-only sliders | Planned | Editable |
| **Active** | Non-archived; Start ≤ now ≤ latest Target Date | Green badge; live timers/sliders | Active | Editable |
| **Completed** | Non-archived; latest Target Date < Current Date | Muted badge; grey full timers; outcomes | Completed | **Read-only** (BR-023) |
| **Archived** | Archived flag set — standalone, overrides the above for presentation | Dashed muted badge (sole badge); underlying card shape per date-computed form | Archived | **View-only**; Unarchive only |

**Transitions:** Planned → Active → Completed occur automatically as the clock crosses D1 and the latest D4 (tenant timezone); **no manual status transition exists** among these three. Editing dates on Planned/Active actions can move an action between Planned/Active/Completed in either direction via recomputation (FR-102); once Completed, no edits are possible, so Completed is terminal except for Archive. **Any status → Archived** via Archive; **Archived → date-computed status** via Unarchive. Invalid: any other user-initiated status set; an action is never simultaneously Archived and Planned/Active/Completed.

### 10.2 KPI Target lifecycle

| State | Entered by | Behaviour | Exit |
|---|---|---|---|
| **Active** | Default on creation / reactivation | Fully editable (while the parent is editable); counted in results & lowest-performing; evaluated on its Target Date | Manual deactivate; force-deactivate; evaluation (remains Active but leaves the eligibility pool — §3.6, §8.4b) |
| **Deactivated (manual)** | Toggle off (Planned/Active parents) | Faded read-only, full details frozen; excluded per BR-010; Delete available | Activate (always enabled) · Delete |
| **Deactivated (forced)** | KPI deactivated in M-06 (BR-011) | Same rendering & exclusions; Delete available | Activate **only when** the KPI is Active in M-06 again · Delete |

### 10.3 Archived status mechanics
`archived: boolean` is the stored representation; when set, the presented status is **Archived** (standalone — never co-displayed with another status). Setting/clearing is allowed from any status, never pauses measurement computation (timers keep running, targets keep getting evaluated, Completed transition still fires underneath), and controls: tab placement (FR-103), the single Archived badge, view-only presentation, and the Archive/Unarchive controls (§6.7, §8.2, §8.8). Both operations are audit events. On unarchive, the status recomputes from dates — including landing directly in Completed (read-only) if the latest Target Date passed while Archived.

### 10.4 Superseded decisions (recorded for traceability)
1. Baseline anchored at Action **End** Date (two early phrasings) → superseded by **Start Date** + Option-C two-anchor model. [BR]
2. Slider labels in the first reference image (red arrow labelled "Upper" near 0) → corrected: nearer 0 = Lower, nearer max = Upper. [BR]
3. "Handles must not intersect" → relaxed to **U ≥ L, equality allowed** (with U > 0 to save, VAL-210). [BR]
4. Add-page slider colouring: v1 fixed gradient → v2 flag-anchored dynamic gradient → **final: grey-until-set + hard-edged Red/Yellow/Green zones** (§5.2). [BR]
5. Tabs "Past"/status "Expired" → **Completed** everywhere. [BR]
6. Active-status condition "≥1 KPI under upper AND date ≤ target" → **purely date-driven** Completed transition (BR-008). [BR]
7. Kebab sets containing Clone / Delete / Deactivate-End-early → final matrix §6.7. [BR]
8. "Pin C flag to track edge" → superseded by adaptive padding §3.7. [BR]
9. Status filter in the toolbar → removed; **Created-by filter → removed** (v1.1, no-assignee decision). [BR]
10. Archive as an orthogonal flag co-displayed with a status (v1.0) → **Archived as a standalone, exclusive status** (v1.1), with measurement continuity retained. [BR]
11. "All fields editable regardless of status" (v1.0) → **Completed actions are read-only** (v1.1, BR-023). [BR]
12. Prototype-only toast wordings ("prototype only…", "semantics TBC") → production copies in §15.1.

## 11. Settings — "Actions" subsection (appended to platform Settings)

| ID | Setting | Type | Default | Range/validation | Effect |
|---|---|---|---|---|---|
| SET-1 | **Action Target Maximum Upper Threshold (X)** | number, 1 dp | **20** | > 0; **cannot be lowered below the largest U saved in the tenant** — such an attempt is blocked with "Cannot set the maximum below an existing Upper Threshold ({largest U})" (stakeholder-confirmed) | Threshold slider scale 0→X; VAL-209 ceiling; scale-note text |
| SET-2 | **Slider Padding (PAD)** | positive integer | **3** | ≥ 1 | §3.7 track extension on every stepped zone slider |

Changes apply tenant-wide on next render, are audit-logged, and require the settings-administration permission (Program Manager per §13; refined by M-10). Both settings were stakeholder-mandated for this SRS. [BR]

## 12. Integrations & Cross-Module Contracts

### 12.1 M-06 — KPI Engine (hard dependency)

| Flow | Direction | Detail |
|---|---|---|
| Active KPI registry | M-06 → M-15 | Populates the KPI select (BR-002); prototype set: NPS, CSAT, CES, FCR, VFM, Agent Score, CHS |
| Live current score | M-06 → M-15 | C flags, Score Progress, §7.6 label |
| Normalised 0–100 index | M-06 → M-15 | Planned lowest-current-score selection (§3.6) |
| Historical daily score | M-06 → M-15 | Baseline capture & recapture, retro-dating (BR-B1/B2, BR-D1) |
| KPI-deactivation / reactivation events | M-06 → M-15 | Trigger force-deactivation and re-enable Activate (BR-011) |
| Failure handling | — | See ERR-4/ERR-5 |

### 12.2 M-07 — Dashboards & Reporting (forward contract)
M-07's trend-analysis chart SHALL offer an **option to overlay Planned / Active / Completed actions as vertical solid or dashed reference lines** on KPI trend lines, so an action's effect on the overall trend is visible. M-15 exposes per action: name, status, Start/End/Target-Start/latest-Target dates, and the Archived status (Archived actions excluded from the overlay by default). Rendering follows the design system's Trend Chart Annotations pattern. [BR]

### 12.3 M-09 Notifications — postponed in full
**All user alerting for M-15 is postponed to the M-09 Notifications module and will be specified there** (stakeholder decision). M-15 v1 ships no alerts, emails, or push notifications of any kind — only the in-app toasts and confirmation dialogs of §15. M-15's obligation is limited to emitting its audit events (§12.4), which M-09 may later subscribe to. [BR]

### 12.4 Audit trail (F-M15-07 — data requirement, no UI in scope)
Every event records actor, timestamp, action id, and old→new values where applicable: action created; any field edited (field-level, incl. dates & thresholds); Baseline captured / recaptured; Target added / activated / deactivated (manual vs forced) / deleted; action archived / unarchived; automatic status transition; outcome evaluated; settings SET-1/SET-2 changed.

## 13. Permissions Matrix (confirmed interim — refined later by M-10 / DOC-02)  [BR]

| Capability | CX Program Manager | CX Analyst | Executive / Viewer |
|---|---|---|---|
| View SCR-01 / SCR-02 (read) / SCR-03 | ✔ | ✔ (SCR-01/03 only) | ✔ (SCR-01/03 only) |
| Create action (Add Action) | ✔ | ✖ | ✖ |
| Edit action (Planned/Active) | ✔ | ✖ | ✖ |
| Archive / Unarchive | ✔ | ✖ | ✖ |
| Activate / deactivate / delete targets | ✔ | ✖ | ✖ |
| Settings → Actions (SET-1/2) | ✔ | ✖ | ✖ |

Write controls are hidden (not merely disabled) for view-only roles; server-side enforcement returns ERR-3 on any bypass. M-10 may later refine roles without contradicting this baseline.

## 14. Error Handling

| ID | Scenario | Behaviour |
|---|---|---|
| ERR-1 | Field validation on Save | First failing VAL rule → toast + inline message + focus (§7.8); no partial save |
| ERR-2 | Duplicate Action Name | VAL-202 message; save blocked |
| ERR-3 | Permission denied | Standard platform 403 pattern; controls hidden per §13 |
| ERR-4 | M-06 unreachable on SCR-02 | KPI select disabled with helper "KPI list unavailable — try again"; score label hidden; Save blocked for new Targets |
| ERR-5 | M-06 score missing for a date (baseline/recapture) | Blocking dialog: "No KPI score exists for {date}. Choose a different Action Start Date."; no silent fallback |
| ERR-6 | Deep link to missing/foreign-tenant action | "Action not found" empty state + Back to Actions |
| ERR-7 | Archive/unarchive write failure | Toast "Couldn't update the action — try again"; status unchanged |
| ERR-8 | Concurrent edits (two editors) | Last-write-wins + full audit trail + stale-save warning on version mismatch (stakeholder-confirmed; no record locking) |
| ERR-9 | Network failure on Save | Toast "Connection lost — your changes were not saved"; form state preserved |
| ERR-10 | Live-score refresh failure on cards | Render last known values with a stale-data tooltip; never block the page |
| ERR-11 | Edit attempt on Completed/Archived (incl. deep link) | Redirect to SCR-03 + toast NTF-6; server rejects any write |

## 15. Notifications (in-app UI feedback only — all alerting deferred to M-09, §12.3)

### 15.1 Toasts (exact copy)

| ID | Trigger | Copy |
|---|---|---|
| NTF-1 | Save with VAL failure | The VAL message (e.g., "Action Name is required", "At least one active KPI target is required", "Upper Threshold must be greater than zero") |
| NTF-2 | Successful save | "Action saved" |
| NTF-3 | Target deleted | "Target removed" |
| NTF-4 | Archived | "Action archived — it keeps running and is available in the Archived tab" |
| NTF-5 | Unarchived | "Action unarchived — it resumes in its status tab with its original dates" |
| NTF-6 | Edit attempted on a Completed/Archived action | "Completed actions are read-only" / "Unarchive this action to edit it" |

### 15.2 Confirmation dialogs

| ID | Trigger | Title / body | Buttons |
|---|---|---|---|
| DLG-1 | Delete a (deactivated) Target | "Delete this KPI target?" / "The target and its configuration will be removed from this action. This cannot be undone." | Cancel (ghost) · Delete (destructive) |
| DLG-2 | Edit Start Date after Baseline exists | "Recapture baselines?" / "Changing the Action Start Date re-captures every KPI baseline for the new date and recalculates all progress and outcomes." | Cancel · Recalculate & continue (primary) |
| DLG-3 | Edit thresholds mid-monitoring | "Change thresholds?" / "This changes how progress and outcomes are calculated for this target." | Cancel · Apply (primary) |
| DLG-4 | Edit End Date on a started action | "Move the monitoring start?" / "Changing the Action End Date moves the Target Start Date (End + 1) and recalculates time progress for every target." | Cancel · Recalculate & continue (primary) |

Archiving intentionally has **no** dialog (non-destructive, BR-009).

## 16. Non-Functional Requirements
- **NFR-1 Localisation/RTL:** Arabic-first production (`dir="rtl"` default), full EN/AR parity, native فصحى copy (not literal translation of the English strings in this SRS), logical CSS properties throughout, Latin numerals in LTR spans, IBM Plex Sans Arabic fallback, minimum 14 px Arabic body with relaxed leading. The prototype is the LTR reference.
- **NFR-2 Theming:** light + navy-tinted dark mode; design-system tokens only (no raw hex); **Two-Palette Rule** — brand cyan/mint for chrome/CTAs/emphasis only; D-scale semantic tokens exclusively for pace, zones, and outcomes, never decoratively.
- **NFR-3 Typography:** Sora headings, Poppins body, tabular numerals for all scores/dates.
- **NFR-4 Accessibility:** WCAG 2.1 AA; visible focus rings; keyboard operation of threshold flags (§5.2), tabs, kebabs; `role="slider"`/`role="img"`/`role="status"`/`aria-haspopup` as specified; colour never the sole signal (labels/letters/tooltips accompany every colour state); contrast ≥ 4.5:1 text, 3:1 components; `prefers-reduced-motion` honoured for all transitions.
- **NFR-5 Performance:** SCR-01 interactive < 2 s with 200 actions; search/filter feedback < 100 ms; slider drag at 60 fps; derived values computed client-side from delivered data.
- **NFR-6 Responsive:** ≥ 1280 px full layout; card grid ≥ 430 px columns; single column and stacked target rows below 940 px; sidebar collapses below 940 px (platform shell).
- **NFR-7 Audit & security:** every write audit-logged (§12.4); strict tenant isolation; module access via platform auth; server-side enforcement of §13 and BR-023; no PII beyond creator attribution.
- **NFR-8 Time handling:** all day-boundary logic per BR-022 (tenant timezone, day granularity); server is the source of truth for "today".
- **NFR-9 Browser/session:** last 2 evergreen versions; platform-standard session handling; unsaved-changes state survives transient network loss (ERR-9).

## 17. Dependency & Side-Effect Analysis
- **Affected modules:** M-06 (5 data flows + 2 events, §12.1), M-07 (overlay contract §12.2), M-09 (audit-event subscription only; all alerting specified there), M-10 (future refinement of §13), platform Settings (new "Actions" subsection), platform audit service, platform tenant-timezone setting (consumed).
- **API changes required:** yes — new M-15 CRUD/read surfaces and the M-06 flows above (shapes out of scope per template).
- **Data migration required:** no (new module; no pre-existing data).
- **Integration impact:** M-06 must add/confirm historical-score-by-date and KPI deactivation/reactivation events; M-07 must plan the overlay toggle; none broken.
- **UI changes cascading:** Settings screen gains the Actions subsection; sidebar gains/keeps the Actions item; M-07 chart gains an overlay option.
- **Risks & mitigations:** missing historical scores → ERR-5 blocking dialog; KPI force-deactivation storms → batch the event handling + audit; mid-flight date/threshold edits distorting results → DLG-2/3/4 warnings + field-level audit; X lowered below existing U → blocked by SET-1 guard; edits racing the Completed transition → server re-validates BR-023 at write time (ERR-11).

## 18. Traceability Matrix (cluster level; per-requirement tags inline throughout)

| Cluster | Fulfilled by | Primary sources |
|---|---|---|
| F-M15-01 Action definition | §7 (SCR-02), §3.2–3.3, VAL-201…211 | BR briefs & follow-ups; HTML v6 |
| F-M15-02 Action assignment | No assignee in v1 (stakeholder-confirmed); `created_by` auto-captured for audit only (§2.1, §12.4) | BR final ruling #9 |
| F-M15-03 Action lifecycle | §10, FR-102/103/105, BR-008/009/022/023 | BR decisions incl. Completed rename, read-only Completed, standalone Archived |
| F-M15-04 Source linkage | §12.1 (KPI linkage only in v1 — confirmed) | BR final ruling #8; Platform Definition |
| F-M15-05 Outcome measurement | §3 (entire), §5.1–5.3, §6.4–6.6, §8.4–8.5 | BR formulas; HTML visualisations |
| F-M15-07 Audit trail | §12.4, BR-B2, §11 | BR (recapture logging); Platform Definition |

## 19. Ratified Decisions Register (former assumptions — all confirmed by stakeholder, 21 Jul 2026)

| # | Decision (now normative) | Home |
|---|---|---|
| R-1 | Interim permissions: Program Manager full control; Analyst and Viewer view-only; M-10 refines later | §13 |
| R-2 | Planned sliders anchor zones provisionally at the KPI's current score until Baseline capture | §3.7, §5.1, §6.5 |
| R-3 | Default landing tab Active; newest-created-first ordering; pagination/infinite scroll beyond one viewport | FR-110 |
| R-4 | Concurrency: last-write-wins + audit + stale-save warning; no locking | ERR-8 |
| R-5 | v1 source linkage = KPIs only (no journeys/cases/AI recommendations) | §1.2, §2.1 |
| R-6 | No assignee in v1; Created-by filter removed; creator captured for audit only | §2.1, FR-107, BR-021 |
| R-7 | SET-1 guard: X cannot go below the largest saved U | §11 |
| R-8 | PAD positive integer; yellow-timer equality band ±0.005; Description plain text ≤ 500; name uniqueness per tenant across all statuses incl. Archived | §11, BR-015, VAL-202/205 |
| R-9 | VAL-210: Upper Threshold must be > 0 on active Targets | §7.8, BR-F3 |
| R-10 | Evaluated Target on an Active action renders as a Completed-style row, excluded from lowest-performing | §8.4(b) |
| R-11 | Zero-eligible-target Active card fallback ("No active targets to feature") | FR-111 |
| R-12 | End-Date edits on started actions guarded by DLG-4 | §7.9, §15.2 |
| R-13 | Completed actions are read-only | BR-023 |
| R-14 | Archived is a standalone status; Archive added to the SCR-03 header | §10.3, §8.2 |
| R-15 | Search & filters span all four tabs | FR-106/107, BR-016 |
| R-16 | Tenant-timezone, day-granularity time handling | BR-022, NFR-8 |

## 20. Open Questions
**None.** All open questions and assumptions from v1.0 were resolved by the stakeholder's 18-point ruling of 21 July 2026 and are recorded in §19 and §10.4. This SRS is final and ready for Speckit.

---

## Appendix A — Data Dictionary (entity summary)

| Entity | Attributes |
|---|---|
| **Action** | id · tenant_id · action_name (≤120, unique/tenant across all statuses) · description (≤500, plain text) · action_start_date · action_end_date · archived (bool, default false — presented as the standalone Archived status when true) · created_by (audit attribution only) · created_at · updated_at · *derived:* target_start_date, status (Planned / Active / Completed / Archived), latest_target_date |
| **KPI Target** | id · action_id · kpi_id (unique per action) · target_date · lower_threshold (0–X, 1 dp) · upper_threshold (L–X, 1 dp, > 0) · active (bool) · deactivation_source (manual / forced / null) · *captured:* baseline_score, baseline_captured_for_date · *derived:* score_progress, time_progress, timer_state, outcome |
| **Settings (Actions)** | max_upper_threshold X (default 20; SET-1 guard) · slider_padding PAD (default 3, positive integer) |
| **Audit event** | id · tenant_id · actor · timestamp · action_id · target_id? · event_type (§12.4 catalogue) · old_value · new_value |

*End of SRS v1.1 (Final) — M-15 Action Management.*
