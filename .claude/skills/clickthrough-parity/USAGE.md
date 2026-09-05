# clickthrough-parity — usage guide

Practical guide for running the **clickthrough-parity** skill during real development.
`SKILL.md` is the skill's contract (what the agent does); **this file is for the humans invoking it.**

> **The click-through is the source of truth.** The business team hands a full-cycle HTML per module;
> the frontend lead refines it in the **click-through repo**. A page implemented under `frontend/`
> via spec-kit must reproduce it exactly — layout, order, every label, hint, badge, empty state,
> placeholder, and the correct control type. This skill automates the "Careful HTML-to-React" review.

---

## 1. TL;DR — the commands

All four forms take the same two trailing lines (see §3 for why they are mandatory).

```
# ── Report one route (safest starting point, no code changes) ──────────────────
/clickthrough-parity 006-integration-hub route /integration-hub/service-channels
  — click-through: /Users/marwan/Desktop/test/test (http://localhost:4000)
  — real app: http://e2e.localhost:5174

# ── Fix one route (applies defects, leaves "needs discussion" alone) ───────────
/clickthrough-parity 006-integration-hub route /integration-hub/service-channels --fix
  — click-through: /Users/marwan/Desktop/test/test (http://localhost:4000)
  — real app: http://e2e.localhost:5174

# ── Report one phase / user story ──────────────────────────────────────────────
/clickthrough-parity 006 phase US1
  — click-through: /Users/marwan/Desktop/test/test (http://localhost:4000)
  — real app: http://e2e.localhost:5174

# ── Report a WHOLE finished module (all phases, deduped onto routes) ───────────
#    This is the run the release gate requires before a push to main/master (§8b).
/clickthrough-parity 002-customer-journey-mapping
  — click-through: /Users/marwan/Desktop/test/test (http://localhost:4000)
  — real app: http://e2e.localhost:5174
```

Then, after the whole-module run's defects are triaged, stamp the module so the push gate opens:

```sh
python3 .claude/skills/clickthrough-parity/record-audit.py 002-customer-journey-mapping \
    --routes <K> --defects <N> --discussion <M> --provenance blind
```

**Never run `--fix` at module scope on the first pass.** See §7.
**Only the whole-module run records a stamp**, and only with `--provenance blind` — see §8b.

---

## 2. Parameter grammar

| Form | Example | Means |
| --- | --- | --- |
| Feature folder | `006-integration-hub`, `006`, `integration-hub` | Every phase of that feature |
| Feature keyword | `journeys`, `kpi`, `actions` | Resolved to the matching feature |
| Phase / story | `006 phase US1`, `005 phase 4` | Only that phase's routes |
| Explicit route | `006 route /integration-hub/service-channels` | Only that route (tightest, most reviewable) |
| _(nothing)_ | `/clickthrough-parity` | Infers from branch / most recently touched `tasks.md` phase, then **confirms before heavy work** |
| `--fix` | append to any of the above | Apply defect corrections. Also accepts "and fix them" in prose |

Backend-only phases self-skip with `no frontend pages in this phase — skipped`.

---

## 3. Prerequisites — and the two lines you must always pass

### The reference paths are NOT optional

The skill is **forbidden** from guessing the click-through location, and specifically from falling
back to this repo's `clickthrough-reference/` folder — **that folder is a stale copy.** It contains
**no Integration Hub pages at all**, so trusting it produces a confident, wrong
"no counterpart, nothing to compare" result. (This happened; see §12.)

So every invocation carries:

```
  — click-through: <CLICKTHROUGH_DIR> (<CLICKTHROUGH_BASE_URL>)
  — real app: <FRONTEND_BASE_URL>
```

### Stack that must be running

| Piece | This machine (verify — see below) |
| --- | --- |
| Postgres | `localhost:5432` (dev: `postgres` / `admin`) |
| Backend | `Nabadat.TenantAdmin` → `https://localhost:7286` |
| Real frontend | `frontend/` dev server → **`http://e2e.localhost:5174`** |
| Click-through | `/Users/marwan/Desktop/test/test` → `http://localhost:4000` |

**Two traps in that table:**

1. **Use the tenant subdomain, not bare localhost.** Tenant is resolved from the subdomain, so
   `http://localhost:5174` returns `tenant.subdomain_missing`. Use `e2e.localhost` (`*.localhost`
   resolves to loopback automatically).
2. **`:5173` is NOT necessarily the real app.** On this machine the *click-through* occupies the
   default `:5173`, and the product frontend runs on `:5174`. Pointing the skill at the wrong one
   fails at sign-in with `waiting for Locator("#email")`, because the click-through's login uses
   `#login-email`. **Always verify:**

```sh
for p in 5173 5174 4000; do
  pid=$(lsof -nP -iTCP:$p -sTCP:LISTEN -t 2>/dev/null | head -1)
  [ -n "$pid" ] && printf "%-5s -> %s\n" "$p" "$(lsof -p $pid -a -d cwd -Fn 2>/dev/null | tail -1)"
done
```

Also derive the **scheme** from the workspace's `vite.config.ts`: `server.https` unset ⇒ `http://`.
A scheme mismatch breaks the very first navigation with `ERR_SSL_PROTOCOL_ERROR`.

### Credentials

Read from the gitignored `tests/Nabadat.E2ETests/appsettings.local.json` (copy from
`appsettings.local.json.example`, which already carries the dev-seeded values). Sign in as the
persona whose view you are checking — permissions change what renders:

| Persona | Account | Sees |
| --- | --- | --- |
| P-01 CX Manager | `e2e-active@dev.local` / `Admin123!` | Full authoring (create/edit buttons) |
| P-07 IT Admin | `e2e-p07@dev.local` / `Admin123!` | Read-only mirror (View, no Save) |
| P-02 CX Analyst | `e2e-p02@dev.local` / `Admin123!` | Journey/KPI read paths |

One-time browser setup for the skill folder:

```sh
cd .claude/skills/clickthrough-parity && npm install && npx playwright install chromium
```

---

## 4. Getting a session token

The real app is MFA-gated, so captures need a `sessionStorage.session_token`. This script drives the
real login + TOTP once and prints the token:

```js
// scratch/token.mjs  —  node token.mjs <baseUrl> <email> <password> <base32TotpSecret>
import crypto from "node:crypto"
const { chromium } = await import("<repo>/.claude/skills/clickthrough-parity/node_modules/playwright/index.mjs")
const [BASE, EMAIL, PASS, SECRET] = process.argv.slice(2)
const A = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
const b32 = (s) => { let b=""; for (const c of s.toUpperCase()) { const v=A.indexOf(c); if(v<0) continue; b+=v.toString(2).padStart(5,"0") }
  const o=[]; for (let i=0;i+8<=b.length;i+=8) o.push(parseInt(b.slice(i,i+8),2)); return Buffer.from(o) }
const totp = (s) => { const n=Math.floor(Date.now()/1000/30); const buf=Buffer.alloc(8); buf.writeUInt32BE(n,4)
  const h=crypto.createHmac("sha1",b32(s)).update(buf).digest(); const off=h[19]&15
  return String((h.readUInt32BE(off)&0x7fffffff)%1e6).padStart(6,"0") }
const b = await chromium.launch(); const p = await (await b.newContext({ignoreHTTPSErrors:true})).newPage()
await p.goto(`${BASE}/login`); await p.locator("#email").fill(EMAIL); await p.locator("#password").fill(PASS)
await p.locator("button[type=submit]").first().click()
await p.waitForURL(/\/auth\/mfa/, { timeout: 15000 })
await p.locator("input[autocomplete='one-time-code'], input[inputmode='numeric']").first().fill(totp(SECRET))
await p.waitForURL(/^(?!.*\/(login|auth|mfa)).*$/, { timeout: 20000 })
let tok = ""
for (let i=0;i<40;i++){ const t = await p.evaluate(()=>sessionStorage.getItem("session_token")); if(t){tok=t;break} await p.waitForTimeout(200) }
await b.close()
if (!tok) { console.error("NO TOKEN"); process.exit(1) }
console.log(tok)
```

> **Re-mint the token every run, and assert it is non-empty.** Dev-server and backend restarts
> invalidate it. A capture with a stale/empty token silently returns the **`/login` page**, which
> reads as *"the page lost all its content"* (`0 headings, 2 controls`) and looks exactly like a
> catastrophic regression. This wasted a full debugging cycle once — see §11.

---

## 5. What counts as a defect

Three buckets, and the third is the one that keeps the skill safe to run.

| Bucket | Examples | `--fix` behaviour |
| --- | --- | --- |
| **MUST match → defect** | Element presence & order · every string incl. **placeholders** · **control type** (switch ≠ checkbox) · layout relationships (columns, stacking, width tiers) · states (disabled/required/error) | Fixed to the click-through's exact value |
| **MAY differ → not a defect** | Mock vs real **data values** · a list empty because the backend has no rows · anything the click-through marks "coming soon" | Ignored |
| **NEEDS DISCUSSION → never auto-fixed** | Click-through has X and `frontend/` doesn't · `frontend/` has Y and the design doesn't | **Reported only.** Left for a human even under `--fix` |

Adding or removing a whole element is a **design decision**, not a fix. That is deliberate: it stops
the skill from deleting a spec-mandated feature just because the prototype lags, or from inventing a
screen the design never approved.

---

## 6. Reading the report

Per route, ranked most-severe first (missing element > wrong control > wrong text/placeholder > spacing):

```
## /integration-hub/service-channels   (ref: /integration-hub/service-channels)
### Layout
- [defect] Search row missing — click-through has a bounded Search label + input
### Text
- [defect] Column header — click-through: "Supported params" · frontend: "Supported"
### Controls
- [defect] Status renders as a bare dot + text — click-through uses a Badge
### Behaviour
- [defect] No client-side pre-validation (click-through validates + focuses first invalid field)
### Needs discussion
- [frontend only] Waypoints header icon — not in the design. Keep or remove?
- [placement] "Archive" action — frontend puts it on the list row; click-through has it on the detail page. Business rule, or drift from the design?
```

**Needs discussion** = one question on **any axis**: *drift from the design, or a deliberate business
rule?* The click-through is the default truth, but if business is right the code stays and the
*click-through* gets updated instead — a valid, accepted outcome, **not** a defect. Never auto-fixed,
even under `--fix`. The tags below are common **examples**, not a closed list:
- `[click-through only]` — the design has it, the code doesn't (missing, or intentionally out of phase).
- `[frontend only]` — the code has it, the design doesn't (scope creep, backend need, or the design should adopt it).
- `[placement]` — **same element, different page** (e.g. an action on the list row vs. the detail page).
- `[control]` / `[behaviour]` / `[text]` / `[layout]` — same intent realised differently (a Select where
  the design uses a toggle, an extra confirm step, reworded copy, a restructured section) **when it reads
  like a considered business choice, not a slip.** Any axis can be a business decision, not just placement.

When unsure whether a difference is mechanical drift (a defect `--fix` may touch) or a business choice,
put it in **Needs discussion** and resolve it with the frontend lead / business first.

**Report-first — the run always ends with two lines and never changes code on its own:**
```
Summary      → N defects, M needs-discussion across K routes
Suggested fix → /clickthrough-parity <feature> [phase <N>] --fix   (same scope you just ran)
```
`--fix` applies only the **defects**; Needs-discussion items are left untouched. The automatic runs
(the `/speckit-implement` per-frontend-phase checkpoint and the `after_implement` safety-net) only ever
report and print this command — applying fixes is always a deliberate `--fix` you run yourself.

> ⚠️ **A clean result does not shorten the report.** Emit every per-route section with all four axis
> headings regardless — a clean axis says "No differences". Collapsing a zero-defect run into one
> summary line is a process failure, because the report is the evidence: without it nobody can tell a
> real comparison from a skipped one. Observed on 2026-09-02 — a run hit 0 defects, printed only the
> summary, and the reader could not see that the Behaviour axis had never been driven.

### Driving the Behaviour axis (the one that silently disappears)

Layout / Text / Controls fall out of `capture.mjs` for free. **Behaviour does not** — it needs a short
script that performs the same interactions on both sides and prints comparable lines, so under time
pressure it vanishes while the report still reads clean. Never let a green E2E suite stand in for it:
E2E proves the product works, not that it matches the design.

```js
// behaviour.mjs <baseUrl> stub|token [token]  — run once per side, then diff the printed lines
const go = async (r) => {          // SPA nav: a full page load resets the click-through's in-memory auth
  await p.evaluate((x) => { history.pushState({}, "", x); dispatchEvent(new PopStateEvent("popstate")) }, r)
  await p.waitForTimeout(1600)
}
await go("/integration-hub/service-channels/new")
const sup = p.locator("[data-testid^='supported-']").first()
const field = (await sup.getAttribute("data-testid")).slice("supported-".length)
const req = p.getByTestId(`required-${field}`)
out.push(`initial:       required disabled=${await req.isDisabled()}`)
await sup.click(); out.push(`supported ON:  required disabled=${await req.isDisabled()}`)
await sup.click(); out.push(`supported OFF: checked=${await req.isChecked()}`)
```

Cover at minimum: the gating interaction the story is about, any filter/search (including its
no-match state), and submit-with-empty to compare validation and focus behaviour.

---

## 7. Recommended workflow

### Rule 0 — implementation is CLICK-THROUGH-BLIND (build the pages without looking at it)

The implementing session must not open, read, or copy from `CLICKTHROUGH_DIR`. Pages are built from
`spec.md` + `tasks.md` + the design system in the root `CLAUDE.md`; the click-through is read only
by this skill, after the story's E2E checkpoint.

```
implement from spec.md  (click-through NOT opened)
        ↓
E2E checkpoint green
        ↓
/clickthrough-parity <feature> phase <N>   ← YOUR TASK in tasks.md; first real look at the design
        ↓
you triage the defect list and decide what to apply
        ↓
… repeat per story …
        ↓
/clickthrough-parity <feature>             ← the full-module task, before the module ships
        ↓
record-audit.py stamps the module → `git push` to main/master unblocks
```

**The per-story and full-module runs are TASKS assigned to you** (changed 2026-09-03). `/speckit-tasks`
emits a `Click-through Parity for User Story X 🎨` subsection after each page-bearing story's E2E
subsection, plus one full-module task in the Polish phase. Nothing fires the audit automatically any
more — a report that lands when nobody is ready to triage it just gets scrolled past. The
`after_implement` hook is now a reminder (`optional: true`), not a trigger.

**Why it is rule 0.** The audit is only worth reading if the two sides were built independently.
Port the click-through's files during implementation and the run compares the reference with itself
— it reports "identical" no matter what, and real drift becomes invisible. Such a run is **VOID**:
the report says **NOT AUDITED**, never "0 defects" (see SKILL.md → "Provenance declaration").

This bit us for real. Across 2026-09-02/03, four M-13 routes (SCR-03, SCR-04, SCR-05, SCR-06) were
implemented by copying the click-through's components and i18n block, then reported "0 defects" —
and a "consecutive 0-defect streak" was recorded as if it were a quality signal. It measured
nothing; all four are now marked NOT AUDITED in `route-map.md`. **A from-spec build of SCR-03/04
had produced 25 real defects** — that was the useful output, and porting traded it away for an empty
report.

If you want the control harder than an instruction: leave `clickthroughDir` out of
`reference.json` and pass the path only when you invoke the audit, so the implementing session has
no reference sitting in the repo. Since the audit is now a task you invoke by hand, the cost of this
is just passing the path on the command line — there is no automatic run left to starve.

### Then, per route

```
report at the widest scope you care about
        ↓
triage: defects (mechanical) vs needs-discussion (decisions)
        ↓
resolve the decisions with the frontend lead
        ↓
--fix ONE ROUTE AT A TIME, reviewing each diff
        ↓
re-run report on that route to confirm parity
```

**Why `--fix` per route, never per module:** one route (SCR-03) was 11 defects; the next (SCR-04)
was 14. A four-route module would be ~50 changes across four files in one unreviewable diff. Fixing
route-by-route keeps each diff small enough to actually read, and each pass ends with a green
`npm run build` plus a re-capture.

---

## 8. Choosing a scope — phases collapse onto routes

A module's phase count is **not** its route count; several phases usually edit the same page. Real
numbers from this repo:

| Module | Frontend phases | Distinct routes | Notes |
| --- | --- | --- | --- |
| **006** Integration Hub, US1 | 2 (Ph 2, Ph 3) | 2 | SCR-03 list + SCR-04 create/edit |
| **003** KPI Engine & Settings | 7 (Ph 3–9) | ~3 | US2/US3/US5/US7 all edit `/kpi-management/:shortName`; US4+US6 both land on `/settings` |
| **002** Customer Journey Mapping | 4 (Ph 3–6) | 6 | list · builder · scoring · detection · versions · personas |

So module scope is usually one practical run. **Dedupe before capturing** — don't capture the same
route four times because four phases touched it.

### Expect asymmetry between modules

Parity is not always "the code is behind the design":

- **006** — the click-through had a *more complete, more refined* implementation than the product.
  25 real defects across 2 routes.
- **002** — the **product went well beyond the design.** The click-through's whole journey surface is
  `/journeys`, `/journeys/:id`, `/journeys/:id/stats` plus three components. There is **no** CT page
  for scoring, detection, versions, or personas; and CT's `JourneyStatsPage` has no product
  counterpart. A 002 run is therefore mostly **needs-discussion**, and `--fix` has little legitimate
  work until a human resolves the pairing.

Check this before promising a fix pass: `ls <CLICKTHROUGH_DIR>/src/pages/` and
`ls <CLICKTHROUGH_DIR>/src/features/<module>/`, and grep its `App.tsx` for the routes.

---

## 8b. The release gate — `git push` to main/master

`.claude/hooks/parity-gate.py` (wired as a `PreToolUse` hook on `Bash` in `.claude/settings.json`)
**blocks a push to `main`/`master`** until the module being promoted has a current whole-module
parity stamp. In this team's flow, pushing to main is what "the module is finished" means, so that
is where the hard check sits.

What it does and does not touch:

| Push | Gated? |
| --- | --- |
| `git push origin <feature-branch>` | **No** — never touched, whatever the module's state |
| `git push origin main` / `master` (also `HEAD:main`, `refs/heads/main`, `--all`, `--mirror`) | **Yes** |
| `npm test && git push origin main`, `git -C /path push origin main` | **Yes** — detection is token-based per command segment |
| Push of a module whose `tasks.md` has no `frontend/src` tasks | **No** — backend-only, nothing to compare |

The stamp comes from `record-audit.py` (SKILL.md → step 5b) and pins the audited commit, so it goes
**stale** — and the gate asks for a re-run — as soon as anything under `frontend/src` changes. A
backend or docs commit after the audit does **not** invalidate it.

**Why a push gate rather than CI:** the audit needs the private click-through checkout, two live
authenticated servers, and a model reading screenshots to judge layout/text/controls/behaviour.
There is no headless assertion to fail, so GitHub Actions cannot run it — `deploy.yml` is a
deploy-on-push-to-main job, which is *after* the point where a defect list would still be useful.

**Every denial names the exact command to run.** If the gate ever blocks something it shouldn't,
that is a bug in the detection — report it rather than working around it; the failure modes are
pipe-tested (16 cases) and each denial path is verified.

---

## 9. `route-map.md`

The persistent `M-NN | real route | real file | click-through route | click-through file` table in
this folder. Read first, appended as pairs are resolved — it makes repeat runs deterministic and is
where per-route parity status and open decisions are recorded. It is the **only** bookkeeping the
skill maintains; it never edits the root `CLAUDE.md` (owner-restricted).

Seed it for a module before the first fix pass, especially where paths differ
(product `/journeys/:id/builder` ↔ click-through `/journeys/:id`).

---

## 10. Interaction with the E2E lane

Fixes can move the DOM the browser tests select on. Rules that held up in practice:

- **`data-testid`s are test wiring, not design.** They belong in **needs-discussion**, not defects —
  renaming them to match the click-through breaks `tests/Nabadat.E2ETests/` for no user-visible gain.
  A fix pass that renamed them was reverted for exactly this reason.
- **When a fix moves a testid, move the hook, don't drop it.** SCR-04's lock indicator changed from a
  "Locked" Badge to a locked help line; `data-testid="channel-id-locked"` moved onto the paragraph,
  so `M13-E2E-02` kept passing untouched.
- **Re-run the module's E2E filter after every fix pass:**
  ```sh
  E2E_BASE_URL="http://e2e.localhost:5174" \
    dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~ServiceChannelTests"
  ```
- A fix that legitimately changes behaviour an E2E asserts is a **decision**, not a silent edit —
  e.g. the click-through blocks P-07 from SCR-04 with `AccessDenied` while the product renders it
  read-only (which spec BR-24 favours, and `M13-E2E-06` asserts).

---

## 11. Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| "No counterpart in the click-through" for a module you know exists | Compared against this repo's stale `clickthrough-reference/` | Always pass `CLICKTHROUGH_DIR` explicitly |
| Every capture/test dies at `waiting for Locator("#email")` | Pointed at the wrong app — the click-through's login uses `#login-email` | Verify port ownership with the `lsof` recipe in §3 |
| Page captures as `0 headings, 2 controls`; screenshot shows `/login` | Stale/empty session token (backend or dev server restarted) | Re-mint; assert the token is non-empty before capturing |
| `tenant.subdomain_missing` | Used bare `localhost` | Use `e2e.localhost:<port>` |
| First navigation throws `ERR_SSL_PROTOCOL_ERROR` | Wrong scheme | Derive from `vite.config.ts` `server.https` |
| Authenticated `/api` calls 401 right after a successful login | Backend `UseHttpsRedirection` 307s the proxied request cross-origin, stripping `Authorization` | Point the Vite `/api` proxy at the backend's **HTTPS** port with `secure:false` (already configured here) |
| Capture reports a button "missing" that is clearly on screen | It's an `<a>` (a `<Link>` styled with `buttonVariants()`), and the extractor counts `<button>` | Verify visually; treat as needs-discussion, not a defect |
| Fingerprint diff is noisy with app-shell items | Topbar search, user menu, theme/language toggles belong to each app's own shell | Filter them out before comparing |
| A populated layout can't be compared | Backend has no rows (e.g. all 50 dev journeys have 0 stages — E2E residue) | Data difference, not a defect. Seed deliberately if that layout must be covered |

---

## 12. Worked example — M-13 US1 (real run)

**Setup.** Click-through `/Users/marwan/Desktop/test/test` on `:4000`; product frontend on `:5174`;
backend `:7286`. First attempt compared against `clickthrough-reference/`, found no M-13 pages, and
concluded "nothing to compare" — **wrong**. The live click-through had a complete Integration Hub at
the same routes, with the same file names (`AllServiceChannelsPage.tsx`, `ServiceChannelForm.tsx`,
`useServiceChannels.ts`).

**Report** — `25 defects, 6 needs-discussion across 2 routes`:

- SCR-03 (11): missing search row and count line; loading/empty/error rendered outside the table
  instead of inside; wrong table-header treatment; status as bare dot instead of a `Badge`; subtitle,
  column header, empty hint, truncated and footer copy all rewritten.
- SCR-04 (14): **single-column instead of the design's `lg:grid-cols-2`**; 5 contract columns instead
  of 4; raw `text`/`date_time` instead of "Text"/"Date & time"; missing subtitle; "Save" instead of
  "Create channel"/"Save changes"; no client-side pre-validation; not a `<form>`.

**Fix, one route per pass.** Each: apply defects → `npm run build` → re-capture → re-diff → re-run
the 6 E2E tests. Both routes reached parity with the fingerprint differing only by the click-through's
own topbar search (app-shell chrome). E2E stayed 6/6 green throughout.

**Left open (6 decisions).** Header icon; `data-testid` naming; `<Link>` vs `<Button onClick>`;
unsaved-changes dialog (product-only, but spec **FR-GBL-03** requires it — the design lags); P-07
read-only vs `AccessDenied`; preserving contract rows whose parameter was later disabled.

**Lesson worth repeating:** during the SCR-03 fix the agent renamed `data-testid`s while copying the
design's markup — a needs-discussion item it was not allowed to resolve. It was reverted and
annotated in the file. If you see a fix pass touching something from the discussion list, push back.

---

## 13. Not supported yet

- **No batch driver.** Nothing runs "every module" in one go, and `route-map.md`'s status table is
  maintained per run, not automatically. Worth building once 3–4 modules are done: a manifest of
  route pairs plus a loop that captures, diffs and writes the table.
- **No CI mode.** The skill needs both apps running and an interactive-ish token mint; it is a
  developer-workstation tool today, not a pipeline gate.
- **Dynamic routes need a real id.** `/journeys/:id` must be given a concrete id per side (the
  click-through's mocks use `j1`, `s1`, `tp1`; the product needs a real GUID from the API).
- **RTL and dark mode are opt-in per run** (`--dir rtl`, `--themes light,dark`). The product is
  bilingual and RTL-by-default, so check RTL before declaring a page done.

---

## 14. Installing & migrating the skill

New machine, new clone, or porting to another repo — start here.

### 14.1 What travels (the file manifest)

Everything lives in one folder, `.claude/skills/clickthrough-parity/`:

| File | Role | Copy it? |
| --- | --- | --- |
| `SKILL.md` | The agent's contract. Its YAML frontmatter (`name`, `description`) is what makes `/clickthrough-parity` discoverable — without it the skill is invisible | ✅ required |
| `capture.mjs` | Playwright runner: screenshot + structural/text fingerprint per route | ✅ required |
| `package.json` · `package-lock.json` | Pins `playwright ^1.48.0`. Self-contained, so `node .claude/skills/clickthrough-parity/capture.mjs` works with no repo-root `package.json` | ✅ required |
| `USAGE.md` | This guide | ✅ required |
| `reference.example.json` | Template for the per-machine app locations | ✅ required |
| `reference.json` | **Your** machine's paths — gitignored | ❌ each dev makes their own |
| `route-map.md` | Route pairings + parity status | ⚠️ repo-specific: carry it within Nabadat, **reset it** when porting elsewhere |
| `node_modules/` | 18 MB of Playwright | ❌ **never** — run `npm install` |

### 14.2 Point the skill at your two apps

Copy the template and edit it. This is what stops an automatic run from stopping to ask:

```sh
cp .claude/skills/clickthrough-parity/reference.example.json \
   .claude/skills/clickthrough-parity/reference.json
```

Set `clickthroughDir`, `clickthroughBaseUrl`, `frontendBaseUrl`, and a default `persona`.
Precedence is **prompt paths → `reference.json` → ask**, so you can still override per run. A
*relative* `clickthroughDir` (`../nabadat-clickthrough`) travels between machines; an absolute one
doesn't — prefer a sibling checkout. Credentials are **not** duplicated here; they stay in the E2E
lane's `appsettings.local.json`.

### 14.3 Register the spec-kit hook

The skill also wires into `/speckit-implement`. That lives in **`.specify/extensions.yml`**, in two
places:

```yaml
installed:
  - agent-context
  - git
  - clickthrough-parity          # ← 1. declare it

hooks:
  after_implement:
  - extension: clickthrough-parity
    command: clickthrough-parity  # ← 2. register the hook
    enabled: true
    optional: false               # mandatory: /speckit-implement emits EXECUTE_COMMAND
    prompt: Run clickthrough-parity on the just-implemented frontend pages?
    description: >-
      Compare this run's frontend pages against the standalone click-through (layout, text,
      placeholders, control types, behaviour) and report differences, plus a bidirectional
      "needs discussion" list. Runs in report mode (no code changes); self-skips backend-only
      phases. Add --fix manually to apply the corrections.
    condition: null
```

With `settings: auto_execute_hooks: true`, it runs **unprompted**. Set `optional: true` to be offered
it instead, or `enabled: false` to park it.

**It fires twice per `/speckit-implement`, and that's intentional:**

1. **Per phase** — the implement outline carries a *binding* instruction to run
   `/clickthrough-parity <feature> phase <N>` as soon as a page-bearing story's checkpoint passes,
   while the implementer's context is still warm.
2. **Once at run end** — this hook, as a safety net.

Both are **report mode**; neither ever applies `--fix`. Backend-only phases self-skip.

### 14.4 Per-machine setup (not in version control)

```sh
# 1. Node deps for the capture runner
cd .claude/skills/clickthrough-parity && npm install

# 2. Chromium — lands in ~/Library/Caches/ms-playwright (~1.1 GB), machine-local, never committed
npx playwright install chromium

# 3. The click-through checkout, on the design branch, on a port that is NOT the product's
cd <clickthroughDir> && npm install && npm run dev -- --port 4000 --strictPort

# 4. E2E credentials (gitignored) — the example already carries the dev-seeded values
cp tests/Nabadat.E2ETests/appsettings.local.json.example \
   tests/Nabadat.E2ETests/appsettings.local.json
```

Then smoke-test with the cheapest run — one route, report mode. With `reference.json` in place the
trailing path lines are optional:

```
/clickthrough-parity 006 route /integration-hub/service-channels
```

A healthy run captures both sides and reports per-route findings. If it says "no counterpart", re-read
§3 — you're almost certainly on the wrong reference or the wrong port.

### 14.5 What an automatic run still can't do for itself

`reference.json` removes the "where is the click-through?" question. Two prerequisites remain, and the
skill correctly **stops and asks** rather than faking a comparison:

- **The stack must already be running** — backend, the product dev server, the click-through, and a
  valid signed-in session. The hook can't start them.
- **Playwright deps must be installed** (§14.4 steps 1–2), once per machine.

### 14.6 Porting to a repo that isn't Nabadat

Six things are hard-wired in `SKILL.md` and must be edited first:

| Assumption | Where | Change to |
| --- | --- | --- |
| Product frontend at `frontend/src/…` | Step 0.3, Step 6 | Your SPA's path |
| Spec-kit layout `specs/NNN-*/tasks.md`, phases tagged `… frontend` with an `E2E 🎭` subsection | Step 0 | Your equivalent — or drop phase resolution and always pass an explicit route |
| Routes in `frontend/src/App.tsx` with `M-NN` module comments | Steps 0.3, 1 | Your router's location |
| Auth: `/login` → MFA/TOTP → `sessionStorage.session_token`, ids `#email` / `#password` | Step 2, `capture.mjs --auth token` | Your login flow and session key (`--token-key`) |
| Persona model `P-01`…`P-08` gating what renders | §3, report expectations | Your role model |
| Design system bound in the root `CLAUDE.md` | Step 6 fix rules, guardrails | Your design-system doc — including the guardrail against editing it |

Also **reset `route-map.md`** to its empty template, and rewrite the
`clickthrough-reference/`-is-stale warning to name whatever stale copy your repo has. (Check before
deleting it — every repo seems to grow one.)

Ports unchanged: `capture.mjs`, the three-bucket defect model, the report format, the
report-then-fix-per-route workflow, and every lesson in §11.

---

## 15. Re-testing the whole flow (revert → `/speckit-implement` → parity)

For exercising the end-to-end loop — implement a frontend phase, watch the E2E checkpoint fire the
parity report, then revert and do it again. Written down because a **fresh session cannot tell which
files were the frontend pass and which belong to the backend team**, and guessing risks deleting
working backend work.

### The M-13 US1 manifest (Phases 2 + 3 of `specs/006-integration-hub`)

**Delete — created by the frontend pass:**

```
frontend/src/features/integration-hub/api.ts
frontend/src/features/integration-hub/dto.ts
frontend/src/features/integration-hub/http.ts
frontend/src/features/integration-hub/integration-hub-api-error.ts
frontend/src/features/integration-hub/components/AccessDenied.tsx
frontend/src/features/integration-hub/components/ScreenPlaceholder.tsx
frontend/src/features/integration-hub/components/ServiceChannelForm.tsx
frontend/src/features/integration-hub/hooks/useIntegrationHubAccess.ts
frontend/src/features/integration-hub/hooks/useServiceChannels.ts
frontend/src/features/integration-hub/pages/AllServiceChannelsPage.tsx
frontend/src/features/integration-hub/pages/IntegrationHubPlaceholderPages.tsx
frontend/src/features/integration-hub/pages/ServiceChannelFormPage.tsx
tests/Nabadat.E2ETests/IntegrationHub/ServiceChannelTests.cs
```

**KEEP** the `.gitkeep` files in those folders — they are Phase-1 scaffolding (T005/T006), not part
of the frontend pass.

**Revert these edits (don't delete the files):**

| File | What to remove |
| --- | --- |
| `frontend/src/App.tsx` | the Integration Hub imports + all 9 `/integration-hub/*` routes |
| `frontend/src/components/layout/AppLayout.tsx` | the 2 `SidebarGroup`s, `canViewIntegrationHub` / `canViewRequestLogs`, their entries in `hasFeatureNav`, and the 5 added lucide icons |
| `frontend/src/i18n/locales/{en,ar}.json` | the whole `integrationHub` namespace + the 7 added `nav.*` keys |
| `tests/Nabadat.E2ETests/Infrastructure/E2ETenantDb.cs` | the 3 M-13 helpers (`GetServiceChannelIdAsync`, `MarkChannelIdLockedAsync`, `DeleteServiceChannelAsync`) |
| `tests/Nabadat.E2ETests/COVERAGE.md` | the M-13 section (6 rows) + its module-folder index row |
| `specs/006-integration-hub/tasks.md` | uncheck **T019, T020, T021, T037, T038, T039, T040, T042** and restore the "not started" phase notes |

**Never touch** — this is the backend team's, and it is complete:
`src/Nabadat.IntegrationHub/**`, `tests/Nabadat.IntegrationHub.{Unit,Integration}Tests/**`, and
tasks **T007–T018, T022–T036, T041** (leave them `[X]`).

### Two easy-to-miss knock-ons

1. **`TODO.md`** — a full pass RESOLVES **TODO-M13-002** (T021 retargeted to `AppLayout.tsx`) and adds
   an update note to **TODO-M13-004** (the E2E lane's cleanup helper). A revert must **reopen
   TODO-M13-002 as a GAP** and drop that note, or the next run thinks the nav question is settled
   when the nav no longer exists.
2. **`route-map.md`** — flip the two M-13 parity rows back to `⛔ not implemented`, so a parity run
   reports "nothing to compare" instead of a bogus defect list.

### Verify the revert

```sh
grep -rl "integration-hub\|integrationHub" frontend/src   # expect 0 (ignore .gitkeep)
(cd frontend && npm run build)                            # expect 0 errors
dotnet build tests/Nabadat.E2ETests                       # expect 0 errors
```

### Then re-run and watch for the flow

```
/speckit-implement T019 to T021 and T037 to T040 then T042
```

The loop is only correct if, **after** the E2E filter goes green, you see the per-route parity report
(all four axis headings) followed by the `Suggested fix → /clickthrough-parity … --fix` line. If you
get a one-line "parity clean" instead, the report step was skipped — see §6.

### Not covered by any revert

The E2E lane writes **real channel rows** and cleans up its own, but the dev tenant still carries
residue from earlier runs (`E2E-*` channels, `e2e_probe_*` parameters visible in SCR-04's contract
table). Rows are data, not code, so no revert removes them — sweep them separately before a demo.
