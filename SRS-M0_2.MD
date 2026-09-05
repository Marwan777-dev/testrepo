# NABADAT VOC PLATFORM — Software Requirements Specification — Survey & Form Builder (Module M-01 · Phase 1 Core Module)

| **Field** | **Value** |
|---|---|
| Version | 6.0 (Draft) — prototype-aligned, screen-by-screen |
| Status | Draft for review — reflects the current clickable prototype |
| Module | M-01 — Survey & Form Builder |
| Author | CX R&D |
| Audience | Backend, frontend, QA, UX, product, CX consultants |
| Companion prototype | m01-survey-screens.html (single-file clickthrough) |

*Confidential — NabadatCX*

<a id="contents"></a>
# Contents

- Contents
- 1. Introduction
  - 1.1 Purpose & audience
  - 1.2 Scope
  - 1.3 Definitions
- 2. Overall description
  - 2.1 Module interactions
  - 2.2 Operating environment
  - 2.3 Primary actors
- 3. System features (screen by screen)
  - 3.1 F1 — Survey Library
  - 3.2 F2 — Sections & Questions Sets (from the library)
  - 3.3 F3 — Survey Settings (build form)
  - 3.4 F4 — Appearance & live preview
  - 3.5 F5 — Build method
  - 3.6 F6 — Choose a template
  - 3.7 F7 — Template authoring
  - 3.8 F8 — Build Survey (the builder)
  - 3.9 F9 — Answer routing (skip logic)
  - 3.10 F10 — Questions Set settings & low-response ordering
  - 3.11 F11 — Translate workspace
  - 3.12 F12 — Multi-channel preview
  - 3.13 F13 — Survey Report
  - 3.14 F14 — Analytics
  - 3.15 F15 — Survey approval & publishing workflow
- 4. Data model (M-01 owned)
  - 4.1 Survey
  - 4.2 Section & Questions Set
  - 4.3 Question (+ type-specific)
  - 4.4 Theme, Template, Translation
- 5. Permissions (RBAC — extends M-10)
- 6. Non-functional requirements
- 7. Localization & RTL
- 8. Glossary
- 9. Change log — prototype iterations reflected
  - Batch A — foundations
  - Batch B — builder, routing, report
  - Batch C — placement & polish
  - Batch D — report maths & library UX
  - Batch E — routing & navigation
  - Batch F — approval, rules & analytics deltas
  - Batch G — sections & Questions Sets, seasonal binding, active-period expiry, report result types
  - Batch H — verbatim response sampling & text sentiment flag (current)

<a id="introduction"></a>
# 1. Introduction

<a id="purpose-audience"></a>
## 1.1 Purpose & audience

This document specifies the **Survey & Form Builder (M-01)** module of the Nabadat Voice-of-Customer (VOC) platform. It is the single source of truth for engineering, QA, design, product and CX teams. The document is organised **screen by screen**: every feature page is presented with a screenshot of the working prototype, the **business context** that motivates it, and the detailed rules, fields, labels and acceptance scenarios that implement it. Figures show the working prototype with the left navigation cropped out, so each screen appears at maximum size and clarity.

The reader is assumed to have no prior CX/VOC knowledge; terms are defined on first use and consolidated in the Glossary.

<a id="scope"></a>
## 1.2 Scope

**In scope:** survey lifecycle authoring — survey library and status management; survey settings (identity, optional journey binding, respondent messages, collection behaviour, active-period expiry, appearance); the survey structure model — **sections** that hold standalone questions and/or **Questions Sets** (rotating question pools with a selection mode and a per-sending count); the question builder (all question types **and their sub-types**, KPI binding, scale/appearance of each type, matrix perspectives, reason follow-ups, **optional per-question comment capture**); answer routing (skip logic); drag-and-drop ordering of **sections, Questions Sets and questions**; templates (built-in and customized, storing a survey's **settings and questions**) and template authoring; localization/translation workspace; survey appearance/theming with live preview; multi-channel preview; survey report and analytics surfaces exposed from the module; the survey approval & publishing workflow (draft → pending review → publish) including the M-10 self-publish grant; survey statuses and their interaction with distribution rules.

**Out of scope (owned elsewhere):** channel delivery and distribution (M-02); audience/contacts (M-03); response ingestion (M-04); NLP/sentiment (M-05); KPI definition and score computation (M-06 — M-01 consumes active KPI definitions and binds them per touchpoint); dashboards rendering internals and the **post-expiry response store** page (M-07); alerts (M-09); RBAC primitives (M-10); tenant administration, branding defaults and audit log (M-11); journey/stage/touchpoint definitions (M-16 — M-01 binds to them).

<a id="definitions"></a>
## 1.3 Definitions

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| VOC | — | Voice of Customer — structured programme to collect, analyse and act on feedback. |
| KPI | — | Key Performance Indicator (NPS, CSAT, CES, FCR, VFM, Agent Score, CHS). Defined in M-06; bound per touchpoint in M-01. |
| Touchpoint | — | A discrete customer interaction inside a journey stage (defined in M-16). |
| Section | — | A named block within a survey that holds standalone questions and/or one or more Questions Sets. Sections order the survey and can be reordered. |
| Questions Set | — | A rotating pool of questions inside a section. Each respondent receives a subset chosen by the set's selection mode (Random or Prioritize low-response) up to a configured per-sending count, so a large bank is covered over time while each response stays short. |
| Routing | — | Skip logic that sends a respondent to a specific next question based on the answer given. |
| Theme | — | The visual styling of a survey: either inherited from Tenant Design Guidelines or customized per survey. |
| Template | — | A reusable snapshot of a survey's **settings and questions** (collection behaviour, appearance, messages, questions and KPI links) with **no journey/stage/touchpoint bindings**; Built-in (platform, locked) or Customized (tenant-authored). |
| Active period (expiry) | — | How long a survey keeps collecting **after it is sent**, expressed in **days/hours** (a duration, not a calendar date). Once elapsed the survey stops collecting on the live report; late responses are retained in the M-07 post-expiry store. |
| Seasonal / Relational | — | A survey **not bound to a journey touchpoint** (relationship/periodic feedback). Leaving Bound journey empty classifies the survey as Seasonal / Relational; binding a journey makes it Transactional. |
| Comment field | — | An optional free-text box shown under a question after it is answered; the comment travels with the response for M-05 text analytics. |

<a id="overall-description"></a>
# 2. Overall description

<a id="module-interactions"></a>
## 2.1 Module interactions

- **M-06 CX Metrics & KPI Engine (consumer/provider):** M-01 reads the active KPI catalogue (scale, representation, normalisation, calculation method) to render KPI questions and to expose a KPI picker; M-06 computes scores from the responses collected.
- **M-16 Customer Journey Mapping (provider):** supplies journeys, stages and touchpoints. A KPI question in M-01 binds to a single touchpoint via Journey → Stage → Touchpoint.
- **M-02 Channel Management (consumer):** renders and distributes the survey across channels using the definitions here; M-01 provides channel previews.
- **M-04 Response Collection (consumer):** attaches responses to the published survey version and enforces the **active-period expiry** (a duration measured from send). Responses that arrive after expiry are flagged post-expiry and routed to the M-07 store rather than the live report.
- **M-05 NLP (consumer):** analyses open-text/paragraph answers and per-question **comment** entries; a Text/Paragraph question may be **flagged for sentiment analysis** by the author. The survey report shows a sample of recent verbatim responses, while frequent-word, theme and sentiment analytics are owned by M-05.
- **M-07 Dashboards & Reporting (consumer):** the Report and Analytics surfaces in this document consume M-06/M-04 aggregates; M-07 also hosts a dedicated **post-expiry response store** page that retains responses collected after a survey's active period ends.
- **M-10 / M-11 (foundational):** roles/permissions, tenant isolation, branding defaults (Tenant Design Guidelines) and the audit log.

<a id="operating-environment"></a>
## 2.2 Operating environment

- Web, latest two versions of Chrome, Edge, Safari, Firefox; desktop-first, tablet supported.
- Bilingual **Arabic + English** with full **RTL** parity across every screen, control, tooltip and message.
- Strict multi-tenant isolation; all actions audit-logged to M-11.

<a id="primary-actors"></a>
## 2.3 Primary actors

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| P-01 CX Program Manager | — | Primary author: creates/edits surveys, sections, Questions Sets, questions, KPI bindings, routing, themes, templates. |
| P-02 CX Analyst | — | Read/preview; consumes report & analytics. |
| P-03 Survey Administrator | — | Operational authoring and distribution setup. |
| P-06 Executive Sponsor | — | Read-only summary/report consumption. |

<a id="system-features-screen-by-screen"></a>
# 3. System features (screen by screen)

Each feature below opens with the prototype screenshot, followed by its business context, then the fields/labels, functional requirements (FR), business rules (BR) and key acceptance scenarios.

<a id="f1-survey-library"></a>
## 3.1 F1 — Survey Library

> **Business context —** The Survey Library is the CX Program Manager's home base for the VOC programme. From one place they see every survey and template, its journey binding, live response volume, status and whether it uses tenant or custom branding — so they can triage what needs attention (paused, draft, low volume) and jump straight into editing, previewing, reporting or analysing without hunting through menus.

<a id="f1-layout-fields-labels"></a>
### Layout, fields & labels

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Tabs | Segmented | **Surveys ‹count›** and **Templates ‹count›**; counts are live. |
| Search | Text | Filters by the survey's English name. On the Templates tab it also matches **tags**. |
| Type / Status / Journey | Dropdowns | Filter chips. Type = Transactional, Seasonal / Relational. Status = Draft/Active/Paused/Archived. |
| Add Template / Add Survey | Buttons | Primary entry points. Add Survey opens the build-method flow; Add Template opens template settings. |
| Table columns | — | Survey (**English name only**), Type, Journey, Responses, Status (coloured dot), Updated, Actions. The Theme column is not shown in the list. |
| Row actions | Icon buttons | **Preview**, **Survey report**, **Analytics** (quick access), and an overflow menu (Edit, Change status, Clone, Sections, Archive). |

<a id="f1-functional-requirements"></a>
### Functional requirements

| **ID** | **Requirement** |
|---|---|
| FR-1.1 | The two tabs display live counts of surveys and templates and switch the listing without a page reload. |
| FR-1.2 | Surveys and templates are listed by their **English name only** (the Arabic name is authored in the Translate workspace and is not shown in lists). Search filters the list in real time by English name; on the Templates tab, matches are also evaluated against each template's **tags**. |
| FR-1.3 | Type, Status and Journey dropdowns filter the list; filters combine (AND) with search and with one another, and clearing a filter restores its rows. When the combined filters match nothing, the list shows an explicit "no results" state rather than a blank table. |
| FR-1.4 | Each row exposes quick-access icons for **Preview**, **Survey report** and **Analytics**, plus an overflow menu (Edit, Change status, Clone, Sections, Archive). |
| FR-1.5 | Clicking anywhere on a survey **row** (outside the action controls) opens that survey's **Survey Settings** screen. |
| FR-1.6 | Row action controls and the overflow menu stop propagation so they never trigger the row-level navigation. |
| FR-1.7 | Status is shown as a coloured dot + label. The survey's theme mode is still stored on the survey (inherited/customized) but is **no longer surfaced as a list column**. |
| FR-1.8 | Clone creates a "Copy of – ‹name›" draft and continues directly into the builder with the cloned questions (see F8). |

<a id="f1-business-rules"></a>
### Business rules

| **ID** | **Business rule** |
|---|---|
| BR-1.1 | Only **Active** surveys collect responses **and are sent by distribution rules**; **Paused** keeps configuration and rules but stops sending and new responses; **Draft** and **Pending review** never collect; **Archived** is read-only. |
| BR-1.2 | Every list action is scoped to the current tenant and written to the M-11 audit log. |
| BR-1.3 | **Archived is a terminal, read-only state.** The only status change available to an archived survey is **Unarchive**, which restores it as a **Draft**; a survey cannot move directly from Archived to Active or Paused. From the restored Draft the normal transitions apply before it can collect again. |

<a id="f1-key-scenarios"></a>
### Key scenarios

| **Scenario** | **Given → When → Then** |
|---|---|
| Open settings from list | Given the library is shown → When the manager clicks a survey row → Then the Survey Settings screen opens for that survey with its name pre-filled. |
| Quick analytics | Given a survey row → When the Analytics icon is clicked → Then the Analytics screen opens for that survey (no menu step). |
| Tag search | Given the Templates tab → When the manager types "onboarding" → Then templates whose name or tags contain "onboarding" remain listed. |

<a id="f1-statuses-distribution-rules-pausing"></a>
### Statuses, distribution rules & pausing

> **Business context —** Surveys are typically sent automatically by distribution rules (e.g., a post-transaction trigger in M-02). Those rules only fire while the survey is Active. Pausing a survey therefore has an operational side-effect — automated sending stops — so the platform makes that consequence explicit with a confirmation alert instead of silently cutting off feedback collection.

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Statuses | Enum | Draft · Pending review · Active · Paused · Archived (Pending review is entered by the approval workflow — see Section 3.15). |
| Rule linkage | — | Each survey exposes the count of connected distribution rules; rules send the survey **only when its status is Active**. |
| Pause confirmation | Modal | "Pause survey?" — warns: "This survey has N distribution rules connected… Rules only send a survey while it is Active — pausing will stop these rules from sending it until the survey is reactivated. The rules themselves are kept and are not deleted." Actions: Cancel / **Pause survey**. |

| **ID** | **Requirement** |
|---|---|
| FR-1.9 | A distribution rule sends its survey only while the survey status is **Active**; Draft, Pending review, Paused and Archived surveys are never sent by rules. |
| FR-1.10 | When the user changes an **Active** survey with ≥ 1 connected rule to **Paused**, a blocking confirmation alert is shown stating the number of connected rules and that pausing stops them from sending the survey until reactivation; the rules themselves are preserved. |
| FR-1.11 | Confirming pauses the survey and suspends rule-driven sending; cancelling leaves the status unchanged. Reactivating the survey resumes the connected rules without reconfiguration. |
| FR-1.12 | The status-change dialog states the rule behaviour: only Active surveys collect responses and are sent by rules; Paused keeps configuration **and rules** but stops sending and new responses. |
| FR-1.13 | In addition to pausing at survey level, the related module (M-02 Channel Management) must provide the ability to **pause an individual distribution rule** from the rule's side: a paused rule stops sending its survey while the survey itself remains Active and continues to be sent by its other active rules; reactivating the rule resumes sending without reconfiguration. |
| FR-1.14 | The Change-status action does not offer Active/Paused for an **Archived** survey; it offers only **Unarchive → Draft**. Unarchiving sets the status to Draft and re-enables editing; the survey does not resume collection or rule-driven sending until it is explicitly set Active again (via the normal transitions and, where required, the approval workflow). |

| **Scenario** | **Given → When → Then** |
|---|---|
| Pause with rules | Given an Active survey with 3 connected rules → When the manager selects Paused → Then the confirmation alert shows "3 distribution rules" and only on confirm is the survey paused and rule sending suspended. |
| Pause without rules | Given an Active survey with 0 rules → When Paused is selected → Then the status changes immediately with no rules alert. |
| Reactivate | Given a Paused survey with rules → When set back to Active → Then connected rules resume sending automatically. |

<a id="f2-question-groups-from-the-library"></a>
## 3.2 F2 — Sections & Questions Sets (from the library)

> **Business context —** A survey is organised into **sections**; each section holds standalone questions and/or one or more **Questions Sets**. A Questions Set is a rotating pool: each respondent is shown only a subset of its questions, so a large question bank is covered across many responses while any single response stays short. Exposing section and set management from the library means quick structural edits without entering the full builder.

<a id="f2-fields-labels"></a>
### Fields & labels

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Section row | — | Editable section name with a live count of standalone questions and Questions Sets. |
| Questions Set row | — | Nested under its section; shows the set title and "shows ‹k› of ‹n›" with its selection mode. |
| Questions Set settings | — | Opens the set settings editor (see F10): title, description, selection mode, number of questions per sending. |
| Add / Delete | — | Add a section, add a Questions Set to a section, or delete either. |

| **ID** | **Requirement** |
|---|---|
| FR-2.1 | The structure view lists the survey's **sections**, and under each section its **standalone question count** and its **Questions Sets**, each with a live "shows ‹k› of ‹n›" summary and selection mode. |
| FR-2.2 | From this view a manager can add a section, add a Questions Set to a section, open a Questions Set's settings (F10), and delete a section or a set. |
| FR-2.3 | A survey always has **at least one section**; deleting the last section is prevented. |

<a id="f3-survey-details-build-form"></a>
## 3.3 F3 — Survey Settings (build form)

> **Business context —** The Survey Settings screen captures everything about a survey that is not an individual question: what it measures, whether it is bound to a journey, the messages a respondent sees first and last, how pages are laid out, how long it stays open once sent, and how it looks. For a **new** survey it is reached **after** the build-method chooser (F5); it is also reachable by clicking an existing survey or the **Survey settings** button inside the builder. It keeps identity, messaging and collection behaviour in one predictable place.

<a id="f3-fields-labels"></a>
### Fields & labels

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Survey name | Text | English name (placeholder "e.g. Post-disbursement satisfaction"). The **Arabic name is managed in the Translate workspace**, not here. |
| Description | Textarea | Internal note — "What this survey measures and when it is sent." |
| Survey type | Dropdown (journey-derived) | Transactional · Seasonal / Relational. Kept in sync with Bound journey: a bound journey ⇒ Transactional, no journey ⇒ Seasonal / Relational. |
| Bound journey | Dropdown (**optional**) | **None** (default) or a journey (Personal Loan Application · Account Onboarding · Branch Visit). Drives the stages/touchpoints available to KPI questions. **Leaving it None classifies the survey as Seasonal / Relational**; choosing a journey makes it Transactional. |
| Welcome message | Rich text | Full content editor (Bold/Italic/Underline/Heading/List/Link) with a **</> HTML** source toggle. Shown before any question. |
| Thank-you message | Rich text | Same editor; shown after submit, with optional redirect. |
| Redirect link / after | Text + number | Optional post-submit redirect URL and delay in seconds. |
| Question layout | Dropdown | All questions on one page · **One page per section** (default) · One question per page · A set number per page. Choosing **one question per page** or a **set number per page** warns that it may disturb the respondent experience before it is applied. |
| Survey active period (expiry) | Duration (days / hours) | Optional; **how long the survey keeps collecting once it is sent** (not a calendar date). When the active period elapses the survey stops collecting on the live report; late responses are retained in the M-07 post-expiry store. |
| Shuffle questions | Toggle + mode | Randomise order; mode = Random shuffling · Prioritize low-response questions. Mutually exclusive with routing (see F9). |
| Appearance | Section | Inherit Tenant Design Guidelines or Customize (see F4). |

<a id="f3-functional-requirements"></a>
### Functional requirements

| **ID** | **Requirement** |
|---|---|
| FR-3.1 | Survey name is English-only here; Arabic and all other translations are entered in the Translate workspace (F11). |
| FR-3.2 | Welcome and Thank-you are full rich-text editors supporting bold/italic/underline, heading, bulleted list and link, plus a source toggle to edit raw **HTML**. |
| FR-3.3 | Question layout controls pagination independently of sections and Questions Sets. Selecting **one question per page** or **a set number per page** first warns the author that the layout may disturb the respondent experience. **Answer routing is only available with the one-question-per-page layout** (see F9). |
| FR-3.4 | The survey's **active period** is captured as a duration in **days and hours** measured from the moment the survey is sent; when it elapses the survey stops collecting on the live report and further responses are stored in M-07 (post-expiry). An empty active period means the survey does not auto-expire. |
| FR-3.5 | Shuffle exposes a mode selector (Random · Prioritize low-response); enabling routing (F9) disables shuffle, and routing is available only with the one-question-per-page layout. **Completion time is recorded automatically for every response** (there is no author toggle), so the report's median-time metric is always available. |
| FR-3.6 | For a **new** survey the build-method chooser (F5) is shown **before** the settings screen; the settings screen is then reachable from a library row click, from the build flow, and from a **Survey settings** button inside the builder. When editing, **Continue** returns to the builder rather than the build-method chooser. |

| **ID** | **Business rule** |
|---|---|
| BR-3.1 | When a survey's **active period** elapses it stops appearing in the live survey report. Responses that still arrive afterwards are **not discarded**: they are retained in a dedicated **post-expiry response store** page in the reporting module (M-07), keeping late feedback out of the live report while preserving it for audit and later analysis. |
| BR-3.2 | Only the English name is required to proceed; translations may be completed later. |
| BR-3.3 | **Journey binding is optional.** A survey with a bound journey is **Transactional** (its KPI questions can bind to that journey's stages/touchpoints); a survey with **no** bound journey is **Seasonal / Relational** and its KPI questions are journey-independent. The survey type is kept consistent with the journey binding automatically. |

<a id="f4-appearance-live-preview"></a>
## 3.4 F4 — Appearance & live preview

> **Business context —** Brand consistency matters in regulated sectors, but teams also need room to tailor a specific campaign. Appearance lets a survey inherit the tenant's design system by default or unlock a full custom theme, while a docked live preview shows the exact respondent experience as each token changes — removing guesswork and repeated "publish-and-check" cycles.

<a id="f4-controls-behaviour"></a>
### Controls & behaviour

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Theme mode | Radio | **Use Tenant Design Guidelines** (default, inherited) or **Customize this survey** (unlocks all tokens). |
| Branding / Colors | Fields | Survey logo (upload/clear), Primary and Text colours. |
| Buttons | Fields | Button radius, button border/colour, button text colour. |
| Header / Footer | Fields | Show logo / show title / header alignment; footer text. |
| Background | Fields | Placed **beside Footer** (top-right) to reduce scroll — type (Solid/Gradient/Image/Pattern), gradient stops/angle, image, opacity. |
| Advanced | Section | Status colours, surfaces (background/card/border), typography (heading/body fonts), layout (card radius, progress bar style). |
| Live preview | Panel | Docked to the **right**, **frozen (sticky)** while the controls column scrolls; **Desktop/Mobile** toggle; rendered at a reduced ratio to minimise scrolling; shows the welcome message and questions with the live theme. |

| **ID** | **Requirement** |
|---|---|
| FR-4.1 | Inherited mode renders all controls read-only/locked; Customize unlocks every token and applies changes live. |
| FR-4.2 | The live preview is pinned to the right and remains visible while the controls list scrolls; it offers a Desktop and a Mobile device frame. |
| FR-4.3 | The preview reflects the current theme (inherited or customized) and the survey's welcome message, updating instantly on any change. |
| FR-4.4 | The chosen appearance (inherited, or the full set of customized theme tokens) is part of the survey's saved settings and is included when the survey is **saved as a template** (F7), so a template reproduces branding as well as questions. |

<a id="f5-build-method"></a>
## 3.5 F5 — Build method

> **Business context —** New surveys start in different ways depending on the manager's intent and expertise: from a blank canvas for full control, from a vetted template for speed and consistency, or with AI assistance for a fast first draft. Offering the choice up front reduces friction for both novice and expert authors.

| **ID** | **Requirement** |
|---|---|
| FR-5.1 | Presents three paths: **From scratch**, **From a template**, and **Build with AI**. |
| FR-5.2 | "From a template" opens the template picker (F6); "From scratch" opens the empty builder; AI opens the assisted flow. |
| FR-5.3 | This step is shown only when creating a new survey; editing or cloning bypasses it and opens the builder directly. |
| FR-5.4 | For a new survey the build-method chooser is the **first** step and **precedes** the Survey Settings screen; the selected method (scratch / template / AI) determines the seed content before settings are configured. |

<a id="f6-choose-a-template"></a>
## 3.6 F6 — Choose a template

> **Business context —** Templates encode the organisation's best-practice surveys so teams don't reinvent them. Built-in templates are curated by the platform per sector and are locked; customized templates are the tenant's own, labelled with their tags for quick recognition. Searching by tag helps managers find the right starting point fast.

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Search / Type / Sector | Controls | Search matches name and **tags**; Type = All/Built-in/Customized; Sector filter retained for built-in metadata. |
| Card — built-in | — | Shows a **Built-in** badge + padlock and its **sector** chips. |
| Card — customized | — | No class label beyond a subtle "Customized"; shows its **tag** chips instead of sectors. |
| Order | — | **Customized templates are listed first**, then built-in. |
| Action | Buttons | **Preview** the template's survey, and **Use as template** → creates a new survey pre-loaded with the template's **settings and questions** (no journey bindings). |

| **ID** | **Requirement** |
|---|---|
| FR-6.1 | Templates are sorted customized-first; built-in cards show sector chips + padlock, customized cards show tag chips. |
| FR-6.2 | Search matches template name and tags; Type and Sector filters apply. |
| FR-6.3 | "Use this template" starts a new survey seeded with the template's **saved settings and questions** (collection behaviour, appearance and messages included); **journey/stage/touchpoint bindings are not copied** and are re-resolved for the new survey. |
| FR-6.4 | From the template picker a manager can **preview** a template's survey before choosing it, without creating a survey. |

<a id="f7-template-authoring"></a>
## 3.7 F7 — Template authoring

> **Business context —** Tenants build their own reusable templates to standardise recurring surveys (e.g., a post-transaction pulse). A template stores the source survey's **settings and questions** (collection behaviour, appearance, messages, questions and KPI links) so reuse reproduces the whole survey — everything except **journey/stage/touchpoint bindings**, which are deliberately omitted and re-resolved when the template is later used to create a survey.

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Template name | Text | English name; Arabic name field for localization. |
| Description | Textarea | What the template is for. |
| Tags | Chips | Comma-separated; power search and appear on customized cards. |
| Class / Primary sector | — | **Removed** from the form — these remain only as **list filters** and are properties of built-in templates. |
| Actions | Buttons | New template → Build survey (empty). Editing a template → **Edit questions** (opens the builder with the template's questions), Save settings, Delete. |

| **ID** | **Requirement** |
|---|---|
| FR-7.1 | Built-in templates **cannot be edited** (attempting to edit surfaces a notice); only customized templates are editable. |
| FR-7.2 | Editing a customized template exposes **Edit questions**, opening the builder in template context with its questions loaded. |
| FR-7.3 | Class and Primary-sector inputs are not part of the authoring form; they persist as filter facets for built-in templates. |
| FR-7.4 | Saving a survey as a template captures its **settings and questions** (collection behaviour, appearance, welcome/thank-you messages, sections/Questions Sets and KPI links) and **strips all journey/stage/touchpoint bindings**; using the template later re-resolves those bindings for the new survey. |

<a id="f8-build-survey-the-builder"></a>
## 3.8 F8 — Build Survey (the builder)

> **Business context —** The builder is where the survey takes shape. A palette of question types (with the KPI "metric" type called out separately) feeds a canvas organised into **sections**; each section holds standalone questions and/or **Questions Sets** (rotating pools shown as sub-windows). A contextual panel exposes exactly the settings relevant to the selected question — its type and **sub-type**, an optional **comment field**, KPI binding to a journey touchpoint, and score-based follow-ups. Sections, Questions Sets and questions can all be reordered by drag-and-drop. This keeps a potentially complex authoring task approachable and fast.

<a id="f8-palette-question-types"></a>
### Palette (question types)

Ordered as: **Scale, Input Field, Single select, Multi-select, Yes/No (Boolean), Single-select matrix, Ranking**, with the **KPI question** offered separately under a "Metric" heading.

<a id="f8-canvas-cards"></a>
### Canvas & cards

- Questions are organised into **sections**; a section can contain **standalone questions** and/or one or more **Questions Sets** (rendered as sub-windows inside the section). Each section and each Questions Set exposes an actions menu — for a section: add a standalone question, add a Questions Set, add a section, delete the section; for a set: set settings (F10), add a question to the set, delete the set.
- **Sections, Questions Sets and questions can all be reordered by drag-and-drop** (questions within and across sections/sets); order persists.
- Each question card shows type/KPI badges (KPI · touchpoint, Stage → Touchpoint), Required, an optional **Comments field** badge, Reason follow-up and — when routing is enabled — a **Routing set** badge; card controls are an **answer-routing** button (routable types) and a **delete** button.

<a id="f8-contextual-settings-common"></a>
### Contextual settings — common

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Question text / Description | Text / Textarea | Question wording and optional helper text. |
| Question type | Dropdown | The primary type; type-specific settings appear below. |
| Question sub-type | Dropdown (type-dependent) | A variant of the chosen type (e.g., Single select → Radio / Dropdown; Scale → Labels / Stars / Smileys / Slider; Input Field → Text / Paragraph / Number / Date / Time / …; Matrix → Custom columns / KPI scale). Its options change with the question type. |
| Show comments field | Toggle | Adds an optional free-text comment box under the question, shown after it is answered; the comment travels with the response for M-05 text analytics. |
| Required question | Toggle | Must be answered before submitting. |

<a id="f8-kpi-connection-kpi-questions"></a>
### KPI connection (KPI questions)

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| KPI | Dropdown | From the active KPI catalogue (CSAT, NPS, CES, FCR, VFM, Agent Score, CHS). Scale follows the KPI's own settings. |
| Perspective | Dropdown | Optional drill-down dimension; options change with the KPI. |
| Bound journey | Toggle (default ON) | Reflects the KPI on the survey's **bound journey** (chosen in Details). Turning it off makes the KPI journey-independent and disables Stage/Touchpoint. |
| Stage | Dropdown | Options come from the bound journey; **None by default**. |
| Touchpoint | Dropdown | **Child of the selected stage**; None by default; disabled until a stage is chosen. |
| Allow "N/A" | Toggle | Adds a not-applicable choice to the scale. |
| Reason follow-up | Toggle + list | Show a reason prompt based on score; multi/single select; reason items; optional "Other". |

<a id="f8-type-specific-settings"></a>
### Type-specific settings

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Scale | — | Sub-type = Labels / Stars / Smileys / **Slider** (chosen in Question sub-type). Non-slider: point count (2–10) + optional per-point labels. Slider: lower/higher limits + number of steps (intermediate values auto-computed). |
| Input Field | — | Field type = Text / **Paragraph** (multi-line) / Number / Date / Time / Date-and-Time / Month. For **Text/Paragraph**, an **Apply sentiment analysis** toggle flags the answer for M-05 sentiment scoring. |
| Single select | — | Sub-type = **Radio** or **Dropdown** (chosen in Question sub-type), plus the option list. |
| Multi-select / Ranking | — | Option list (multi-choice / ordered ranking). |
| Yes/No (Boolean) | — | Editable "true"/"false" labels. |
| Single-select matrix | — | Sub-type = **Custom columns** (author columns) or **KPI scale** (columns follow the KPI scale as faces, and **rows are the selected KPI's perspectives** — each with an editable display name and removable). A row added beyond the seeded perspectives is treated as an ordinary metric of the KPI (see BR-8.3), not a new perspective; optional N/A column. |

<a id="f8-header-actions"></a>
### Header actions

**Survey settings** (opens F3), status selector, **Translate**, **Preview**, **Save as template**, **Save survey**, and — at the far right — the **Question routing** toggle (F9, available only with the one-question-per-page layout).

<a id="f8-functional-requirements"></a>
### Functional requirements

| **ID** | **Requirement** |
|---|---|
| FR-8.1 | The palette lists the question types in the specified order and offers the KPI type under a separate Metric heading. |
| FR-8.2 | **Sections, Questions Sets and questions** all support drag-and-drop reordering (questions within and across sections and sets); order persists and drives presentation. |
| FR-8.3 | The settings panel shows only the controls relevant to the selected question's type. |
| FR-8.4 | A KPI question binds to a **single touchpoint** via Bound journey → Stage → Touchpoint; Stage and Touchpoint default to None and Touchpoint is filtered by the chosen Stage. |
| FR-8.5 | Scale offers a Slider display with lower/higher limits and step count; Single select offers List/Dropdown display; Input Field offers a Paragraph (long-text) type. |
| FR-8.6 | In matrix KPI mode, rows are seeded from the selected KPI's perspectives and remain individually renamable/removable. |
| FR-8.7 | The builder header exposes Survey settings, status, Translate, Preview, Save as template, Save survey and the Question-routing toggle. |
| FR-8.8 | Every question exposes a **sub-type** whose available options are determined by its question type; changing the sub-type changes how the question is rendered/collected without changing its type (e.g., Single select → Radio or Dropdown; Scale → Labels/Stars/Smileys/Slider; Input Field → Text/Paragraph/Number/Date/Time/…). |
| FR-8.9 | Any question may enable a **comment field**: an optional free-text box shown under the question after it is answered. When enabled, the comment is collected alongside the answer and forwarded to M-05 text analytics with the response. |
| FR-8.10 | **Questions Sets are created and managed from the section's menu**, not from a question's settings; a set is added to a section, and questions are added into a set. A question's settings panel does not create sections or sets. |
| FR-8.11 | A **Text** or **Paragraph** question may be flagged with **Apply sentiment analysis**; when set, its answers are routed to M-05 for sentiment scoring (positive / neutral / negative) in addition to being stored and shown as verbatim responses in the report. The flag is off by default and has no effect on non-text question types. |

| **ID** | **Business rule** |
|---|---|
| BR-8.1 | Switching a question's type preserves compatible settings and seeds sensible defaults for the new type. |
| BR-8.2 | A KPI question with Bound journey off contributes no touchpoint binding. |
| BR-8.3 | **Adding a row to a matrix KPI question** adds a metric that counts toward the KPI's **overall** score like any other question measuring that KPI, but it is **not a new perspective**: perspectives are defined in KPI Management (M-06) and only defined perspectives receive per-perspective calculations. The added row therefore contributes to the KPI as a whole without producing perspective-level results (the KPI computation itself is owned by M-06). |
| BR-8.4 | A **Questions Set** presents each respondent with a subset of its questions — up to its configured per-sending count — chosen by its selection mode (Random or Prioritize low-response). Standalone questions in a section are always shown; only the set's pool is sampled. |

<a id="f9-answer-routing-skip-logic"></a>
## 3.9 F9 — Answer routing (skip logic)

> **Business context —** Routing keeps surveys short and relevant: a respondent is sent only to the questions that matter given their previous answer. Routing advances one question at a time, so it is **only available with the one-question-per-page layout**. Because routing and randomised order are mutually incompatible, enabling routing explicitly disables shuffling — communicated up front so the manager understands the trade-off.

<a id="f9-behaviour-fields-labels"></a>
### Behaviour, fields & labels

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Question routing | Header toggle | On the builder header. **Enabled only when the Question layout is "one question per page"**; enabling it shows a confirmation that **shuffling will be disabled**. |
| Confirmation | Modal | "Enable question routing?" → Cancel / Enable routing. On confirm, shuffle is turned off and locked, and the answer-routing icon appears on eligible question cards. |
| Eligible types | — | Single select, Scale, Yes/No (Boolean), KPI. |
| Routing editor | Modal | Title "Enable answer routing" with the question text; one row per answer ("Answer N: ‹label›") with a **Go-to** dropdown listing subsequent questions and **End survey**; **Save / Cancel**. |
| Default target | — | Each answer defaults to the **next question** in order. |

| **ID** | **Requirement** |
|---|---|
| FR-9.1 | Routing is available **only with the one-question-per-page layout**; enabling it requires confirmation and then disables (and locks) question shuffling, and switching the layout away from one-question-per-page turns routing off. Disabling routing re-enables shuffle. |
| FR-9.2 | When routing is on, an answer-routing control appears beside the delete control on eligible question cards, and cards with routing configured show a "Routing set" badge. |
| FR-9.3 | The routing editor lists each answer of the question with a target selector containing every subsequent question plus "End survey"; the default is the immediately following question. |
| FR-9.4 | Saving persists the per-answer targets on the question; the editor can be reopened to adjust them. |

| **Scenario** | **Given → When → Then** |
|---|---|
| Enable + confirm | Given shuffle is on → When routing is toggled on and confirmed → Then shuffle is switched off and locked, and routing icons appear on eligible cards. |
| Route an answer | Given a KPI question → When the manager opens routing and sets "Score 1" → "End survey" → Then a respondent answering 1 finishes the survey. |

<a id="f10-group-settings"></a>
## 3.10 F10 — Questions Set settings & low-response ordering

> **Business context —** A Questions Set shows a manageable subset of a larger pool to each respondent and lets the manager choose how that subset is picked — random for balance, or **prioritising low-response questions** to fill data gaps so every question in the bank accumulates enough responses over time. The low-response mode also influences the order in which sections are presented, so under-answered content surfaces first.

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Set title | Text | The Questions Set's title, shown as its sub-window header. |
| Description | Textarea | Optional; shown under the set title. |
| Selection mode | Dropdown | **Random** or **Prioritize low-response**. |
| Questions per sending | Number | How many of the set's questions each respondent receives (≤ the set size). |

| **ID** | **Requirement** |
|---|---|
| FR-10.1 | Each **Questions Set** stores a title, description, a **selection mode** (Random / Prioritize low-response) and a **per-sending count** (number of its questions each respondent receives). |
| FR-10.2 | Set settings are editable both from the builder (the set's menu) and from the library structure view (F2). |
| FR-10.3 | M-01 stores the selection configuration and ordering policy; the render/collection layer (M-02/M-04) executes the actual per-respondent selection at send time. |
| FR-10.4 | **Low-response ordering algorithm.** When Prioritize low-response is in effect, the platform first determines, **within each Questions Set**, the set's lowest-response question(s) among those eligible to be sent; it compares that against the response counts of the section's **standalone** questions to obtain the **section's** lowest-response question. It then compares each section's lowest-response question across the whole survey: the section holding the **survey-wide lowest** is presented **first**, the next-lowest section second, and so on. This steers respondents toward under-answered content so coverage evens out over time. |

<a id="f11-translate-workspace"></a>
## 3.11 F11 — Translate workspace

> **Business context —** Deep Arabic support is a core platform differentiator. Centralising all localizable strings — including the survey's Arabic name, welcome/thank-you messages, questions and options — in one workspace ensures nothing ships untranslated and lets a specialist localise efficiently with full RTL fidelity.

| **ID** | **Requirement** |
|---|---|
| FR-11.1 | The Translate workspace holds every localizable string: survey **Arabic name**, welcome & thank-you messages, question text/description, option labels, scale labels and reason items. |
| FR-11.2 | English is authored on the build form; Arabic (and any other language) is completed here. |
| FR-11.3 | Arabic renders full RTL; the workspace shows source and target side by side. |

<a id="f12-multi-channel-preview"></a>
## 3.12 F12 — Multi-channel preview

> **Business context —** Before publishing, managers must see the true respondent experience on the channels their customers use. The preview renders the themed survey across web and messaging channels, showing pagination, the welcome and thank-you messages, and the exact styling — catching layout or wording issues early.

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Channels | Segmented | Mobile web · **Desktop web (default)** · WhatsApp · Email. |
| Welcome / Thank-you | — | Rendered live from the Details messages — welcome first, thank-you after submit. |
| Pagination | — | Follows the Question layout (one page, per section, per question, or set number). |

| **ID** | **Requirement** |
|---|---|
| FR-12.1 | The preview opens on the **Desktop web** channel by default and offers Mobile, WhatsApp and Email. |
| FR-12.2 | The welcome message is shown before questions and the thank-you message after submit, live from the Details editors. |
| FR-12.3 | Preview pagination mirrors the configured Question layout. |

<a id="f13-survey-report"></a>
## 3.13 F13 — Survey Report

> **Business context —** The report answers "how is this survey performing?" at a glance: headline KPI gauges, response health (volume, completion, median time, touchpoints) and a per-question breakdown. A period filter lets managers compare recent windows, and composite KPIs are derived from their underlying questions so the headline number always ties back to the detail.

<a id="f13-fields-behaviour"></a>
### Fields & behaviour

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Metric cards | — | Responses, Completion rate, **Median time** and Touchpoints (Touchpoints shows the count of bound touchpoints across the journey's stages). |
| Period filter | Dropdown + custom | **Last 1 day** · Last 7 days · Last month · 3 / 6 / 9 months · Last year · Custom range — identical to Analytics; charts update live. |
| KPI Scores | Gauges | CSAT, NPS, CES gauges with target markers, band chips, period delta and response count. |
| CSAT (composite) | — | The headline **CSAT is the average of the survey's CSAT questions** (e.g., 81% and 76% → 78.5%). |
| Per-question results | Cards | Each question type has a fit-for-purpose result view (see FR-13.3): KPI → bars + gauge; single-select & Boolean → distribution **donut** with legend; multi-select → **bar chart with counts and %**; scale → aggregate **gauge** plus a visual in the question's own style (faces / stars / labels); text & paragraph → **table of recent verbatim responses** (channel + time) with a **show-more** to the last 100; number/date/time → **value-distribution line** (with an average for numeric). The whole per-question section can be hidden/shown. |

| **ID** | **Requirement** |
|---|---|
| FR-13.1 | A period filter (**1 day** / 7d / month / 3 / 6 / 9 months / year / custom) drives the report; KPI gauges, per-question values and counts update accordingly. |
| FR-13.2 | The headline CSAT gauge is computed as the average of the survey's CSAT questions and updates if those change. |
| FR-13.3 | Per-question results are rendered per question type: **KPI** → bar distribution + KPI gauge (the top-right label shows the **response count**); **single-select** and **Yes/No (Boolean)** → distribution **donut** with legend; **multi-select** → **bar chart** showing each option's **count and percentage**; **scale** → an aggregate **gauge** plus a visualisation in the question's display style (a face for Faces, filled stars for Stars, and no side chart for Labels); **text/paragraph** → a **table of individual verbatim responses** (each with its channel and submission time) — the latest few by default, with a **"show more"** control that reveals up to the **last 100 received responses**; frequent-word, theme and sentiment analysis are provided by M-05; **number/date/time** → a **value-distribution line** over the answer values (numeric additionally shows the **average**). |
| FR-13.4 | **Median completion time is always available** because completion time is recorded automatically for every response (there is no author toggle). |
| FR-13.5 | **Multi-select results are reported as counts and percentages of respondents**, and the percentages **can total more than 100%** because a respondent may choose several options; the base (number of respondents) is stated with the chart. |
| FR-13.6 | The report reflects only responses collected **within the survey's active period**; responses received after expiry are excluded from the live report and are available in the M-07 post-expiry store instead. |
| FR-13.7 | Text/paragraph questions surface a **sample of recent verbatim responses** (with channel and submission time) directly in the report — the latest few by default, expandable to the **last 100 received responses** — giving managers immediate qualitative signal; full-text listing, frequent-word, theme and sentiment analysis are owned by M-05. |

<a id="f14-analytics"></a>
## 3.14 F14 — Analytics

> **Business context —** Analytics explains the survey's reach and drop-off: how many invitations were sent, opened, started and finished, how each channel converts, and how volume trends over time. Every headline number carries an up/down deviation indicator against the previous period of equal length, so managers immediately see whether performance is improving or declining — guiding channel mix, reminder cadence and survey length decisions.

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Period / granularity | Controls | Same period options as the report (**1 day** / 7d / month / 3 / 6 / 9 months / year / custom), plus a daily/weekly/monthly granularity segment; a live "responses in period" chip. |
| Overall Completion Rate | Card | Finished ÷ Sent for the period, with a **▲/▼ delta in percentage points** vs the previous equal-length period. |
| Funnel cards | Cards + chips | Sent → Opened → Started → Finished. Each card shows: the absolute count, its **% of Sent**, and a **▲/▼ % deviation vs the previous period**. Between the cards, conversion chips show stage-to-stage rates. |
| Visual funnel | Bars | Volume bars per stage with count and % of Sent. |
| Channel breakdown | Bars | Per channel (Web, WhatsApp, Email, SMS): Sent count → completion rate, each with a **▲/▼ deviation** vs the previous period. |
| Responses trend | Line chart | Volume over the selected period/granularity. |

<a id="f14-displayed-numbers-calculations"></a>
### Displayed numbers & calculations

| **Number** | **Calculation** | **Deviation shown** |
|---|---|---|
| Sent | Count of survey invitations delivered in the selected period. | % change vs previous equal-length period (▲ green / ▼ red). |
| Opened | Invitations opened; card also shows Opened ÷ Sent as % of sent. | % change vs previous period. |
| Started | Respondents who answered ≥ 1 question; % of Sent on the card. | % change vs previous period. |
| Finished | Submitted responses; % of Sent on the card. | % change vs previous period. |
| Stage conversion chips | Opened ÷ Sent, Started ÷ Opened, Finished ÷ Started (between the funnel cards). | — |
| Overall Completion Rate | Finished ÷ Sent (period). | Delta in **percentage points** vs previous period. |
| Channel Sent / completion | Per-channel Sent count and Finished ÷ Sent for that channel. | Per-channel % deviation vs previous period. |
| Responses in period (chip) | Total finished responses within the selected period. | — |

<a id="f14-functional-requirements"></a>
### Functional requirements

| **ID** | **Requirement** |
|---|---|
| FR-14.1 | Analytics offers the same period options as the report (**including Last 1 day**) plus a granularity segment appropriate to the period; all numbers, deltas and charts recompute on period change. |
| FR-14.2 | The funnel shows Sent/Opened/Started/Finished with per-stage absolute counts, % of Sent, stage-to-stage conversion chips, and an overall completion rate. |
| FR-14.3 | Every headline number (completion rate, each funnel stage, each channel) displays an up/down deviation indicator computed against the **previous period of equal length**: ▲ (positive, green) or ▼ (negative, red), with the value in % (counts) or percentage points (rates). |
| FR-14.4 | A channel breakdown and a responses trend chart update with the selected period/granularity. |
| FR-14.5 | When no previous-period data exists (new survey), deviation indicators are suppressed rather than showing a misleading 0. |

<a id="f15-survey-approval-publishing-workflow"></a>
## 3.15 F15 — Survey approval & publishing workflow

> **Business context —** In many organisations the person who builds a survey (the Survey Administrator) is not the person accountable for what customers receive (the CX Program Manager). The approval workflow enforces that separation: a designer's survey is locked once submitted, the authorized reviewer is notified and taken straight to the survey's settings to act, and nothing reaches customers without an explicit publish decision. Where a tenant trusts its designers, an M-10 permission grant restores self-publishing without changing the module.

<a id="f15-workflow"></a>
### Workflow

1. **Draft:** The Survey Designer (P-03) creates and edits the survey freely while it is in Draft.
2. **Submit for review:** On submission the survey enters **Pending review**. From this point **P-03 cannot edit the survey** until an authorized user has reviewed it.
3. **Notification:** The authorized reviewer (P-01 CX Program Manager) receives a notification (via M-09) that a survey awaits review; opening it **redirects to the Survey Settings screen** of that survey.
4. **Action:** From Survey Settings, P-01 reviews the configuration and takes an action — **Publish** (survey becomes Active / ready for distribution) or **Return to draft** with remarks (survey becomes editable again by P-03).
5. **Self-publish grant (M-10 option):** An M-10 permission option — "Publish own surveys" — can be granted to P-03. With this grant, P-03 may publish surveys they created without P-01 review; without it, the review step is mandatory.

<a id="f15-functional-requirements"></a>
### Functional requirements

| **ID** | **Requirement** |
|---|---|
| FR-15.1 | A survey created by P-03 is saved as Draft; submitting it moves it to **Pending review** and locks it against edits by the submitter (read-only view remains available). |
| FR-15.2 | On entering Pending review, M-09 sends a notification to users holding the review/publish permission (P-01 by default); the notification deep-links to the survey's **Survey Settings** screen. |
| FR-15.3 | From Survey Settings, the reviewer can **Publish** (status → Active) or **Return to draft** (status → Draft, editable again by P-03), optionally with remarks recorded in the audit log. |
| FR-15.4 | A survey in Pending review cannot be distributed, is not sent by rules, and does not collect responses. |
| FR-15.5 | M-10 exposes a grantable permission **"Publish own surveys"** for P-03. When granted, the P-03 user may publish surveys they authored directly and the submit-for-review step is skipped. The grant is tenant-configurable per user/role. |
| FR-15.6 | All submit, publish, return and permission-grant events are written to the M-11 audit log with actor, timestamp and remarks. |

<a id="f15-business-rules"></a>
### Business rules

| **ID** | **Business rule** |
|---|---|
| BR-15.1 | The edit lock applies to the survey structure, questions, routing and details while Pending review; the reviewer (P-01) may edit before publishing. |
| BR-15.2 | Publishing requires either the reviewer permission (P-01) or the M-10 "Publish own surveys" grant (P-03, own surveys only). |
| BR-15.3 | Statuses interact with distribution rules per Section 3.1: only **Active** surveys are sent by rules. |

| **Scenario** | **Given → When → Then** |
|---|---|
| Standard review | Given P-03 submits a draft → When P-01 opens the notification → Then P-01 lands on the survey's Survey Settings screen and can Publish or Return to draft. |
| Locked while pending | Given a survey Pending review → When P-03 opens it → Then all editors are read-only with a "Pending review" banner. |
| Self-publish grant | Given P-03 holds "Publish own surveys" → When they finish a draft → Then a Publish action is available to them directly and no review notification is sent. |

<a id="data-model-m-01-owned"></a>
# 4. Data model (M-01 owned)

Standard audit fields (id, tenant_id, created_at/by, updated_at/by) are present on all entities and omitted below for brevity.

<a id="dm-survey"></a>
## 4.1 Survey

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| name_en | string | Required. English survey name. |
| description | text | Internal description. |
| survey_type | enum | Transactional · Seasonal / Relational. |
| bound_journey | ref → M-16 Journey | **Nullable (optional).** Drives KPI stage/touchpoint options. **Null ⇒ survey_type = Seasonal / Relational; set ⇒ Transactional** (kept in sync). |
| status | enum | Draft · Pending review · Active · Paused · Archived. Pending review is entered via the approval workflow (Section 3.15). |
| rules_count | int (derived) | Number of connected distribution rules (owned by M-02); drives the pause confirmation (FR-1.10). |
| submitted_by / reviewed_by | ref → user | Approval workflow attribution (Section 3.15). |
| theme_mode | enum | inherited · customized. |
| welcome_html / thanks_html | html | Rich-text messages (source-editable). |
| redirect_url / redirect_after_s | string / int | Optional post-submit redirect. |
| layout | enum | single · **section** · question · count (+ questions_per_page). Routing requires the `question` layout. |
| active_period {days, hours} | duration | Optional. **How long the survey collects after being sent** (not a calendar date). On elapse, collection stops on the live report and later responses go to the M-07 post-expiry store. |
| record_time | bool (always true) | Completion time is **always** recorded; there is no author toggle. Powers the median-time metric. |
| shuffle / shuffle_mode | bool / enum | random · low_response. Mutually exclusive with routing_on. |
| routing_on | bool | When true, shuffle is disabled. |

<a id="dm-group-section"></a>
## 4.2 Section & Questions Set

**Section**

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| name / description | string / text | Section title and optional description. |
| order | int | Position within the survey (drag-and-drop). |
| questions[] | list → Question | Standalone questions in the section. |
| sets[] | list → Questions Set | Zero or more Questions Sets in the section. |

**Questions Set**

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| title / description | string / text | Set title and optional description. |
| selection_mode | enum | random · low_response. |
| count | int | Questions shown per sending (≤ number of questions in the set). |
| order | int | Position within its section (drag-and-drop). |
| questions[] | list → Question | The set's pool. |

<a id="dm-question-type-specific"></a>
## 4.3 Question (+ type-specific)

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| type | enum | Scale · Input Field · Single select · Multi-select · Yes/No (Boolean) · Single-select matrix · Ranking · KPI. |
| text / description / required | string / text / bool | Common attributes. |
| subtype | enum (type-dependent) | Rendering/collection variant of the type (e.g., Single select → radio/dropdown; Scale → labels/stars/smileys/slider; Input → text/paragraph/number/date/time/…; Matrix → custom/kpi). |
| comments | bool | When true, an optional free-text comment box is shown under the question after answering; the comment is collected with the response (M-05). |
| sentiment | bool | Text/Paragraph only: when true, answers are flagged for M-05 sentiment scoring. Default false. |
| section_id / set_id / order | ref / ref / int | The question's section, its Questions Set (if any), and position (drag-and-drop). A standalone question has set_id = null. |
| KPI: kpi, perspective, bound_journey, stage, touchpoint, allow_na | — | Bound journey default true; stage None; touchpoint child of stage. |
| Scale: scale_view, scale_points, scale_labels[], slider_min/max/steps | — | scale_view ∈ labels/stars/smileys/slider. |
| Input: input_type | enum | Text · Paragraph · Number · Date · Time · DateTime · Month. |
| Single select: subtype, options[] | enum/list | subtype ∈ radio (list) · dropdown. |
| Boolean: bool_true, bool_false | string | Answer labels. |
| Matrix: matrix_mode, matrix_kpi, matrix_rows[](=perspectives in KPI mode), matrix_cols[], allow_na | — | KPI mode seeds rows from KPI perspectives; **rows added beyond the seeded perspectives count toward the KPI overall but are not new perspectives** (BR-8.3). |
| reason {on,prompt,mode,items[],other} | object | Score-based follow-up (KPI). |
| routing {answerKey → target} | map | Target is a question id or "__end". |

<a id="dm-theme-template-translation"></a>
## 4.4 Theme, Template, Translation

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Theme | object | Per-survey tokens (colours, fonts, radius, header/footer, background) when customized; else inherited from Tenant Design Guidelines (M-11). |
| Template | entity | class (Built-in/Customized), name_en/ar, description, tags[], sectors[] (built-in), **settings snapshot** (collection behaviour, appearance, messages, sections/Questions Sets) and questions[]. **No journey/stage/touchpoint bindings.** |
| Translation | per-locale | Arabic name, welcome/thanks, question/option/scale/reason strings; RTL. |

<a id="permissions-rbac-extends-m-10"></a>
# 5. Permissions (RBAC — extends M-10)

| **Action** | **P-01 CX PM** | **P-03 Survey Admin** | **P-02 Analyst** | **P-06 Exec** |
|---|---|---|---|---|
| View library / preview | ✓ | ✓ | ✓ | ✓ |
| Create / edit draft survey, sections, Questions Sets, questions | ✓ | ✓ (own drafts) | — | — |
| Submit survey for review | — | ✓ | — | — |
| Review / publish / return to draft | ✓ | ✓ only with "Publish own surveys" grant (own surveys) | — | — |
| Edit a survey in Pending review | ✓ | — | — | — |
| Configure KPI binding & routing | ✓ | ✓ (own drafts) | — | — |
| Edit appearance / customize theme | ✓ | ✓ (own drafts) | — | — |
| Author customized templates | ✓ | ✓ | — | — |
| Change status / pause / archive | ✓ | — | — | — |
| View report & analytics | ✓ | ✓ | ✓ | ✓ (summary) |

The **"Publish own surveys"** grant is an M-10 permission option configurable per user/role at tenant level; without it P-03's surveys must pass P-01 review (Section 3.15). Permissions are enforced at both UI (controls hidden/disabled) and API (403) levels; every change is attributed in the M-11 audit log.

<a id="non-functional-requirements"></a>
# 6. Non-functional requirements

- **Performance:** list and builder load within 1.5 s on standard tenant volumes; live preview and configuration updates render within ~100 ms of a change.
- **Localization:** full EN/AR parity and RTL for every screen, control, tooltip, message and preview.
- **Accessibility:** WCAG 2.1 AA — colour is never the sole signal (badges/labels accompany status and bands); controls are keyboard-reachable.
- **Isolation & audit:** strict tenant isolation; all create/edit/status/delete actions audit-logged to M-11.
- **Resilience:** autosave-friendly drafts; unsaved-changes guards when navigating away from edited sections.
- **Forward compatibility:** data model and API contracts designed to add channels, question types and languages by configuration.

<a id="localization-rtl"></a>
# 7. Localization & RTL

- English is authored inline; Arabic and other locales are completed in the Translate workspace (F11), including the survey's Arabic name.
- All UI labels, helper texts, validation messages and confirmations have EN and AR versions.
- In Arabic the entire layout — builder panes, appearance controls, gauges, modals and previews — renders RTL.

<a id="glossary"></a>
# 8. Glossary

| **Field / Control** | **Type** | **Rules, defaults & labels** |
|---|---|---|
| Bound journey | — | The journey a survey (and its KPI questions) reflects; chosen in Details, toggled per KPI question. |
| Perspective | — | A drill-down dimension of a KPI; used as matrix rows in KPI matrix mode. |
| Reason follow-up | — | A conditional prompt shown based on a KPI answer/score. |
| Routing | — | Skip logic mapping each answer to a next question or survey end. |
| Selection mode | — | How a Questions Set's displayed subset is chosen: Random or Prioritize low-response. |
| Inherited / Customized theme | — | Survey styling from Tenant Design Guidelines vs per-survey overrides. |
| Pending review | — | Approval-workflow state: the survey is locked for its author until an authorized user publishes it or returns it to draft. |
| Distribution rule | — | An M-02 automation (e.g., post-transaction trigger) that sends a survey; fires only while the survey is Active. |
| Section | — | A survey block holding standalone questions and/or Questions Sets; reorderable. |
| Questions Set | — | A rotating pool of questions inside a section; each respondent sees a subset by the set's selection mode and per-sending count. |
| Active period | — | Duration (days/hours) a survey collects after being sent; on elapse it stops on the live report and late responses go to the M-07 store. |
| Question sub-type | — | A type-dependent rendering variant of a question (e.g., single-select radio vs dropdown). |
| Comment field | — | Optional free-text box under a question, collected with the response for M-05. |
| Sentiment flag | — | An author flag on a Text/Paragraph question that routes its answers to M-05 for positive/neutral/negative scoring. |
| Post-expiry store | — | The M-07 reporting page that retains responses received after a survey's active period ends. |

<a id="change-log-prototype-iterations-reflected"></a>
# 9. Change log — prototype iterations reflected

<a id="batch-a-foundations"></a>
### Batch A — foundations

<a id="batch-b-builder-routing-report"></a>
### Batch B — builder, routing, report

<a id="batch-c-placement-polish"></a>
### Batch C — placement & polish

<a id="batch-d-report-maths-library-ux"></a>
### Batch D — report maths & library UX

<a id="batch-e-routing-navigation"></a>
### Batch E — routing & navigation

<a id="batch-f-approval-rules-analytics-deltas"></a>
### Batch F — approval, rules & analytics deltas

<a id="batch-g-sections-sets-seasonal-active-period-report-types-current"></a>
### Batch G — sections & Questions Sets, seasonal binding, active-period expiry, report result types

- **Survey structure** reworked from flat "groups" to **sections** that contain standalone questions and/or **Questions Sets** (rotating pools with a selection mode and a per-sending count). Sections, sets and questions are drag-and-drop reorderable. "Survey details" is renamed **Survey settings**; the build-method chooser now precedes settings for new surveys. (F2, F3, F5, F8, F10, §4.2)
- **Low-response ordering** specified end-to-end: the least-answered question is found inside each Questions Set, compared with the section's standalone questions, then compared across sections so the survey-wide lowest-response section is presented first. (FR-10.4)
- **Journey binding is optional**: no journey ⇒ the survey is **Seasonal / Relational**; a bound journey ⇒ Transactional (kept in sync). (F3, BR-3.3, §4.1)
- **Expiry redefined as an active period in days/hours** measured from send. Post-expiry responses are retained in a dedicated **M-07 post-expiry store** rather than the live report. (F3, BR-3.1, §2.1, §4.1)
- **Completion time is always recorded** (the Save-response-time toggle is removed); the median-time metric is always available. (F3 FR-3.5, F13 FR-13.4, §4.1)
- **Question sub-types** added (type-dependent variant, e.g., single-select radio/dropdown, scale labels/stars/smileys/slider); redundant display toggles folded into the sub-type. (F8 FR-8.8, §4.3)
- **Per-question comment field** option: an optional free-text box shown after answering, collected with the response for M-05. (F8 FR-8.9)
- **Report per-question result types** specified per question type: donut for single-select/Boolean, bar with counts + % for multi-select (percentages may exceed 100%), aggregate gauge + style visual for scale, most-frequent-words table for text/paragraph, and a value-distribution line (with average for numeric) — plus a **Last 1 day** period option. (F13 FR-13.1/13.3/13.5, F14)
- **Matrix KPI rows**: a row added beyond the seeded perspectives counts toward the KPI overall but is **not** a new perspective (perspectives are owned by M-06). (BR-8.3)
- **Templates** now store the survey's **settings and questions** (not just questions); the template picker offers a **preview**. (F6, F7)
- **Archived is terminal**: an archived survey can only be **unarchived to Draft** — no direct move to Active/Paused. **Theme column removed** from the library; lists show the **English name only**. (F1 BR-1.3, FR-1.14, FR-1.7)
- **Routing** clarified as available **only with the one-question-per-page layout**. (F9, F3)

<a id="batch-h-verbatim-sampling-sentiment-current"></a>
### Batch H — verbatim response sampling & text sentiment flag (current)

- **Text/paragraph report view** changed from a most-frequent-words table to a **table of verbatim responses** (each with channel and submission time): the latest few are shown by default, and a **“show more”** control reveals up to the **last 100 received responses**. Frequent-word, theme and sentiment analysis remain owned by M-05. (F13 FR-13.3, FR-13.7; §2.1)
- **Apply sentiment analysis** flag added to **Text/Paragraph** question settings; when enabled the answers are routed to M-05 for positive/neutral/negative scoring. (F8 FR-8.11; §4.3; §2.1)
- Defect fix (no business change): KPI question settings failed to render for newly added or newly converted KPI questions because the reason-follow-up object was not initialised; KPI questions now always carry that object so their settings panel renders reliably.
