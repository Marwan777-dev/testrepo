---
name: clickthrough-parity
description: >-
  Compare a spec-kit phase's just-implemented frontend pages in `frontend/` against the standalone
  click-through design repo (the Nabadat click-through, maintained separately) and drive them to match — layout,
  text, every element, every placeholder, control type, and behaviour. Run it after finishing a
  page-bearing (frontend) phase of a spec-kit feature to catch drift from the click-through before
  it compounds: missing or extra elements, wrong control (switch vs checkbox), changed copy or
  placeholders, reordered layout. Produces a per-route parity report grouped by
  Layout / Text / Controls / Behaviour, PLUS a bidirectional "Needs discussion" list (anything one
  side has that the other doesn't — including an element placed on a different page than the design,
  which is flagged for a business decision, not silently changed). Runs report-first and always ends
  with the exact `--fix` command to apply the defects; `--fix` applies them and re-checks until
  parity. Scope narrows or widens by argument: a phase checks that phase's routes, while a bare
  feature (no phase) audits the whole finished module's page-bearing routes in one pass. THE
  CLICK-THROUGH IS THE SOURCE OF TRUTH; `frontend/` must match it unless business rules say otherwise.
  Use when asked to check/enforce click-through parity, "does the page match the design", "compare the
  real page to the prototype", or after `/speckit-implement` finishes a frontend phase (its E2E
  checkpoint fires this).
argument-hint: "<feature-or-phase> [--fix]  e.g. 005 phase 4 (one phase) | 005-action-management (whole module) | actions --fix"
user-invocable: true
disable-model-invocation: false
---

## User Input

```text
$ARGUMENTS
```

The argument names WHICH frontend work to check and whether to fix. Accept any of:
a feature folder (`005-action-management` / `005` / `action-management`), a specific phase
(`005 phase 4`), or a feature/area keyword (`actions`, `journeys`, `kpi`). `--fix` (or "and fix
them") switches on fix mode. **Scope follows the argument shape:** a **phase** (`005 phase 4`) checks
only that phase's routes — the per-phase implementation case; a **bare feature** with no phase
(`005-action-management`, or "whole module" / "full module" in prose) widens to **ALL** of the
module's page-bearing routes at once — the retro-audit of a **finished** module. No argument → infer
from the current branch / most recently touched `specs/NNN-*/tasks.md` phase, and confirm with the
user before doing heavy work.

---

# Click-through parity — the real page must match the design

> 📖 **Humans:** see **[USAGE.md](USAGE.md)** in this folder for the developer guide — copy-paste
> commands (report / `--fix` / phase / whole module), the parameter grammar, stack + token setup,
> a troubleshooting table, and a worked example. Keep it updated when a run teaches you something new.

**The standalone click-through repo is the source of truth.** The business team hands a full-cycle
HTML per module; the frontend lead translates it into React pages and keeps refining them in the
**click-through repo** (the separate Nabadat click-through — the same prototype codebase, its own
git repo/branch, NOT this product repo). When development implements a page under `frontend/` via
spec-kit, that page must reproduce the click-through **exactly** — layout, order, spacing, every
label, hint, badge, empty-state, placeholder, and the correct control type (a toggle is a toggle,
never a checkbox). Nothing in the design may be forgotten, and nothing extra may be silently invented.

> ⚠️ Do NOT **auto-discover** or silently fall back to a `clickthrough-reference/` folder inside this
> product repo — it is a stale copy, not the maintained source of truth. Only ever use the reference
> the developer **explicitly** supplies via `CLICKTHROUGH_DIR` / `CLICKTHROUGH_BASE_URL` (step 2).
> That explicit path MAY point at a local copy — even `clickthrough-reference/` — e.g. while testing
> this skill; that's fine because it's deliberate. Just never assume/default to it.

## ⚠️ Implementation must be CLICK-THROUGH-BLIND, or this skill measures nothing

**The implementing session must NOT open, read, or copy from the click-through checkout.** Frontend
pages are built from `spec.md` + `tasks.md` + the design system in the root `CLAUDE.md`. The
click-through is the **audit** reference, read only by *this* skill, **after** the story's E2E
checkpoint.

A parity run only carries information when the two sides were produced **independently**. If the
implementation was ported from the reference, the run diffs a file against the file it was copied
from and can only ever report "identical" — a rubber stamp that hides real drift. **A run whose
implementation was derived from the reference is VOID, not clean:** report it as **NOT AUDITED**,
never as "0 defects", and say which files were copied.

This is an instruction-level control, not a technical gate — the checkout is just a directory. So
**every report opens with the provenance declaration** (step 5). If you cannot state truthfully that
the implementation was click-through-blind, the honest result is *not audited*. Do not let a
zero-defect report stand in for an audit that never happened. (Observed 2026-09-02 → 09-03: four
M-13 routes were ported and then reported "0 defects" across two sessions, and a "consecutive
0-defect" streak was recorded in `route-map.md` as if it were a quality signal. It measured nothing.
Both are now re-classified NOT AUDITED.)

This skill does what the human "Careful HTML-to-React" review does, but automatically and **per
phase** — a phase touches a small, known set of routes, so the diff is small, fast and reliable, and
you catch drift while the implementer's context is still warm (far better than diffing a whole module
at the end).

Both codebases are Vite + React 19 + Tailwind 4 + `@base-ui/react`, sharing the same `ui/*` + `cx/*`
components and a common token base (`frontend/src/index.css` is a *refined superset* of the
click-through's tokens). So parity is about **layout & composition & content**, not re-importing tokens.

---

## What MUST match vs. what MAY differ

**MUST match the click-through (report as a defect, fix under `--fix`):**
- Element **presence and order** — sections, cards, rows, fields, buttons, tabs, banners, icons,
  dividers, badges, empty states — top-to-bottom and left-to-right.
- **Every string** — headings, labels, hints/help text, descriptions, badge text, button labels,
  empty-state copy, and **input placeholders** — verbatim.
- **Control type** per field — `switch` ↔ `switch`, `checkbox` ↔ `checkbox`, `radio`, `select`,
  `input` (and its `type`), `textarea`, `tabs`. The switch-vs-checkbox swap is the #1 recurring bug.
- **Layout relationships** — vertical vs horizontal stacking, grouping into the same container,
  columns, alignment, and gross spacing/width tiers. Do not "improve" or reflow the layout.
- **States** — disabled, selected, required (`*`), pending, error, loading affordances that the
  click-through shows.

**MAY legitimately differ (do NOT flag as a defect):**
- **Data values** — the click-through renders mock data; `frontend/` renders real/backend data. A
  different customer name, count, date, or list length is expected. Compare the *template/label*, not
  the value (e.g. both must have a "Response rate" field; the number may differ).
- **Backend-driven emptiness** — a real list that is empty because the backend has no rows yet, where
  the mock had seeded rows, is a data difference, not a layout defect (but the empty STATE, if the
  design defines one, must still render).
- Anything the click-through itself marks "coming soon"/deferred that the phase legitimately hasn't
  built yet — note it, don't fail on it.

**NEEDS DISCUSSION (never auto-fix — surface for a human decision):**
The governing question is **"drift from the design, or a deliberate business rule?"** — and it applies
to **ANY axis**, not one special kind of difference. A difference is Needs-discussion (not a defect)
whenever the implementation may be intentionally diverging from the click-through because the business
told it to. **The click-through is still the default truth; but if business is right, the code stays
and the *click-through* is what gets updated — a valid, accepted outcome, not a defect.** This can show
up on any axis:
- **Presence — click-through has X, `frontend/` doesn't.** "Dev forgot it" (usually add it) OR
  intentionally out of this phase's scope. Ask, don't assume.
- **Presence — `frontend/` has Y, the click-through doesn't.** Scope creep, a backend-required control
  with no design yet, or a genuine improvement the design should adopt. Ask; the design may need to
  be updated instead of the code.
- **Placement — same element, different page.** e.g. the implementation puts an action button *on the
  page* while the click-through has it on an **inner** page (drawer / detail / settings sub-page) or not
  at all. Report exactly where each side puts it. (Detecting this reliably needs the whole module's page
  map, so it is strongest at whole-module scope — a bare feature with no phase, see Scope.)
- **Control / behaviour / copy / layout — same intent, different realisation.** A different control
  (a Select where the design uses a segmented toggle), a changed flow (an extra confirm step, a
  different empty-state action), a reworded label or a restructured layout — whenever it reads like a
  **considered business choice** rather than a slip. These are just as much "business, or drift?" as
  placement is; the placement case is only the most common **example**, not the only one.

**These are examples of the rule, not a closed list.** When a difference on *any* axis could be a
deliberate business decision, collect it into the **"Needs discussion"** section, state the business
question plainly ("business rule, or drift from the design?"), and STOP before changing it — even in
`--fix` mode. Only differences that are clearly mechanical drift (no plausible business reason) are
defects that `--fix` may touch. When unsure which bucket a difference is in, put it in Needs
discussion, not defects.

---

## Procedure

### 0 · Resolve the scope
**Scope follows the argument: one phase, or the whole module (a bare feature with no phase).**
- **Phase (`005 phase 4`):** only the routes THIS phase shipped — small, fast, warm-context. This is
  what the `/speckit-implement` per-frontend-phase checkpoint fires.
- **Whole module (`005-action-management`, no phase):** EVERY page-bearing route of the module, in one
  pass. Use it to retro-audit a **finished** module, and because the cross-page **placement** checks
  (see Needs discussion) need the module's full page map. **Manual only — never auto-run.**

1. Identify the **feature** (`specs/NNN-*/`) and, for phase scope, the **phase(s)** from `$ARGUMENTS`
   (or the current branch / most-recent phase). Read that feature's `tasks.md`.
2. Confirm **page-bearing (frontend)**: the phase header is tagged `… frontend` (e.g.
   `## Phase 4: User Story 2 … — [Atia backend / Marawan frontend]`), it has tasks touching
   `frontend/src/…`, and it carries an `E2E (Browser) Tests 🎭` subsection. If backend-only, report
   `no frontend pages in this phase — skipped` and stop. **At whole-module scope (bare feature),
   gather EVERY page-bearing phase of the module and union their routes.**
3. Extract the **pages/components** in scope (`frontend/src/features/<f>/pages/*.tsx`, shared
   `components/*`) and their **routes** (grep `frontend/src/App.tsx` → route paths; the route comments
   tag the `M-NN` module). Phase scope = that phase's routes; whole-module (bare feature) = all of the
   module's routes.

### 1 · Map each real route → its click-through page
- Keep a persistent map next to this skill at **`.claude/skills/clickthrough-parity/route-map.md`**
  (create it if missing) — a table of `M-NN module | real route | real file | click-through route |
  click-through file`. Read it first; it makes each run deterministic.
- For a route not yet in the map, derive the pair: match by the `M-NN` module tag (from the
  `frontend/src/App.tsx` route comment and the spec/SRS) and the page's role (list / builder / detail /
  settings / wizard-step), then find the matching page in the click-through checkout
  (`<CLICKTHROUGH_DIR>/src/pages/*.tsx` and its route in `<CLICKTHROUGH_DIR>/src/App.tsx`).
  **Append every pair you resolve to `route-map.md`** so the map grows with each module.
- Produce a concrete list of route pairs: `{ realRoute, realFile, ctRoute, ctFile }`.

### 2 · Bring up both apps (distinct ports)

**First, read `reference.json` in this skill folder** (gitignored; template
`reference.example.json`). When present it supplies `clickthroughDir`, `clickthroughBaseUrl`,
`frontendBaseUrl` and a default `persona` — so an automatic run (the `/speckit-implement`
`after_implement` hook, which carries no paths) does not have to stop and ask. Precedence:
**paths named in the prompt win**, then `reference.json`, then **ASK**. A relative
`clickthroughDir` resolves from this repo's root.

This file is the developer supplying the reference explicitly, which is exactly what the
guardrail requires — it does NOT weaken it. With no prompt paths and no `reference.json`, still
**ASK**; never guess, and never fall back to this repo's stale `clickthrough-reference/` folder.

Both default to Vite `5173`, so they cannot share a port.
- **Click-through** (source of truth): the developer supplies it — either a local checkout path
  `CLICKTHROUGH_DIR` (a clone of the Nabadat click-through repo, on the design branch, e.g. `ba-farah`)
  or an already-deployed URL `CLICKTHROUGH_BASE_URL`. If given a checkout, start it yourself:
  `cd "$CLICKTHROUGH_DIR" && (npm ci || npm install) && npm run dev -- --port 5175 --strictPort`.
  It is static + mock data with a **stub login** (any email/password, then a "Skip for now" button).
  If neither is supplied, ASK for the click-through path/URL — do not guess or auto-fall-back to a
  `clickthrough-reference/` folder in this repo. (The developer MAY point `CLICKTHROUGH_DIR` at such a
  folder explicitly, e.g. for testing — that's allowed precisely because it is explicit.)
- **Real frontend**: target an **already-running** instance (like the `e2e-testing` skill) — the dev
  stack (backend + `frontend/` on its usual port) must be up so pages actually render. Take its URL
  as `FRONTEND_BASE_URL` (default `http://localhost:5173`; DERIVE the scheme from how Vite serves it —
  HTTP for `frontend/portal`-style, HTTPS if a `basicSsl()` plugin is configured — a scheme mismatch
  breaks the first navigation). The real app is auth-gated (`/login` → MFA/TOTP →
  `sessionStorage.session_token`); reuse the E2E creds (`E2E_USER` / `E2E_PASSWORD` /
  `E2E_TOTP_SECRET` from the gitignored `appsettings.local.json`) OR inject a valid
  `sessionStorage.session_token` grabbed from a signed-in browser. If real pages won't authenticate,
  say so and ask the developer to provide a running, signed-in dev stack — don't fake the comparison.

### 3 · Capture both sides
Use the bundled **`capture.mjs`** (Playwright) — it screenshots and extracts a structural+text
snapshot per route so the comparison is not eyeballing alone:

> One-time setup (self-contained in the skill folder - the dev repo root has no `package.json`):
> `cd .claude/skills/clickthrough-parity && npm install && npx playwright install chromium`

```sh
# click-through (stub auth)
node .claude/skills/clickthrough-parity/capture.mjs \
  --base http://localhost:5175 --auth stub \
  --routes '<refRoute1>,<refRoute2>' --out <scratch>/ref

# real frontend (token or dev auth)
node .claude/skills/clickthrough-parity/capture.mjs \
  --base "$FRONTEND_BASE_URL" --auth token --token "$SESSION_TOKEN" \
  --routes '<realRoute1>,<realRoute2>' --out <scratch>/app
```

For each route it writes `<slug>.light.png`, `<slug>.dark.png`, and `<slug>.json` (the structural
fingerprint: page title, headings, section titles, form labels, input placeholders, **control types**,
button labels, badge/pill text, table headers, tab labels, empty-state text). Capture both themes;
capture RTL too when the page is bilingual (`--dir rtl`).

### 4 · Compare — per route, on four axes + presence
For each route pair, read BOTH `.json` fingerprints and VIEW both `.png`s (this is where your vision
matters — the screenshots catch spacing/order/alignment the JSON can't):
1. **Layout** — element order and vertical/horizontal grouping; column structure; obvious spacing/
   width/alignment drift. (screenshots + structural order)
2. **Text** — diff every string verbatim, **including placeholders** and empty-state copy. Ignore
   pure data-value differences (see the MUST/MAY rules above).
3. **Controls** — diff the control TYPE per field; a design `switch` implemented as a `checkbox`
   (or `select` as `input`, etc.) is a defect.
4. **Behaviour** — drive the same interactions on both (open the modal/drawer, toggle, switch tabs,
   expand a row, submit-with-empty to see validation) and compare the resulting text/state.
Then compute **presence**: strings/controls/sections present on one side but not the other →
the **Needs discussion** bucket.

### 5 · Report

**Provenance declaration — the report's FIRST line, always.** Before any per-route section, state:
```
Implementation click-through-blind: yes | NO — <which files were ported/copied>
```
- **yes** → the comparison is a real audit; report defects normally.
- **NO** → the run is **VOID**. Head the report `⚠️ NOT AUDITED`, do not emit a defect count, and do
  not write "0 defects" or "parity clean" anywhere. Say plainly that the compared artifacts are not
  independent, list what was copied, and state what the run *did* legitimately establish (e.g. the
  pages render in the product's environment; the deliberate divergences are the only divergences).
  Then record the route as `n/a (ported)` in `route-map.md`'s parity-status table.

Determine it from what the implementing session actually did — not from an assumption. If you are
the session that implemented the pages, you know; declare it. If you inherited the work and cannot
tell, say `unknown` and treat it as NO.

Then, per route:
```
## <realRoute>   (ref: <refRoute>)
### Layout
- [defect] <what differs> — click-through: <correct> · frontend: <current> · <file:line if known>
### Text
- [defect] Placeholder on "Search" field — click-through: "Search rules…" · frontend: "Search…"
### Controls
- [defect] "Ignore quiet hours" is a Checkbox — click-through uses a Switch
### Behaviour
- [defect] Row click doesn't open the drawer (click-through opens the detail drawer)
### Needs discussion
- [click-through only] "Rule simulator" button in the header — missing in frontend. Add, or confirm out of phase?
- [frontend only] "Export CSV" button — not in the design. Keep (backend feature) or remove?
- [placement] "Archive" action — frontend puts it on the list row; click-through has it on the detail page. Business rule, or drift from the design?
```
Rank defects most-severe first (missing element > wrong control > wrong text/placeholder > spacing).
If a route is clean, say so.

**The report is NOT optional, and a clean result does not shorten it.** Emit the per-route sections
with all four axis headings every time — a clean axis says "No differences", an unverified one says
so explicitly. Collapsing a zero-defect result into a one-line "parity clean" is a **process
failure**: the report IS the evidence, and without it nobody can tell a real comparison from a
skipped one. (Observed 2026-09-02: a run reached 0 defects and reported only the summary line, so
the reader could not see that the Behaviour axis had never been driven.)

**Behaviour is the axis that gets silently skipped.** Layout / Text / Controls fall out of
`capture.mjs` for free; Behaviour needs a hand-written interaction script per page, so under time
pressure it quietly disappears while the report still reads clean. Either drive it on **both** sides
(see USAGE.md §6 for the script pattern) or write `⚠️ Not verified` under that heading and say what
was not driven. Never leave it blank, and never let a passing E2E suite stand in for it — E2E proves
the product works, not that it matches the design.

**Report-first — always end with these two lines (never auto-fix here):**
- **Summary** — `N defects, M needs-discussion across K routes`. **Under a `NO` provenance
  declaration, there is no defect count to report** — write `NOT AUDITED (implementation ported) ·
  M needs-discussion across K routes` instead.
- **Suggested fix** — the exact command to apply the defects:
  `Suggested fix → /clickthrough-parity <feature> [phase <N>] --fix` (same scope you just ran)
  `--fix` applies only the **defects**; the **Needs discussion** items (presence / placement) are
  business decisions and are deliberately left untouched — resolve those with a human first.

### 5b · Record the audit — WHOLE-MODULE scope only
A bare-feature (no phase) run ends by stamping the module, which is what releases the push gate:
```sh
python3 .claude/skills/clickthrough-parity/record-audit.py <feature> \
    --routes <K> --defects <N> --discussion <M> --provenance blind
```
- **Only for whole-module scope.** A **phase** run must NOT record — it compared a slice of the
  module's routes, so it cannot clear the module. The recorder takes no phase argument by design.
- **Only for a blind implementation.** `--provenance ported` (or `unknown`) is **refused** by the
  recorder rather than written: stamping a VOID run would make the release gate pass on an audit
  that measured nothing.
- **Record after the defects are triaged**, not before — the stamp pins `HEAD`, and any later
  change under `frontend/src` (including a `--fix` commit) marks it stale, so stamping first just
  means stamping twice.
- The stamp is read by **`.claude/hooks/parity-gate.py`**, which blocks `git push` to `main`/
  `master` until the finished module has a current one. Feature-branch pushes are never gated, and
  neither are backend-only modules. Never hand-write a stamp — a forged one turns the gate into
  decoration.

### 6 · `--fix` mode
Only when `--fix` (or the user says to fix): apply the **defect** corrections to `frontend/` to match
the click-through, then re-run capture+compare on the affected routes until the defect list is empty.
Rules:
- Fix TEXT/placeholders/labels/control-type/order/layout to the click-through's exact value.
- Stay inside the binding design system in the root **`CLAUDE.md`** (use `ui/*`+`cx/*` components and
  design tokens — never raw hex; logical RTL props; correct radius/spacing tiers). Reuse the design
  page's structure; don't reinvent.
- **Never auto-resolve "Needs discussion" items** — leave those for the human, even under `--fix`.
- Match the surrounding code's conventions; run `npm run build` in `frontend/` after fixing and keep
  it green.
- Re-verify by re-capturing; report which defects were fixed and which remain (e.g. blocked on data).

---

## Guardrails
- **Never treat a ported page as audited.** If the implementation read or copied from the
  click-through, the run is VOID — report NOT AUDITED with the file list, and never a defect count.
  See the click-through-blind rule at the top.
- Do not edit the click-through to match the code — the click-through is the truth. If the code looks
  *more* correct, that is a **Needs discussion** item, not a fix.
- Do not modify the frontend/design-system sections of the root `CLAUDE.md` (owner-restricted). The
  only bookkeeping you maintain is this skill's own `route-map.md`.
- Don't auto-discover or fall back to a `clickthrough-reference/` folder in this repo — it's a stale
  copy, not the maintained truth. Use only the explicitly-supplied reference (`CLICKTHROUGH_DIR`/URL)
  from step 2 — which MAY be that folder if the developer points at it on purpose (e.g. while testing).
- Compare templates, not data. A different number/name/row-count is not a defect.
- If a real page can't be reached (auth/backend down), stop and ask — never compare a half-rendered or
  error page and call it parity.
- Keep it phase-scoped by default — only the routes this phase shipped — unless the user passes a
  bare feature with no phase (retro-audit of a whole finished module) or otherwise widens the scope.
- **Report-first, and no longer auto-triggered (changed 2026-09-03).** This skill does not fire
  automatically any more. `/speckit-tasks` emits a **`Click-through Parity for User Story X 🎨`**
  task per page-bearing story plus one **full-module** task in the Polish phase, and the frontend
  developer runs them by hand when ready to triage; the `after_implement` hook is now
  `optional: true` (a reminder, not a trigger). Every run still only REPORTS and prints the `--fix`
  command — applying fixes is always a deliberate `--fix` invocation. The one hard gate is at
  release: `.claude/hooks/parity-gate.py` blocks a push to `main`/`master` until the module has a
  current whole-module stamp (step 5b).
