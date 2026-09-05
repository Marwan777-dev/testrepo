# Click-through ↔ frontend route map

Maintained by the **clickthrough-parity** skill. One row per page-bearing route. When the skill
resolves a new pair (from the `M-NN` module tag + page role), it appends it here so the next run is
deterministic. Data values may differ between the two sides — only layout / composition / text /
control-type must match.

## Workflow rule — implementation is CLICK-THROUGH-BLIND (read this first)

**The implementing session must NOT open, read, or copy from the click-through checkout.** Build
frontend pages from `spec.md` + `tasks.md` + the design system in the root `CLAUDE.md`. The
click-through is the **audit** reference, read only by `/clickthrough-parity` **after** the story's
E2E checkpoint.

**Why this is a hard rule.** A parity run only carries information when the two sides were produced
independently. If the implementation was ported from the click-through, the run diffs a file against
the file it was copied from and can only ever report "identical" — a rubber stamp that tells the
reader nothing and hides real drift. That is not a clean audit; it is **no audit**.

An earlier note here recommended the opposite ("port the click-through's files — it turned 25 parity
defects into 0"). **Withdrawn 2026-09-03.** Porting did not fix 25 defects; it made the diff
undefined. Those 25 defects were the signal — where a from-spec build drifts from the design — and
they were traded away for an empty report.

**So:** a run whose implementation was derived from the reference is **VOID, not clean**. Report it
as *not audited*, never as "0 defects". Every report states this on its first line (see
`SKILL.md` → step 5, "Provenance declaration").

## How the audit gets run (changed 2026-09-03)

**It is assigned work, not an automatic step.** `/speckit-tasks` now emits a
**`Click-through Parity for User Story X 🎨`** subsection after each page-bearing story's E2E
subsection, plus one **full-module** task in the Polish phase. The frontend developer runs them by
hand when ready to triage; the `after_implement` hook is a reminder (`optional: true`), not a
trigger. Why: an audit that fires the moment a phase goes green produces a defect list nobody is
sitting down to act on.

**One hard gate, at release.** `.claude/hooks/parity-gate.py` blocks `git push` to `main`/`master`
until the module being promoted has a current whole-module stamp in `audits/<feature>.json` (written
by `record-audit.py`). Feature-branch pushes are never gated, backend-only modules are never gated,
and the stamp only goes stale on a change under `frontend/src`. A **phase** run cannot stamp, and
the recorder **refuses** a `ported`/`unknown` provenance — a VOID run must never open the gate.

| Module | Real route (`frontend/`) | Real file | Click-through route | Click-through file |
| --- | --- | --- | --- | --- |
| _(example)_ M-16 | `/journeys` | `frontend/src/features/journeys/pages/JourneyListPage.tsx` | `/journeys` | `src/pages/JourneysPage.tsx` |
| M-13 | `/integration-hub/service-channels` | `frontend/src/features/integration-hub/pages/AllServiceChannelsPage.tsx` | `/integration-hub/service-channels` | `src/features/integration-hub/pages/AllServiceChannelsPage.tsx` |
| M-13 | `/integration-hub/service-channels/new` · `/:id` | `frontend/src/features/integration-hub/components/ServiceChannelForm.tsx` | `/integration-hub/service-channels/new` · `/:id` | `src/features/integration-hub/pages/ServiceChannelFormPage.tsx` + `components/ServiceChannelForm.tsx` |
| M-13 | `/integration-hub/parameters` | `frontend/src/features/integration-hub/pages/AllParametersPage.tsx` | `/integration-hub/parameters` | `src/features/integration-hub/pages/AllParametersPage.tsx` |
| M-13 | _(SCR-06 drawer — no route; opens over `/integration-hub/parameters`)_ | `frontend/src/features/integration-hub/components/ParameterDrawer.tsx` | _(same)_ | `src/features/integration-hub/components/ParameterDrawer.tsx` |

> Replace the example row as real pairs are resolved. `Click-through file` is relative to the
> click-through checkout root (`CLICKTHROUGH_DIR`).


## Resolved references (2026-08-31)

- **Click-through checkout**: `/Users/marwan/Desktop/test/test`, served at `http://localhost:4000`.
  **Do not use this repo's `clickthrough-reference/` folder** — it is a stale copy with **no**
  Integration Hub pages at all, and trusting it produces a false "no counterpart, nothing to
  compare" result.
- **Real app**: this repo's `frontend/`, dev server on `http://e2e.localhost:5174`.
- **Don't assume which port is which** — it moves. On 2026-09-02 `:5173` AND `:5174` were both this
  repo's `frontend/` and the click-through was on `:4000` only; earlier notes had `:5173` as the
  click-through's. Confirm every time with `lsof -nP -iTCP -sTCP:LISTEN | grep -E ':(4000|5173|5174) '`
  then `lsof -p <pid> -a -d cwd` to read each server's working directory.
- The click-through's M-13 module mirrors the file layout `tasks.md` prescribes
  (`features/integration-hub/{api,dto,http}.ts`, `hooks/useServiceChannels.ts`,
  `components/ServiceChannelForm.tsx`, `pages/AllServiceChannelsPage.tsx`), so pairs map 1:1 by name.
  One structural difference: the click-through splits SCR-04 into a route wrapper
  (`pages/ServiceChannelFormPage.tsx`, owning load/save) plus a presentational
  `components/ServiceChannelForm.tsx`; `frontend/` currently has both roles in the component.

## Parity status

`Provenance` records whether the implementation was click-through-blind. **A ported page cannot be
audited** — its result is `n/a (ported)`, and the needs-discussion items are still worth reading
because they came from the deliberate divergences, not from a comparison.

| Route | Last checked | Provenance | Result |
| --- | --- | --- | --- |
| `/integration-hub/service-channels` (SCR-03) | 2026-09-02 | ❌ ported | ⚠️ **NOT AUDITED** · 3 needs-discussion |
| `/integration-hub/service-channels/new` · `/:id` (SCR-04) | 2026-09-02 | ❌ ported | ⚠️ **NOT AUDITED** · 2 needs-discussion |
| `/integration-hub/parameters` (SCR-05) | 2026-09-03 | ✅ blind | **14 defects** · 3 needs-discussion |
| SCR-06 drawer (over `/integration-hub/parameters`) | 2026-09-03 | ✅ blind | **12 defects** · 4 needs-discussion |

> These two rows read `✅ 0 defects` until 2026-09-03. They were **re-classified, not re-run**: both
> pages are copies of the click-through, so the runs compared the reference against itself.
>
> **SCR-03/SCR-04 are NOT being re-implemented** (frontend lead, 2026-09-03) — no rebuild, no
> back-fill audit. The rows stay `NOT AUDITED` as a standing record that they were never
> independently verified against the design; the flag is the deliverable, not a to-do. The only way
> they ever get a real audit is a click-through-blind re-implementation.
>
> **SCR-05/SCR-06 took the other path, and it worked.** They were also ported (2026-09-03), the whole
> pass was **reverted in full**, and US2 was then rebuilt click-through-blind and audited for real —
> the last two rows above. That is what the two paths cost and buy, side by side in one table: the
> ported rows carry no information, the rebuilt rows carry **26 defects and 7 open questions**.

> **Fourth implementation, 2026-09-02 — reported "0 defects", now re-classified NOT AUDITED.**
> Re-implemented from scratch in a fresh session via `/speckit-implement` (T019–T021, T037–T040,
> T042) after the third revert, by **porting** the click-through's `dto.ts` / `http.ts` /
> `useServiceChannels.ts` / `ServiceChannelForm.tsx` / `AllServiceChannelsPage.tsx` /
> `AccessDenied.tsx` / `ScreenPlaceholder.tsx` plus its whole `integrationHub` i18n block, and
> writing only a real `api.ts` against the backend.
>
> This note used to claim "three consecutive 0-defect results from the port-the-design approach,
> against 25 defects from the one written-from-`spec.md` attempt", and that porting the i18n block
> wholesale means "the Text axis cannot drift **by construction**". That last phrase is the tell:
> an axis that cannot drift by construction also **cannot be measured**. The streak was not a
> quality signal — it measured nothing. Withdrawn; see "Workflow rule" at the top.
>
> **The Behaviour axis was driven this time**, on both sides, with
> `behaviour-probe.mjs` (added to this folder alongside `capture.mjs` — reuse it, don't rewrite it):
> ID sanitisation, Supported→Required gating, the live contract-summary counts, empty-submit
> validation, and the contract filter's empty state all returned **byte-identical** results. The
> probe's stub-auth path must click **Sign in** before **Skip for now** — omitting the Sign in click
> leaves it on `/login` and every selector times out (cost a cycle to rediscover; `capture.mjs`
> already had it right).

SCR-03 open decisions (deliberately NOT auto-fixed): the `Waypoints` header icon (removed as part of
the rewrite — click-through has no icon), `data-testid` naming (kept this repo's, the E2E lane selects
on them), and the primary action being a `<Link>` styled with `buttonVariants()` (this repo's
convention, per `KpiManagementPage`) vs the click-through's `<Button onClick={navigate}>`.

SCR-04 open decisions (deliberately NOT auto-fixed): the unsaved-changes `AlertDialog` (frontend only,
but spec **FR-GBL-03** requires it — the click-through looks behind the spec here; no task owns it
either, tracked as **TODO-M13-006**); the P-07 path (frontend renders the form **read-only** with a
notice banner, the click-through returns `AccessDenied` — spec BR-24 favours read-only, and E2E
`M13-E2E-06` asserts it); and preserving contract rows whose parameter has since been disabled (the
click-through drops every non-supported row from the payload). All three re-confirmed 2026-09-02.

After SCR-04's rewrite, `frontend/`'s `ServiceChannelForm.tsx` renders the click-through's
`ServiceChannelFormPage` + `ServiceChannelForm` output from a **single** component — the route wires it
directly (T039). Rendered output is at parity; only the file split differs.

**Gotcha for the next run:** the dev server and backend get restarted often, which invalidates a
captured `session_token` — an unauthenticated capture silently returns the `/login` page and reads as
"the page lost all its content" (`0 headings, 2 controls`). Always re-mint the token, and assert it is
non-empty, before capturing.

**Two more, both cost time on 2026-09-02:**

1. **The click-through's login fields are `#login-email` / `#login-password`** — NOT
   `input[type=email]`/`[type=password]`, which is what `capture.mjs`'s stub-auth path fills. Every
   step there is wrapped in `.catch(() => {})`, so those fills **silently no-op** and it is the
   `Skip for now` click alone that authenticates. That works for `capture.mjs`, but any hand-written
   interaction script must either use the real ids or rely on Skip — and must not conclude "the page
   is empty" when it is actually sitting on `/login`. **The `Sign in` click is not optional either** —
   a probe that fills the fields and jumps straight to `Skip for now` never leaves `/login`, because
   Skip only appears after Sign in. Mirror `capture.mjs`'s order: fill → **Sign in** → wait for Skip →
   Skip.
2. **Navigate the click-through CLIENT-SIDE.** Its auth is in-memory, so a `page.goto` of an in-app
   route bounces straight back to `/login`. `capture.mjs` already handles this with
   `pushState` + a `popstate` event (see its `gotoRoute`); copy that into any behaviour probe rather
   than calling `page.goto` per route. The real frontend does not care (its token is in
   `sessionStorage`), so the same helper is safe for both sides.
3. **Behaviour-probe selectors: use `[role=checkbox]`, not `input[type=checkbox]`.** base-ui renders
   a visually-hidden `<input type=checkbox aria-hidden>` next to the real button; Playwright resolves
   the input first and then spins forever on "element is outside of the viewport". The switch has the
   same shape — target `[role=switch]`. Also, `text=/Contract summary/` matches the inner
   `<span>` label, not the paragraph, so it returns the label with no counts — read the summary off
   `[data-testid=contract-summary]` instead.

**How the real frontend's token was minted (2026-09-02), if you need it again:** POST the seeded
P-01 credentials to `/api/v1/auth/login` (`{username, password}`) for a `challengeId`, then
`/api/v1/auth/mfa/verify` (`{challengeId, totpCode}`) with a TOTP computed from `p01TotpSecret` in
`tests/Nabadat.E2ETests/appsettings.local.json`. That returns `sessionToken`, which is what
`capture.mjs --auth token --token …` seeds into `sessionStorage.session_token`.



## SCR-05 / SCR-06 (M-13 US2) — REAL AUDIT, 2026-09-03 (the blind rebuild)

**The first genuinely independent M-13 audit.** T061–T064 were rebuilt from `spec.md` + `tasks.md` +
the root `CLAUDE.md` with the click-through never opened, and this run is what that buys: **26
defects and 7 needs-discussion items** across the list page and its drawer — where the three earlier
ported runs could only ever produce "0 defects".

**What converged on its own** (worth knowing — it says the design system is carrying real weight):
both sides independently landed the same page skeleton, the same 12 table columns in the same order,
`TabsListSegmented` + `TabsCountPill` for the origin tabs, a `FlagGlyph` exported from
`ParameterDrawer.tsx` in **brand cyan not semantic green**, the ghost icon-only row action, the
`bg-muted` API-field chip, D-6 driven off the 200-`requires_confirmation` shape, and byte-identical
`suggestApiField` output. **Behaviour was driven on both sides** and every assertion matched except
two, both traced to deliberate divergences (see below).

**Where the drift actually was** — almost entirely *chrome*, not logic: the filter row (design puts
tabs + search + type on ONE row with icon-in-field and `sr-only` labels; the rebuild used a separate
labelled filter row), the usage-flag grid (design 2-column, rebuild 1-column), drawer width
(`max-w-lg` vs `max-w-xl`), disabled-row dimming scope, and a missing "Clear filters" button. This is
the shape to expect from a from-spec build: the spec pins copy and behaviour tightly, so what drifts
is the layout the spec does not describe.

**Two places the CLICK-THROUGH is behind, not the code** (business decisions, left untouched):
1. The List panel's **"Open mappings" button** — `spec.md`'s SCR-06 field details name it verbatim;
   the design's List panel is a bare `Alert` without it.
2. The type-filter trigger renders the raw value **`all`** in the design, because its `<SelectValue />`
   has no render prop — the "All types" label the rebuild shows is the design's *own*
   `parameters.typeAll` string.

**Superseded:** the section below described the 2026-09-03 **ported** run on these same two surfaces
and its revert. Kept as the record of why this audit is worth reading.

## SCR-05 / SCR-06 (M-13 US2) — earlier run 2026-09-03, VOID, then reverted

The 2026-09-03 run on these two surfaces reported "0 defects". It measured nothing:
`useParameters.ts`, `ParameterDrawer.tsx`, `AllParametersPage.tsx` and `ui/tabs.tsx` had been
**copied** from the click-through (`cp`, verbatim, then locally amended), so the run diffed the
reference against itself. The SCR-06 drawer came out pixel-identical because it *was* the same
component — a property of the copy, not a finding.

**The whole implementation pass was then reverted** at the frontend lead's request, so US2 can be
rebuilt click-through-blind and audited properly. When that rebuild lands, this section gets
replaced by a real report — expect genuine defects, mostly copy and placeholders.

Two **product** facts the void run did surface, safe to know before a blind rebuild because they are
about `frontend/`, not about the design: `ui/tabs.tsx` ships no `TabsListSegmented`/`TabsCountPill`
(CLAUDE.md requires the segmented control for tabbed pages), and SCR-05 will need the same
hydration-skeleton + FR-GBL-02 access-denied divergences SCR-03 carries.

**Token minting hits MFA anti-replay.** Minting twice inside one 30-second TOTP window fails the
second time with `auth.mfa.invalid_code`, `$TOKEN` comes back empty, and the capture silently reads
the `/login` page as "0 headings, 2 controls". Mint **once** into a file, assert it is non-empty,
and reuse it for every capture in the run (retry across windows if it fails).

**A behaviour probe for SCR-05/06 existed and was deleted with the revert** — deliberately. It named
the design's testids (`range-card`, `list-panel`, `flag-mapping`, `type-option-*`, …), so leaving it
in the repo would have leaked the click-through's structure into the very rebuild that is supposed
to be blind. Re-author it at audit time, after the pages exist, from `behaviour-probe.mjs`.
