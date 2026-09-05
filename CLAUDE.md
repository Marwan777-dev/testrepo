<!-- SPECKIT START -->

For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at [specs/006-integration-hub/plan.md](specs/006-integration-hub/plan.md)

<!-- SPECKIT END -->

<!-- FRONTEND START -->

## Frontend — design system (binding)

For **any** UI / frontend task in `frontend/` you MUST read and follow the **Nabadat
Design System** below before writing TSX, and treat it as binding (Vite + React 19 +
Tailwind 4 + `@base-ui/react` + shadcn-style components).

The styled components live in `frontend/src/components/` (`ui/` primitives, `cx/` feature
components), design tokens in `frontend/src/index.css`, and helpers in
`frontend/src/lib/` (`utils.ts` → `cn()`, `journey-data.ts` → `perfColor`/`perfLevel`).
**Reuse them** per the Component Sourcing Rule below — never recreate what exists.

<!-- FRONTEND END -->

# Nabdat — Design System & Development Guidelines

> **Voice of Customer (VOC) SaaS platform** for enterprise and government.
> The definitive reference for color, typography, logo, and brand voice.
> The interface must feel professional, data-rich, trustworthy, and fast.

## Design Context

### Users

Business analysts, customer success managers, quality teams, and executives in
banking, telecom, and government. Desktop-first. Mixed technical proficiency.
Arabic-speaking primary audience with English as secondary language.

### Brand Personality

**Precise · Human · Action-Oriented · Credible** — Nabadat speaks with clarity
and care. It earns the trust of government officials and enterprise leaders by
being precise, never clinical. Warm, never casual.

### Brand Voice

Four pillars define all UI copy, labels, insights, and AI-generated text:

1. **Precise** — Numbers, percentages, and trends stated clearly. No hedging.
   No vague positives. Data speaks first.
   - DO: "NPS dropped 6 points this month across digital channels."
   - DON'T: "Scores may have shifted somewhat recently."
2. **Human** — Behind every score is a person. Nabadat surfaces feelings, not
   just figures. Tone stays warm even when the data is hard.
   - DO: "Customers want faster responses — 68% mentioned wait time."
   - DON'T: "Response time metric indicates suboptimal performance."
3. **Action-Oriented** — Every insight surfaces a next step. Nabadat doesn't
   just report — it recommends. Passive voice is avoided.
   - DO: "Escalate 14 open complaints in Eastern Branch now."
   - DON'T: "There are complaints that might need attention."
4. **Credible** — No jargon, no buzzwords. Government and banking clients
   demand institutional confidence. Substance over style.
   - DO: "Q3 closed-loop rate: 91% — above sector benchmark."
   - DON'T: "Our AI-powered synergies unlock transformative CX value!"

**Arabic Voice:** Arabic copy must be written natively — never translated from
English. The formal register (Modern Standard Arabic, فصحى) is required for
government and banking contexts. Avoid colloquial dialects in the UI. Tone should
carry the same precision and warmth as the English voice, adapted to Arabic
rhetorical conventions.

### Aesthetic Direction

Nabadat Cyan/Mint brand with navy-tinted dark mode. Clean, data-dense dashboard
style. Professional but not cold — approachable enterprise. Navy-tinted dark mode
that maintains brand identity.

### Design Principles

1. **Data first** — content and data are the hero, not the chrome
2. **Clarity over decoration** — every element must earn its place
3. **Consistency** — same patterns, same spacing, same behavior everywhere
4. **Bilingual by design** — Arabic/English must both feel native, not adapted
5. **Accessible by default** — WCAG AA minimum, always

---

## Self-Maintenance Rule

CLAUDE.md holds two kinds of content with **different ownership**:

- **Frontend / design-system content** (the Nabadat Design System: color, typography,
  components, RTL/bilingual, dark mode, brand voice, layout, data-viz, and all
  DO / DO NOT lists) is a **living document owned by the frontend lead**. The
  email-owner restriction below applies **only** to this content.
- **Backend governance content** (Unit Test Policy, E2E Test Policy, Backend
  Integration, Dev Environment Workflow) mirrors the platform constitution under
  `.specify/memory/` and is owned by the platform/backend governance process. It is
  kept in sync with the constitution by whoever lands the corresponding amendment —
  the frontend-lead email restriction does **NOT** apply to it.

**Owner identification (frontend / design-system content only):** Before modifying a
**frontend / design-system** section, check `git config user.email`.

- If the email is `mrwantarik50@gmail.com` or `m.elsayed@qbs.jo` → this is the owner. Apply updates directly.
- If the email is **anything else** → this is a team member. **Do NOT modify the frontend / design-system sections.**
  Instead, suggest changes as a message so the owner can review.

Only the frontend developer should modify the **frontend / design-system** sections
directly. All other team members (backend, business, COO) should **follow** them but
never change them. Backend governance sections are exempt from this restriction and
track the constitution in `.specify/memory/`.

### For the frontend developer (owner)

Update CLAUDE.md proactively — don't wait to be asked:

- **Bug fix reveals a pattern** — add the rule so it never happens again.
- **New reusable component created** — document it in the Component Sourcing Rule.
- **New page pattern established** — document it for consistency.
- **Integration pattern discovered** — add it to an API Integration section.
- **Design decision made** — document the reasoning.
- **Gotcha or pitfall found** — add a warning to the relevant section.

### For everyone else (backend devs, COO, business team)

**Read and follow CLAUDE.md — but do NOT modify it.** If you discover something that
should be added (a new API endpoint, a pattern, a rule), tell Claude:

> "Suggest this for CLAUDE.md: [your observation]"

Claude will **not** write the change. Instead it will note the suggestion in a comment
or message so the frontend lead can review and apply it. This prevents conflicting
rules from different team members.

### Protected sections (never modify without frontend lead approval)

- Color System (brand palette, D1–D5, Two-Palette Rule)
- Typography (fonts, scale, weights)
- Spacing & Border Radius
- Component Sourcing Rule
- Dark Mode rules
- Brand Voice
- DO / DO NOT lists

### When NOT to update

- Ephemeral fixes (typos, one-off data changes)
- Things already derivable from reading the code
- Temporary workarounds that will be removed

### How to update (frontend lead only)

Add the rule to the **most relevant existing section**. If no section fits, create a new one.
Keep entries concise — one line per rule where possible. Always explain **why** the rule exists,
not just what it is.

---

## Stack

- React 19 + TypeScript + Vite
- Tailwind CSS v4 + shadcn/ui
- React Router for navigation
- Recharts (via shadcn chart wrapper) for data visualization
- Lucide React for icons (never use emojis as icons)

### Recommended Claude Code Plugin

For higher design quality when generating pages and components, install the
**Impeccable** frontend-design plugin. It enforces creative, polished output that
avoids generic AI aesthetics. Install it via Claude Code plugin manager.
This is optional — the design system rules below are sufficient on their own.

---

## Component Sourcing Rule

When a prompt requires a component or visualization:

0. **Search the existing codebase FIRST** — before building anything, search `src/components/`
   and `src/pages/` for existing components that already do what's needed. If a KPI card,
   gauge, sentiment chart, or any reusable component already exists, **import and reuse it**.
   Never recreate functionality that already exists in the codebase.
1. **Search shadcn/ui second** — check if shadcn offers the component (e.g. `npx shadcn@latest add <component>`). If it does, install it and use it.
2. **Search shadcn Charts** — for any chart or data visualization need, check [shadcn Charts](https://ui.shadcn.com/charts) which wraps Recharts with theme-aware `ChartContainer`, `ChartTooltip`, `ChartLegend`, etc. Always prefer the shadcn chart wrapper over raw Recharts.
3. **Search Recharts directly** — if shadcn Charts doesn't cover the chart type, use Recharts directly but still wrap it in `ChartContainer` for consistent theming.
4. **Only then build custom** — if none of the above cover the need, build the component from scratch.

> **Anti-pattern:** Creating a new inline KPI card when `<KpiFlipCard>` already exists.
> Always `import` from existing components rather than duplicating their logic.

### Click-through-blind implementation (HARD RULE)

Build frontend pages from `spec.md` + `tasks.md` + this design system. **Never open, read, or copy
from the click-through checkout while implementing.** The click-through is the **audit** reference,
read only by `/clickthrough-parity` **after** the story's E2E checkpoint.

**Why:** a parity audit only carries information when the two sides were built **independently**.
Porting the click-through's components (or its i18n block) makes the run diff a file against its own
source — it reports "identical" regardless, and real drift becomes invisible. A ported page is
reported **NOT AUDITED**, never "0 defects".

This is not hypothetical. Across 2026-09-02/03, four M-13 routes (SCR-03/04/05/06) were implemented
by copying the click-through's components and i18n block, then reported "0 defects", and a
"consecutive 0-defect streak" was recorded as if it were a quality signal — it measured nothing. All
four are now marked NOT AUDITED. A from-spec build of SCR-03/04 had surfaced **25 real defects**;
that was the useful output, and porting traded it away for an empty report.

The correct loop, per frontend story:
```
implement from spec.md  (click-through NOT opened)
        ↓
E2E checkpoint green
        ↓
/clickthrough-parity <feature> phase <N>   ← an assigned TASK in tasks.md; first look at the design
        ↓
frontend lead triages the defect list and decides what to --fix
        ↓
… once every story is done …
        ↓
/clickthrough-parity <feature>             ← the full-module task (bare feature, NO phase)
        ↓
record-audit.py stamps it → `git push` to main/master unblocks
```
**The parity run is assigned work, not an automatic step (2026-09-03).** `/speckit-tasks` emits a
`Click-through Parity for User Story X 🎨` subsection after each page-bearing story's E2E
subsection, plus one **full-module** task in the Polish phase; the frontend developer runs them by
hand when ready to triage. Nothing fires the audit for you — a defect list that arrives when nobody
is sitting down to act on it just gets scrolled past. The one hard gate is at release:
`.claude/hooks/parity-gate.py` blocks a push to `main`/`master` until the finished module has a
current whole-module stamp. Feature-branch pushes and backend-only modules are never gated.
Expect real defect lists (mostly copy and placeholders) and rework to close them. That is the point:
drift you cannot see is the expensive kind. Full rules in
`.claude/skills/clickthrough-parity/{SKILL.md,USAGE.md,route-map.md}`.

### When to use Recharts vs Custom SVG

Recharts is appropriate for **standard data charts**: line charts, bar charts, area charts, stacked
charts, and basic pie charts — anywhere the data shape is tabular and the visual is conventional.

**Always use custom SVG** for these visualization types — Recharts cannot achieve the required
visual richness:

- **KPI Gauges** — semicircular dual-ring gauges with colored zones (red→amber→green), needle
  dot indicator, target arrow marker, and gradient depth
- **Spider/Radar charts** — radial gradient fills (red center → green outer showing performance
  zones), per-vertex colored dots based on value, white cutout between boundary and data polygon
- **Sentiment ring charts** — donut segments with tri-color sentiment breakdown
- **Topic bubble charts** — sized circles with sentiment-ring strokes
- **Performance indicators** — any visualization that needs zone coloring, gradients, or
  needle/marker positioning

The rule is: **if the visualization needs gradients, zones, needles, per-element color coding,
or depth effects → custom SVG. If it's a standard x/y data plot → Recharts.**

### After sourcing, always apply the Nabdat design system:

- **Colors**: Replace any default/generic colors with Nabadat brand tokens (`bg-primary`, `text-nb-cyan`, `fill-chart-1`, etc.). Never ship a component with default blue/grey colors. Follow the Two-Palette Rule (see Color System).
- **Dark mode**: Add `dark:` variants using the navy-tinted dark palette — never leave components unstyled in dark mode.
- **Typography**: Use `font-sans` / `font-heading` / `font-mono` — never inherit external font families.
- **Borders & Radius**: Use `border-border`, `rounded-md` (12px) / `rounded-lg` (20px) / `rounded-xl` (32px) — match the system radius scale.
- **RTL**: Ensure the component uses logical properties (`ps-*`, `pe-*`, `ms-*`, `me-*`, `text-start`, `text-end`).
- **Spacing**: Follow the 4px grid spacing scale (`p-6` for cards, `gap-4` / `gap-6` for grids, `space-y-4` for form fields).

### Custom SVG Visualizations

When Recharts doesn't cover a chart type (e.g. bubble/ring sentiment charts), build custom SVG:

- Use `stroke-dasharray` + `stroke-dashoffset` on `<circle>` elements for donut/ring segments
- Use `transform="rotate(-90, cx, cy)"` to start arcs from 12 o'clock
- Wrap in a `<svg viewBox="...">` with `className="w-full h-full"` for responsiveness
- Always add `role="img"` and `aria-label` to the `<svg>` for accessibility
- Use CSS variables from the design system (`var(--color-nb-cyan)`, etc.) for stroke/fill colors
- **Neutral SVG chrome MUST be theme-aware — never a hardcoded light-slate hex (dark-mode
  gotcha).** Only _status_ colors (`perfColor` / the D1–D5 constants) may be raw hex in an SVG
  `fill`/`stroke`, because those are fixed across themes. Everything **neutral** — unfilled
  gauge/ring **tracks**, gridlines, **tick marks**, needle-pivot dots, and axis / center /
  min-max **labels** — must adapt to dark mode. Put a Tailwind utility on the SVG element
  (`className="stroke-muted/20"`, `fill-muted-foreground`, `stroke-foreground`, `stroke-card`
  for a contrast ring) instead of a `stroke="#F1F5F9"` / `fill="#94A3B8"` attribute. A
  hardcoded slate track is invisible-or-wrong on the navy dark surface. (This is why the
  "allowed hex in JS" note covers `perfColor`/data-viz _status_ values only — not neutral
  track/label chrome.)

### KPI Gauge Design Spec

KPI gauges must be visually rich — dual-ring semicircular design:

- **Outer ring** (thick, ~6% of size): shows filled value arc in the KPI's brand color,
  unfilled portion in `stroke-muted/20`
- **Inner ring** (thin, ~1.5% of size): three colored zones — red (0-33%), amber (33-55%),
  green (55-100%) — these show "bad / average / great" thresholds
- **Needle dot**: small filled circle at the current value position on the outer arc, white
  stroke for contrast
- **Target marker**: tick line + arrow at the target position on the arc, with small "T" label
- **Center text**: large value (e.g., "+42", "78%") + small label below (e.g., "NPS", "CSAT")
- **Min/Max labels**: at arc endpoints (e.g., "-100" and "+100" for NPS). **Pad the `viewBox`
  horizontally so these clear the arc — don't let them clip.** End labels sit _outside_ the arc
  radius at the far left/right, and a 3–4-char number (`100`, `−100`, `+100`) centered there
  overflows a tight viewBox (numbers came out cut to `10` / `-100`→`100`). Give the `viewBox` a
  negative min-x and extra width (e.g. `viewBox="-14 -2 248 136"` for a 220-wide gauge) and set
  the label radius a couple px beyond the ring (`R_OUTER + 18`, not `+16`) so labels read as
  spaced, not stuck to the arc. The gauge scales down a hair — correct trade for un-clipped labels.
- Below gauge: **3-tile segment grid** (e.g., Promoters 51% | Passives 40% | Detractors 9%),
  each tile with colored background tint matching its category
- Below segments: change indicator (▲/▼ + value) + total responses count

### Spider/Radar Chart Design Spec

The KPI overview spider chart must use a custom SVG with:

- **Radial gradient fill**: red (center, opacity 0.6) → orange → amber → green (outer,
  opacity 0.2) — showing performance zones from bad to great
- **White cutout**: use `fill-rule="evenodd"` to create a white mask between the outer
  boundary polygon and the data polygon — only the data area shows the gradient
- **Per-vertex colored dots**: each KPI vertex gets a dot colored by its performance level
  (≥85 green, ≥75 lime, ≥60 amber, ≥45 orange, <45 red)
- **Value labels**: shown next to each dot, colored to match the dot
- **Grid levels**: polygons at 20, 40, 60, 80, 100 with subtle stroke
- **Axis labels**: KPI names positioned outside the outer polygon

> **In short:** Never drop in a third-party component with its default styling. Every component must look like it was designed for Nabdat from the start.

---

## Data Visualization — Advanced Patterns

These patterns apply to **all future charts and visualizations** in the app. Any developer
building a new chart must follow these specs to maintain the premium data-rich feel.

### Journey Map — Stage Columns (Customer Journey)

The Journey Map renders each stage as a **vertical column**, and every column is
**color-coded by the stage's score on the D1–D5 scale — five colors, one per band**.
This is mandatory, not optional styling:

- **Column background = a lighter shade of the stage's status color:**
  `bg-d{n}-light dark:bg-d{n}-dark/25`, where `n = perfLevel(stage.score)` (from
  `journey-data.ts`). E.g. 93% → d1 (pale green), 80% → d2 (green), 65%/67% → d3 (pale
  amber). The five D-levels give the five stage colors.
- **Score % (column header) = the matching deep shade:** `text-d{n}-dark
dark:text-d{n}-light`.
- **No / zero score →** neutral column (`bg-muted/30`), render the score as `—` — never
  default to D5 red.
- **Emotion bubble:** white circle (`bg-card`) with a colored ring in the full-strength
  `perfColor(score)`. The emotion label below ("excited", "anxious", …) uses
  `text-foreground font-semibold` for contrast on the tint — **never**
  `text-muted-foreground`.
- **Connecting curve** across the columns uses the brand **Mint→Cyan** gradient (~0.5
  opacity), drawn behind the bubbles.
- Columns are flush with `divide-x divide-border`; tags inside follow the white-fill
  outline Badge rule.

> The score color and its background must come from the **same** `perfLevel(score)` so
> they never disagree. Full token mapping + code in **Color System → Performance Color
> Scale → "Status-tinted containers"**.

### Trend Chart Annotations

When a time series chart tracks KPIs over time and there are **actions/events** that were
taken during that period, mark them on the chart:

- Use Recharts `ReferenceDot` for action markers: colored circle (r=6) with white stroke at
  the event's x/y position on the chart
- Use Recharts `ReferenceLine` with `strokeDasharray="4 4"` for vertical dotted lines at
  the event week
- Wrap markers in a `Tooltip` showing: action name, date, and target KPI impacts
  (e.g., "IVR Redesign — Week 4 | Targets: CES −0.4, FCR +5%, CSAT +3%")
- Color-code markers by action type (gold for operational changes, blue for training, teal
  for tech improvements)
- Always include a small legend explaining what the dots mean

### Distribution / Progress Bars

When showing percentage breakdowns (e.g., Promoters/Passives/Detractors, Satisfied/Neutral/Dissatisfied):

- Use **horizontal bars** with `rounded-full` ends
- Each bar width = proportional to its percentage value
- Color each bar by its category (green for positive, amber for neutral, red for negative)
- Show the **percentage label** inside the bar (if wide enough) or beside it
- Add a small **category label** to the left/start side
- Bars should have a subtle background track (`bg-muted/20`) so empty space is visible
- Animate bar width on mount: `motion-safe:transition-all motion-safe:duration-700`

> **Sentiment-graded vs categorical — the chart-colouring decision (Two-Palette Rule
> in practice).** A distribution's colours depend on **what the buckets mean**, and this is
> the single most common data-viz mistake:
>
> - **Sentiment / rating scales** (KPI question 5→1, Scale stars/points, Promoters/Passives/
>   Detractors) → colour each bar by **rank on the D-scale**, green→red best→worst
>   (`D1…D5` mapped by index: `RAMP[Math.round(i/(n-1)*4)]`). Value text is "%" only.
> - **Categorical breakdowns** (single-select options, multi-select options, **delivery
>   channels**, reasons) → colour by the **brand `chart-1…5` palette**, NEVER `perfColor`.
>   Painting a channel red because its completion rate is "low" is a Two-Palette violation
>   _and_ misreads as an alarm — a channel is a category, not a status. Multi-select value
>   text is "count · %"; single/channel legends show "%".
>
> Rule of thumb: **if a lower value is genuinely "worse", grade it D-scale; if the buckets
> are just different things, use brand chart colours.** `perfColor` on a categorical
> breakdown is always wrong.

### Segment / Branch Breakdown Bars

When showing values per location, channel, or segment (e.g., branch NPS scores):

- Each row: label on start side, mini **progress bar** in the middle, score value on end side
- Bar width = proportional to the **maximum value** in the dataset (not 100%)
- Color each bar using `perfColor(value)` — green for high, amber for mid, red for low
- Bar height: `h-2` with `rounded-full`
- Score value: `font-bold tabular-nums` colored to match the bar
- Rows sorted by value descending (best at top)
- Maximum 6-8 rows — if more exist, show a "View all" link

### Donut / Reasons Charts

For categorical breakdowns shown as donut charts (e.g., reasons analysis on KPI flip cards):

- Use Recharts `PieChart` with `Pie` component, `innerRadius` ~60% of `outerRadius`
- **Center label**: large value or metric name (e.g., "+42" or "NPS") using Recharts `Label`
- **Segment stroke**: use `stroke="var(--color-card)"` for clean separation between slices
- **Legend below chart**: vertical list with color dots + labels + percentage values
- Max 4-5 slices — combine anything smaller than 5% into "Other"
- Animate on mount with Recharts' built-in animation props

### Chart Card Styling

Every card that contains a chart or visualization must feel **elevated**:

- Card: `shadow-sm` in light mode, `dark:shadow-none` (borders provide depth in dark)
- Card header: `CardTitle` + `CardDescription` — always explain what the chart shows
- Card content: adequate padding (`p-6`) so the chart doesn't touch edges
- Hover: `hover:shadow-md motion-safe:transition-shadow` on interactive chart cards
- Chart height: use Tailwind height classes (`h-64`, `h-72`, `h-80`) not arbitrary values
  where possible. For custom SVG charts, use `viewBox` + `width="100%"` for responsiveness

### Comparison Charts (Current vs Previous Period)

When showing two time periods side by side (e.g., customer journey current vs previous):

- **Current period**: solid line, `strokeWidth={2.5}`, with gradient area fill below
  (`linearGradient` from 30% opacity at top to 5% at bottom)
- **Previous period**: dashed line (`strokeDasharray="6 3"`), `strokeWidth={2}`, no fill
- Both lines should have **data point dots** (small circles at each data point)
- Include a **legend** distinguishing current (solid) vs previous (dashed)
- If a stage shows significant decline, add a **warning badge** (AlertTriangle icon +
  description of the decline)

### Funnel Visualizations

For conversion funnels (e.g., survey impressions → completed):

- Show as **stacked horizontal bars** with decreasing widths
- Each bar: `rounded-full` ends, height `h-3`
- Color progression: light shade at top → darker shade at bottom (e.g., blue-lite → blue)
- Show **value** (e.g., "4,820") and **conversion rate** (e.g., "67% conv.") per step
- Add a **completion rate badge** at the bottom summarizing overall conversion
- Bars should animate width on mount

---

## Arabic & Bilingual Support

### RTL-First Architecture

The app is **RTL by default** (`dir="rtl"` and `lang="ar"` on `<html>`).
All layouts must be built RTL-first, then verified in LTR.

### Mandatory Logical Properties

**Never use physical direction properties.** Always use logical equivalents:

```tsx
// Correct — works in both RTL and LTR
className = "ps-4 pe-2 ms-auto me-0 text-start rounded-s-lg border-e";

// Wrong — breaks in RTL
className = "pl-4 pr-2 ml-auto mr-0 text-left rounded-l-lg border-r";
```

| Physical (NEVER use)          | Logical (ALWAYS use)          |
| ----------------------------- | ----------------------------- |
| `pl-*` / `pr-*`               | `ps-*` / `pe-*`               |
| `ml-*` / `mr-*`               | `ms-*` / `me-*`               |
| `left-*` / `right-*`          | `start-*` / `end-*`           |
| `text-left` / `text-right`    | `text-start` / `text-end`     |
| `rounded-l-*` / `rounded-r-*` | `rounded-s-*` / `rounded-e-*` |
| `border-l-*` / `border-r-*`   | `border-s-*` / `border-e-*`   |
| `scroll-pl-*` / `scroll-pr-*` | `scroll-ps-*` / `scroll-pe-*` |

**Exception:** `mx-auto` and `px-*` (symmetric padding) are safe to use.

### Arabic Typography Rules

- Arabic text should **never be justified** (`text-justify`) — it distorts letter connections
- Minimum body text size: `text-sm` (14px). Never use `text-xs` for Arabic body text
- Arabic reads more comfortably with slightly more line-height: use `leading-relaxed` (1.625) for Arabic body text
- Numbers in Arabic UI: use Western digits (0-9) not Eastern (٠-٩) unless the client specifically requests it
- Never break Arabic words with `break-all` — use `break-words` or `overflow-wrap: break-word`

### Bilingual Text Patterns

When the app supports both Arabic and English:

```tsx
// Directional-aware icon placement (icon before text in reading direction)
<Button>
  <span>إنشاء تقرير</span>
  <Plus className="size-4 ms-2" /> {/* ms-2 flips automatically */}
</Button>

// Numbers mixed with Arabic text — wrap numbers in LTR span
<p>تم استقبال <span dir="ltr" className="inline-block">1,234</span> استبيان</p>

// Thousands-separated counts in a label — interpolate a PRE-FORMATTED string via a
// NON-`count` key (e.g. {{n}}), never i18next's magic {{count}}.
//   key: "qBadgeKpi": "{{n}} responses"
t("…qBadgeKpi", { n: value.toLocaleString("en-US") })  // → "3,180 responses"

// Date formatting — position differs per locale
<time>{isArabic ? "١٢ مايو ٢٠٢٦" : "May 12, 2026"}</time>

// Stacked bilingual name (primary + secondary opposite-script) — use <bdi>, NOT a dir'd span
<div className="flex min-w-0 flex-col items-start">
  <bdi dir={primaryDir} className="max-w-full truncate text-start font-semibold">{primary}</bdi>
  <bdi dir={secondaryDir} lang={secondaryLang} className="max-w-full truncate text-start text-sm text-muted-foreground">{secondary}</bdi>
</div>
```

**Two-name cells: `<bdi>` + `text-start`, never a `dir`-flipped `<span>`.** When a table cell
stacks a primary + secondary name where the secondary is the _opposite_ script (English name
under an Arabic one, or vice-versa), a `<span dir="rtl">` sets that span's writing direction,
so its `text-align: start` resolves to the **opposite** edge — the secondary name jumps to the
far side of the cell and reads as broken (seen on the Personas table). Use `<bdi>` (bidi
isolation: correct glyph shaping per its own `dir`) as a content-width flex item under
`items-start`, with explicit `text-start` — both names then hug the cell's start edge while
each still renders in its own direction.

**Why `{{n}}`, not `{{count}}`:** i18next treats `count` as the **plural selector** — it runs
`Number(count)` (which is `NaN` for a `"3,180"` string) to pick a plural suffix, and it does
**not** thousands-format the value. So `{{count}}` gives you `3180` (no comma) or plural
breakage. Interpolate a plain `{{n}}` with a `.toLocaleString()`-formatted string instead.

### Content Guidelines for Arabic

- Use Modern Standard Arabic (فصحى) for UI labels, not dialect
- Keep labels concise — Arabic labels are typically 30-40% longer than English
- Test with long Arabic text to ensure truncation works properly
- Always provide `lang` attribute when mixing languages: `<span lang="en">VOC</span>`

---

## Color System

### Two-Palette Rule (THE HARD RULE)

Nabadat uses **two separate color palettes** that must NEVER be mixed:

| Brand Palette (Mint · Cyan · Navy) | Semantic Palette (D1 · D2 · D3 · D4 · D5) |
| ---------------------------------- | ----------------------------------------- |
| Marketing and communications       | KPI status indicators only                |
| Product chrome and navigation      | Dashboard severity states                 |
| UI components and buttons          | Performance threshold badges              |
| Illustrations and hero backgrounds | Alert and notification states             |
| Chart series colors (non-semantic) | Closed-loop case status                   |
| Logo and identity elements         | Survey score bands                        |

> **Brand colors never signal status.**
> **Semantic colors never appear decoratively.**

### Brand Colors (`nb-*` prefix)

The Nabadat brand palette — use for chrome, navigation, CTAs, chart series:

```
Cyan family:     bg-nb-cyan             #0D8BBC   (primary brand, CTAs, active nav, headline KPIs)
                 bg-nb-cyan-100         #CCF0FB   (subtle tint)
                 bg-nb-cyan-200         #98E1F2   (hover tint)
                 bg-nb-cyan-300         #59CCEA   (light accent)
                 bg-nb-cyan-700         #0087A8   (hover/pressed)
                 bg-nb-cyan-800         #005F7A   (deep)
                 bg-nb-cyan-900         #003A4D   (deepest)

Mint family:     bg-nb-mint             #13DB9B   (secondary brand, accents)
                 bg-nb-mint-100         #CBF5EB   (subtle tint)
                 bg-nb-mint-200         #96EDD4   (hover tint)
                 bg-nb-mint-300         #54DE8C   (light accent)
                 bg-nb-mint-700         #0DA670   (hover/pressed)
                 bg-nb-mint-800         #07704E   (deep)
                 bg-nb-mint-900         #034530   (deepest)

Navy family:     bg-nb-navy             #1E2235   (wordmark, dark surfaces, text)
                 bg-nb-navy-100         #E3EBF4   (subtle tint)
                 bg-nb-navy-200         #C4CADD   (borders)
                 bg-nb-navy-300         #8B90A5   (secondary text)
                 bg-nb-navy-700         #161B29   (deep)
                 bg-nb-navy-800         #0E1018   (deeper)
                 bg-nb-navy-900         #070B10   (deepest)
```

**Named Gradient:** Always runs Mint → Cyan (`#1EC99A → #00B4D8`).
Never reverse the gradient direction (Cyan → Mint) in LTR layouts.

**Neutral Palette:**

```
bg-nb-dark         #0D0F14   (darkest surface)
bg-nb-dark-2       #1B1C27   (elevated dark surface)
bg-nb-dark-3       #2E3044   (dark borders/dividers)
bg-nb-stone        #7A8196   (muted text, icons)
bg-nb-stone-lt     #B8BFCE   (disabled states, captions)
bg-nb-cloud        #EEF1F7   (subtle backgrounds, table rows)
```

### Semantic KPI Colors (D1–D5 Scale)

Shared standard with QBS. This is a universal KPI language — NOT a brand color.
Enterprise clients who use both Nabadat and QBS see consistent KPI signaling.

| Degree | Name      | Base      | Light Token | Dark Token | Threshold                                                |
| ------ | --------- | --------- | ----------- | ---------- | -------------------------------------------------------- |
| D1     | Excellent | `#1A7A3C` | `#D4F4E2`   | `#0D4A24`  | >110% target · Revenue over target · NPS above benchmark |
| D2     | Good      | `#2EB85C` | `#C8F5DB`   | `#156632`  | 90–110% · Pipeline health · CSAT on track                |
| D3     | Caution   | `#E8A020` | `#FFF0CC`   | `#7A5000`  | 70–89% · Drifting NPS · Response time rising             |
| D4     | Warning   | `#E05C1A` | `#FFE4D0`   | `#7A2800`  | 50–69% · SLA breach risk · Escalation queue full         |
| D5     | Critical  | `#C01B2A` | `#FFD6DA`   | `#6B0010`  | <50% · System failure · Major complaint surge            |

**Token Usage:**

- **Base color** — text values, icons, dot indicators
- **Light token** — badge backgrounds, row fills
- **Dark token** — text ON light-token backgrounds
- **Hover token** (`d{n}-hover`) — a darker step of the _light_ token for hover on a
  D-tinted surface (e.g. `bg-d2-light hover:bg-d2-hover`). Only `--color-d2-hover`
  exists today; add `--color-d{n}-hover` for other levels as needed — never reach for a
  raw `hover:bg-[#hex]`.
- **Scale direction** — always D1 (deep green) to D5 (deep red)

**Hard Rules:**

- Never use semantic colors for chart series or decorative fills
- Never skip degrees — show actual state even if it jumped to D5
- D1 and D5 must be rare — overuse destroys meaning
- D2 should be the most common state on a healthy dashboard

### Semantic shadcn Colors (use these first)

These map to shadcn utility classes. Always prefer semantic tokens over brand tokens:

| Token                       | Light Mode          | Dark Mode                      | Usage                                                                                                                    |
| --------------------------- | ------------------- | ------------------------------ | ------------------------------------------------------------------------------------------------------------------------ |
| `bg-primary`                | Cyan `#0D8BBC`      | Cyan-lite `#59CCEA`            | CTAs, active nav, headline KPI values                                                                                    |
| `text-primary-foreground`   | White               | Navy `#1E2235`                 | Text on primary backgrounds                                                                                              |
| `bg-secondary`              | Mint `#13DB9B`      | Mint-lite `#54DE8C`            | Secondary accents, chart series 1                                                                                        |
| `text-secondary-foreground` | White               | Navy `#1E2235`                 | Text on secondary backgrounds                                                                                            |
| `bg-accent`                 | Cloud `#EEF1F7`     | Navy-dark `#161B29`            | Feature sections, highlights                                                                                             |
| `text-accent-foreground`    | Navy `#1E2235`      | Cloud `#EEF1F7`                | Text on accent backgrounds                                                                                               |
| `bg-muted`                  | Cloud `#EEF1F7`     | Navy-800 `#0E1018`             | Subtle backgrounds                                                                                                       |
| `text-muted-foreground`     | Stone `#7A8196`     | Stone-lite `#B8BFCE`           | Secondary text, captions                                                                                                 |
| `bg-destructive`            | D5 base `#C01B2A`   | D5 lite `#FFD6DA`              | Errors, delete actions                                                                                                   |
| `border-border`             | Navy-200 `#C4CADD`  | Navy tint `rgba(30,34,53,0.3)` | All borders                                                                                                              |
| `bg-background`             | Off-white `#F7F9FC` | Dark `#0A0C11`                 | Page background — lightly navy-tinted, never pure white; just enough that white cards lift without the page reading grey |
| `text-foreground`           | Navy `#1E2235`      | Off-white `#EDEDEF`            | Primary body text                                                                                                        |
| `bg-card`                   | White               | Dark-2 `#1B1C27`               | Card surfaces                                                                                                            |

### Multi-Tenant Theming (themeable vs fixed tokens — THE THEMING RULE)

Nabadat is multi-tenant: each tenant's brand is driven by a small **seed** of colors
stored per tenant, expanded at runtime into the full variable set. For that to work,
**new pages and components MUST be built so a tenant theme reskins them with zero code
change.** Three tiers of color, by whether they re-theme:

1. **Themeable — semantic tokens (USE THESE for anything brand/chrome).** `bg-primary`,
   `bg-secondary`, `bg-accent`, `bg-muted`, `bg-card`, `bg-popover`, `text-foreground`,
   `border-border`, `ring`, `fill-chart-1…5`, and all `*-sidebar*` tokens read CSS
   variables (`--primary`, …) that are overridden per tenant. A card built only from
   these re-themes for free.
2. **Themeable — brand `nb-*` shades (now tenant-aware).** `bg-nb-cyan`, `text-nb-mint-700`,
   etc. resolve through `--color-nb-* → var(--nb-*)` (the raw `--nb-*` ramps live in a
   `:root` block in `index.css`), so a tenant theme re-skins all of them by overriding the
   `--nb-*` vars — **no component changes**. Keep using `bg-nb-cyan-700` freely; it follows
   the tenant brand automatically. **Raw hex is still forbidden** (`bg-[#C8F5DB]` → use the
   `d{n}-*` token; `bg-[#0D8BBC]` → use `bg-nb-cyan` or `bg-primary`).
   **Stays FIXED (never themed):** the `nb-gradient-*` tokens (logo gradient — non-negotiable)
   and the D1–D5 scale. Use `nb-gradient-*` for the logo so it never re-themes; decorative
   gradients may use `from-nb-mint`/`to-nb-cyan` and will follow the tenant.
3. **Global constants — NEVER tenant-themed.** The **D1–D5 KPI scale** and
   **`destructive`** (which _is_ D5) are semantic _meaning_, not branding: red = critical
   for every tenant, consistent with QBS. They live permanently in `index.css` and the
   theming engine never emits or overrides them.

**Engine (`src/lib/theme/`):** a `TenantThemeSeed` is **3 required** (`primary`,
`secondary`, `neutral`) **+ 3 optional** (`sidebar`, `accent`, `background`) colors.
`deriveThemeVars(seed)` expands it (OKLCH ramps, contrast-picked `*-foreground`,
`chart-*`, neutrals, dark-mode variants); `buildThemeCss(seed)` serializes to
`:root{…} .dark{…}`; `applyTheme(seed)` injects a `<style id="tenant-theme">` (client),
or inject the same string server/edge-side for zero-flash first paint. **The default
Nabadat theme is NOT generated** — it stays pinned in `index.css` verbatim, so the
default app is pixel-identical; derivation only runs for non-default tenants. The boot
path is `loadCurrentTheme()` in `main.tsx`, calling `GET /api/theme/current` (relative,
through the `/api` proxy with `xfwd` so the backend resolves the tenant by subdomain).

**Theming self-review — run on every new page/component (and when reviewing one).**
Two regex searches (VS Code regex mode, or `grep -rnoE … src --include='*.tsx'`) tell you
if a page is tenant-safe. Don't search a bare `#` — it's ~90% noise (`url(#id)`, etc.).

1. **The hard rule — MUST return 0:** `-\[#[0-9a-fA-F]{3,8}\]`
   Hex used as a Tailwind class (`bg-[#0D8BBC]`). This bypasses tokens and never
   re-themes. **Any hit is a defect** — replace with a token (`bg-primary` /
   `bg-nb-cyan-700` / `bg-d2-light` / `text-foreground`).
2. **Judgment check:** `style=\{\{[^}]*#[0-9a-fA-F]{6}`
   Hex in an inline `style={{…}}`. Allowed **only** for an intentionally-fixed surface;
   if it's a Nabadat brand color that should follow the tenant, move it to a token/class.

**Allowed hex (NOT violations) — leave these alone:**

- `fill="#…"` / `stroke="#…"` / `url(#id)` — SVG attributes & gradient/filter refs.
- `perfColor` D-scale constants and chart/data-viz hex in **JS** — must be JS values
  (can't be Tailwind classes); this is the documented SVG/canvas pattern.
- **Third-party brand mockups** — external-app chrome (e.g. a WhatsApp/device preview).
  These simulate an external app and must stay fixed; they are NOT Nabadat UI.

**New-page rule of thumb:** every color is a token (`semantic` → `nb-*` → `d{n}-*`);
zero hex in `className`; inline-`style` hex only for fixed third-party/device mockups.

### Chart Colors

Charts use `chart-1` through `chart-5` — always brand palette, NEVER semantic:

- Chart-1: Cyan `#0D8BBC` (use for primary data series inside dashboards — safer near semantic states)
- Chart-2: Mint `#13DB9B`
- Chart-3: Navy-300 `#8B90A5`
- Chart-4: Cyan-700 `#0087A8`
- Chart-5: Mint-700 `#0DA670`
- Use `fill-chart-1`, `stroke-chart-2`, etc.
- Always include a legend — never rely on color alone
- Add patterns/textures as secondary differentiator for accessibility

### Performance Color Scale (uses D1–D5 semantic tokens)

When values represent performance (KPIs, scores, percentages), always color-code
them dynamically using the **D1–D5 semantic scale** — never use brand colors:

```tsx
// D1–D5 semantic colors for performance indication
const D1 = "#1A7A3C"; // Excellent
const D2 = "#2EB85C"; // Good
const D3 = "#E8A020"; // Caution
const D4 = "#E05C1A"; // Warning
const D5 = "#C01B2A"; // Critical

function perfColor(value: number, kpiId?: string): string {
  // KPI-aware thresholds — different metrics have different "good" ranges
  // NPS: 50+ is great (scale is -100 to +100, segment values are 20-60)
  // CES: LOWER is better (30 is great, 60+ is poor — inverted)
  // Default: standard 0-100 percentage scale

  if (kpiId === "ces") {
    // Inverted — lower effort = better
    if (value <= 30) return D1;
    if (value <= 40) return D2;
    if (value <= 50) return D3;
    if (value <= 60) return D4;
    return D5;
  }
  if (kpiId === "nps") {
    if (value >= 50) return D1;
    if (value >= 40) return D2;
    if (value >= 30) return D3;
    if (value >= 20) return D4;
    return D5;
  }
  // Default (CSAT, Agent, VFM, FCR — all 0-100%)
  if (value >= 85) return D1;
  if (value >= 75) return D2;
  if (value >= 60) return D3;
  if (value >= 45) return D4;
  return D5;
}
```

**Always pass `kpiId` when coloring segment/branch bars** so that NPS 52 shows
D1/green (great for NPS) while the same value 52 for CES shows D3/amber (poor for CES).

**Status-tinted containers — use the D-scale light tokens.** When a container
_represents_ a scored entity — a journey-report **stage column**, a touchpoint card,
a segment row — its background must reflect the entity's status color in a **clearly
visible lighter shade**, and its headline number in the matching deep shade. Drive
both off the **same** D-level via `perfLevel(value, kpiId?)` (in `journey-data.ts`,
returns `"d1"…"d5"`), then map to the real D-scale tokens:

```tsx
const level = perfLevel(stage.score ?? 0)          // "d1".."d5"
const BG  = { d1:"bg-d1-light dark:bg-d1-dark/25", /* …d2–d5 */ }
const PCT = { d1:"text-d1-dark dark:text-d1-light", /* …d2–d5 */ }
<div className={cn("…", BG[level])}>
  <span className={PCT[level]}>{stage.score}%</span>
</div>
```

Light mode uses the pale `d{n}-light` fill with the deep `d{n}-dark` text; dark mode
uses a soft `d{n}-dark/25` tint with the `d{n}-light` text. Prefer this token approach
over a faint hex-alpha tint — the light tokens are legible and read as a real heat-map
(a ~8% alpha wash looks washed-out and grey). Score `0`/no-score → neutral
(`bg-muted/30`, `text-muted-foreground`, render "—"), not D5 red. This keeps the
Two-Palette Rule intact (semantic D-tokens signal state, never decorate). Bar _tracks_
stay neutral (`bg-muted/40`); it is the stage/segment _container_ that takes the tint.

**Labels sitting on a tinted container need real contrast.** Small captions/status
labels (e.g. the stage emotion label "excited"/"anxious") on a `d{n}-light` background
must use `text-foreground` (and `font-semibold` at tiny sizes) — **not**
`text-muted-foreground`, whose stone-grey fails contrast on a pale tint. Raise the
text contrast; don't touch the container background.

> `perfTint(value)` (low-alpha hex `{ bg, border }`) still exists for cases that need
> a barely-there tint over an arbitrary surface — but for stage/segment backgrounds
> use the D-light **tokens** above; they're the design-system "lighter shade".

For **SVG / canvas status nodes** that can't take a Tailwind `bg-*` class (gauge fills,
radar vertices, ring segments), paint with `perfColor(score)` via `fill`/`stroke` or an
inline `style`. **Journey Map bubble specifically:** it is a plain **white** circle
(`bg-card`) with a colored **ring** in the full-strength `perfColor(score)` — the status
shade lives in the _column background_ behind it (see the Journey Map spec above), so the
bubble interior stays white; do **not** also tint the bubble.

Use this scale on: gauge fill colors, radar vertex dots, segment bar colors,
distribution bars, branch scores, and any metric that has a target/benchmark.

### Data Visualization Quality Principles

Every chart and visualization in the app must feel **premium and data-rich** — never flat,
generic, or library-default. These principles apply to ALL future charts:

1. **Depth over flatness** — use gradients, subtle shadows (`shadow-sm` on chart cards),
   multi-ring designs, and layered elements. A chart should feel like it has physical depth.
2. **Performance coloring everywhere** — any value that represents performance uses the
   `perfColor` scale. Branch NPS = green/amber/red based on value. Gauge fill = colored by
   health. Radar dots = per-vertex performance colors.
3. **Markers and indicators** — gauges need needle dots and target tick marks. Trend lines
   need data point dots (not just the line). Annotations need colored dots with hover tooltips.
4. **3-zone inner rings** — any gauge or circular indicator should show the "zone" context:
   red zone (bad), amber zone (average), green zone (great) — so users instantly see where
   their value falls in the performance spectrum.
5. **Segment tiles** — KPI cards always show a 3-tile breakdown grid below the gauge (e.g.,
   Promoters 51% | Passives 40% | Detractors 9%). Each tile gets a tinted background matching
   its category color.
6. **Distribution bars** — always horizontal with rounded ends (`rounded-full`), colored by
   performance level, with percentage labels inside or beside the bar. Never plain grey.
7. **Segment breakdowns** — show mini colored progress bars proportional to max value, with
   score values at the end. Color each bar by `perfColor(value)`.
8. **Responsive SVG** — always use `viewBox` and `preserveAspectRatio` for SVG charts. Never
   hardcode pixel dimensions. Use `className="w-full"` for container sizing.

---

## Dark Mode

### Architecture

Dark mode is toggled by adding/removing the `dark` class on `<html>`.
Use `prefers-color-scheme` as default, allow user override, persist choice in `localStorage`.

```tsx
// Theme toggle implementation pattern
function useTheme() {
  const [theme, setTheme] = useState(() => {
    const stored = localStorage.getItem("theme");
    if (stored) return stored;
    return window.matchMedia("(prefers-color-scheme: dark)").matches
      ? "dark"
      : "light";
  });

  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark");
    localStorage.setItem("theme", theme);
  }, [theme]);

  return {
    theme,
    setTheme,
    toggle: () => setTheme((t) => (t === "dark" ? "light" : "dark")),
  };
}
```

### Dark Mode Design Rules

**Surface hierarchy (light-to-dark elevation):**

A **stepped ladder** — each surface is a clear perceptual-lightness step above the last
(OKLCH L in brackets), so regions read as distinct instead of one flat black mass:

```
Sidebar (deepest):     #070B10  [L .147]  — nav rail; a WIDE ~.07 step below the canvas
Sidebar divider:       #444753  [L .400]  — --sidebar-border; a bright seam sealing nav↔canvas
Background (canvas):   #171923  [L .216]  — page base (navy-tinted, a wide step above the sidebar)
Card / Popover:        #22252F  [L .266]  — cards, panels, table rows, dialogs, sheets, select menus
Accent (highlight):    #292C37  [L .295]  — select-item focus, feature areas (LIGHTER than card → reads raised)
Muted (RAISED):        #30333E  [L .323]  — hover states, tracks, status pills, skeletons, table-header band
Border (divider):      #383C54  [L .363]  — card edges, table dividers, separators
Input (field edge):    #434864  [L .409]  — input / select / textarea outlines (a step lighter than border)
```

> **Two hard lessons baked into this ladder:**
>
> 1. **Sidebar must sit a WIDE step BELOW the canvas.** They were once `#070B10` vs `#0D0F14`
>    (0.02 L gap) → then `#14161F` (0.055) — both still let the near-black sidebar and the dark
>    canvas blur into one flat sheet ("sidebar and background look the same"). The canvas is now
>    `#171923` — a **~0.069 L step** above the sidebar — _and_ the divider (`--sidebar-border`)
>    is a **brightened** solid navy `#444753` (was `#313650`) so the seam clearly reads as an
>    edge. The sidebar stays `#070B10`; going deeper is a trap (near-black can't hold tint, so a
>    "deeper" sidebar reads muddier) — lift the canvas away from it instead. Never close this
>    gap back up, and never dim the divider.
> 2. **Muted is the RAISED surface, not a dark one.** It MUST sit clearly _above_ Card so
>    `hover:bg-muted/50`, `bg-muted/40` tracks, and `bg-muted` pills are visible. An earlier bug
>    set it to `#0E1018` (= the page background) and every hover/track/badge vanished.
>
> Order (dark→light): **Sidebar < Background < Card < Accent < Muted < Border < Input.**
> **This ladder lives in TWO files that MUST move together:** the default (navy) is pinned in
> `index.css`'s `.dark` block and is used by every tenant _not_ in `tenant-themes.json`; custom
> tenants are generated by `deriveThemeVars` (the `DARK` anchors + `DARK_CHROMA ≈ 0.28` for big
> surfaces, `SIDEBAR_DARK_CHROMA ≈ 0.9` for the sidebar's brand tint). Editing only one file
> fixes only _some_ tenants — the default and the derived tenants then diverge. Two
> seed-independent rules for the derivation: (a) never derive a sidebar **deeper** than L .147
> (near-black collapses every hue to muddy neutral); (b) keep big surfaces **low-chroma** so a
> vivid seed (green, orange…) stays a clean near-neutral dark, never a murky saturated wash.

**Key principles:**

1. **Navy-tinted, not grey.** Dark mode uses deep navy-blues, never neutral greys. This keeps brand identity present even in dark mode.
2. **Never use pure black `#000000`.** It causes OLED smearing and feels unnatural. Our deepest color is `#070B10` (the sidebar); the canvas is `#171923`. Use Navy or Dark tokens instead.
3. **Lighten, don't invert.** Primary cyan goes from `#0D8BBC` (light) to `#59CCEA` (dark) — brighter, not simply inverted.
4. **Borders & field edges must be a real lightness STEP above their surface — NOT low-alpha
   navy.** A translucent navy border (`rgba(30,34,53,0.5)`) resolves to _almost exactly_ the
   card/background it sits on (a 2–8/255 delta), so table dividers, card edges, and input
   outlines become invisible — the exact bug that made "fields I can't even see." Use the solid
   `--border` (`#313650`) / `--input` (`#3E4360`) tokens (i.e. `border-border` / `border-input`),
   which are deliberately lighter than every surface. Never hand-roll a dark border from
   `border-nb-navy/…` or a raw `rgba(30,34,53,·)` — reach for the tokens. The same values are
   generated for tenant themes by `deriveThemeVars` (`withLightness(neutral, 0.31/0.37)`), so a
   subdomain's dark mode inherits legible chrome automatically; never lower those anchors back
   toward the surface lightness.
5. **Test contrast separately.** Dark mode contrast must be verified independently — don't assume light mode values work. Minimum 4.5:1 for body text, 3:1 for large text.
6. **Desaturate status colors slightly.** Bright green/red on dark backgrounds is jarring. Use D1–D5 light tokens in dark mode.

**Conditional dark mode classes:**

```tsx
// Apply different styles per theme
<Card className="bg-card border border-border shadow-sm dark:shadow-none dark:border-border">

// KPI status badges that work in both themes (using D2 semantic tokens)
<Badge className="bg-[#C8F5DB] text-[#156632]
                  dark:bg-[#156632]/20 dark:text-[#C8F5DB]">
  On Track
</Badge>
```

---

## Font

- **Primary**: "Poppins" — `font-sans` (body text, paragraphs, UI labels)
- **Headings**: "Sora" — `font-heading` (all English headings and display text)
- **Arabic fallback**: "IBM Plex Sans Arabic" — in the `font-sans` chain for Arabic rendering
- **Monospace** (code, KPI values): "Fira Code" — `font-mono`
- **Fallback**: IBM Plex Sans Arabic, sans-serif
- **Never use Inter, Roboto, or Arial** — they are off-brand.
- Always use `font-sans` / `font-heading` / `font-mono`.

### Typography Scale (from designer spec — Poppins reference sizes)

| Role         | Font         | Size    | Weight | Notes                                                |
| ------------ | ------------ | ------- | ------ | ---------------------------------------------------- |
| Display/Hero | Poppins/Sora | 40–64px | 800    | Letter-spacing -0.02em. Cover & hero only            |
| Heading 1    | Poppins/Sora | 26–32px | 700    | Line-height 1.2. Section titles                      |
| Heading 2    | Poppins/Sora | 18–22px | 700    | Line-height 1.3. Card titles                         |
| Body         | Poppins      | 14–16px | 400    | Line-height 1.7. Paragraphs and descriptions         |
| UI Label     | Poppins      | 10–12px | 500    | Uppercase. Letter-spacing 0.12em. Tags, badges       |
| Data / KPI   | Poppins      | 12–14px | 400    | Tabular numerals. KPI values, hex codes, data tables |

**Tailwind mapping:**

| Element                 | Classes                                                               | Notes                                       |
| ----------------------- | --------------------------------------------------------------------- | ------------------------------------------- |
| Page title              | `text-2xl font-heading font-bold`                                     | One per page                                |
| Section title           | `text-lg font-bold`                                                   |                                             |
| Card title              | `text-base font-bold`                                                 |                                             |
| Body text               | `text-sm` or `text-base leading-relaxed`                              | Use `leading-relaxed` for Arabic paragraphs |
| Caption/meta            | `text-sm text-muted-foreground`                                       |                                             |
| Field hint / help text  | `text-xs leading-relaxed text-muted-foreground`                       | 12px — see "Subordinate text" below         |
| Field text (dense form) | `text-xs md:text-xs` on the `Input` / `Textarea`                      | 12px value **and** placeholder — see below  |
| Small label / UI Label  | `text-xs font-medium uppercase tracking-widest text-muted-foreground` | Avoid for Arabic body text                  |
| Stat number             | `text-3xl font-heading font-bold tabular-nums`                        | Use `tabular-nums` for aligned numbers      |
| Data table cell         | `text-sm tabular-nums`                                                | Numeric columns get `tabular-nums`          |

### Subordinate text is 12px — hints, placeholders, and explanatory card lines

Text that **explains a control rather than being content** drops to `text-xs` (12px) with
`leading-relaxed`. At `text-sm` it competes with the field's own `<Label>` and with card titles,
which made forms read as a wall of same-size grey text (caught on SCR-04, the service-channel
form: a four-line 14px hint under the channel-ID field out-shouted every label on the card).
Three specific cases:

- **Field hint / help text** under an input → `text-xs leading-relaxed text-muted-foreground`.
  The `leading-relaxed` is not optional — multi-line hints at 12px need the line-height back.
- **Field text on a data-dense form** → `text-xs md:text-xs` on the `Input` / `Textarea`, which
  takes the typed value **and** its placeholder to 12px. The `md:` variant is required: the
  component default is `text-base md:text-sm`, so `text-xs` alone is overridden back to 14px at
  `md` and up. Use this on **dense, multi-field module forms** (SCR-04 and the rest of Integration
  Hub) where a 14px value column made the card read as oversized. Keep 14px for **short, isolated
  forms** (login, a single-field dialog) — there is no density pressure there and a bigger value is
  easier to proofread.
  **Trade-off, stated so it isn't rediscovered:** 12px is the small end for text the user is
  actively editing and proofreading — identifier-shaped values (`E2E-BACK-210731708`) are exactly
  where a mistyped character hides. That is the accepted cost of the denser form; it is **not**
  licence to shrink editable text below 12px, ever.
- **Explanatory `CardDescription`** that instructs about the content below it →
  `className="text-xs leading-relaxed"`. A card _title_ stays `text-base font-bold`.

**Validation errors NEVER shrink** — keep `text-sm text-destructive`. An error occupies the same
slot as the hint it replaces, and must not be quieter than the hint.

**Emphasise control names inline** when an explanatory line names the exact controls it's telling
you to use: the gating control gets `font-semibold text-foreground`, a secondary one
`font-medium text-foreground`. Render it with `<Trans>` and named component tags so the emphasis
travels with the translation instead of being spliced in per-locale:

```tsx
// en.json → "Turn on <s>Supported</s> for every field …; mark <r>Required</r> to make it mandatory."
<Trans
  i18nKey="…contractDescription"
  components={{
    s: <span className="font-semibold text-foreground" />,
    r: <span className="font-medium text-foreground" />,
  }}
/>
```

**Arabic exception to the 12px rule.** The "never `text-xs` for Arabic body text" rule still
holds for _paragraphs_. These three cases are captions, not body text, so 12px is allowed — but
**check any multi-line Arabic hint at 12px before shipping it**; if it reads cramped, raise that
one instance to `text-sm` rather than lowering the whole page.

**Per-instance, not per-component.** Apply these as `className` on the usage site. Do **not** edit
`input.tsx`, `textarea.tsx`, or `card.tsx` to change their default sizes — those defaults are
shared by every form and card in the app, and a global shift is a separate, deliberate decision.
If the same override starts appearing on most screens, that is the signal to change the default
once, in the component, and delete the overrides.

---

## Component Rules

### Always use shadcn components

Import from `@/components/ui/*`. Never use raw HTML when a shadcn component exists:

```tsx
// Correct
import { Button } from "@/components/ui/button"
<Button>إنشاء</Button>

// Wrong
<button className="...">إنشاء</button>
```

### Icons

Use **Lucide React** only. Never use emojis, FontAwesome, or other icon sets:

```tsx
import { Plus, Search, Download, Trash2 } from "lucide-react";
<Plus className="size-4" />;
```

- Icon-only buttons MUST have `aria-label`: `<Button size="icon" aria-label="حذف"><Trash2 /></Button>`
- Standard size: `size-4` (16px) inline, `size-5` (20px) in nav
- **Create/add buttons lead with the icon, consistently.** A `+`/action icon on a labelled
  button goes **before** the label (`<Plus className="size-4" />{label}`), matching New
  Journey · Add Stage · Add KPI · Add Parameter — never trail it on some buttons and lead it
  on others (that mix looked inconsistent on the scope editor's "Add Parameter" vs "Create
  Rule"). Let the button variant's own `gap` space the icon from the label — **don't** hand-add
  `ms-1`/`ms-2` to the icon (a leading-icon `ms-*` pushes the wrong edge and double-spaces the
  `gap`). The Loader2 spinner follows the same rule: place it where the icon would sit, no manual margin.

### Button Variants

| Action           | Component                                                                                                               | Example                      |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------- | ---------------------------- |
| Primary action   | `<Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground">` (hover shadow is built into the variant) | اشتراك, إنشاء, حفظ, نشر      |
| Secondary action | `<Button variant="secondary">` — soft cyan, **derived from the primary**                                                | تفاصيل, تحليلات, إضافة مرحلة |
| Destructive      | `<Button variant="destructive">`                                                                                        | حذف, إزالة                   |
| Neutral / Cancel | `<Button variant="outline">` — **borderless soft-grey fill (`bg-muted`) + muted text**                                  | Cancel, Back, Skip, dismiss  |
| Ghost/subtle     | `<Button variant="ghost">`                                                                                              | Navigation, toolbar          |
| Loading state    | `<Button disabled><Loader2 className="size-4 animate-spin ms-2" />جاري الحفظ...</Button>`                               | During async ops             |

**The `outline` variant is the one canonical Cancel/dismiss look — a _borderless_ soft-grey
fill, and every Cancel uses it.** Resting style: `bg-muted` (#EEF1F7) fill + `text-muted-foreground`,
**no visible border** (the base keeps `border border-transparent`), hover `bg-muted/80`. Two
things this replaced, both real bugs: (1) the old `bg-background` (#F7F9FC) fill was ~invisible
on white cards/sheets (`bg-card`/`bg-popover` are pure white) so cancels looked like empty
hairline rectangles; (2) a visible `border-border` made it read as a hard "outline" rather than
the soft filled pill the app's journey dialogs already used. **Every Cancel/Back/Skip/dismiss
button is now `variant="outline"`** — never `ghost` (invisible), never `secondary` (soft-cyan is
for a real secondary _action_, not a dismiss), never a hand-rolled `bg-muted … text-muted-foreground`
inline (that's exactly this variant — use it).

**Action-button hierarchy (the one-blue rule).** A page header may show several
action buttons, but **exactly one is the filled blue primary** (`variant="default"`,
`bg-primary`). **Every other action uses the secondary style — regardless of how many
there are.** With three actions (e.g. "Analytics" + "Add Stage" + "Publish") the
2nd _and_ 3rd are both secondary; with four, the 2nd/3rd/4th are all secondary. Only
one stays the filled primary. Per-row actions like "Add Touchpoint" follow the same
rule. Secondary actions use `variant="secondary"`: a **lighter shade of the same
primary cyan** (`bg-nb-cyan-100 / text-nb-cyan-800`; dark `bg-nb-cyan-900/40 /
text-nb-cyan-200`). It stays in the brand family but reads visibly quieter than the
solid primary, so the one primary remains _the_ action. Rules:

- Never promote a second button to the **filled** blue — only one filled primary per page.
- Never leave a secondary action as a plain neutral/`outline` button — it looks empty
  next to the primary; the secondary must carry the soft-cyan fill so the pair reads
  as a deliberate primary + secondary set.
- Never use the **mint** token for action buttons — mint clashes with the D2 "Good"
  semantic state. The button `secondary` variant has been redefined to the cyan-derived
  style specifically for action buttons (it no longer renders mint).
- **Compact secondary (inside accordions / tables).** A secondary action that lives in
  an accordion row or a table uses `variant="secondary" size="compact"` — the _same_
  soft-cyan style at **35px** tall (`size="compact"`, still `rounded-md`/12px) instead
  of the full 40px, so it doesn't crowd the row. Example: the per-stage "Add Touchpoint"
  button. Don't hand-set `h-7`/`h-8` on these — use `size="compact"`.

**Action-button shadows are hover-only.** Both the blue primary and the soft-cyan
secondary carry **no shadow at rest** — on hover, _both_ lift with a soft colored
shadow (`hover:shadow-md hover:shadow-primary/30` for primary, `…/20` for secondary).
This is baked into the `default` and `secondary` button variants, so **never add a
resting shadow** to an action button (no `shadow-sm` on the button itself). Outline/
ghost actions stay flat at rest and on hover.

**Interactive control sizing — one 40px family.** Default-size **buttons, inputs,
and selects all stand 40px tall (`h-10`) with a 12px radius (`rounded-md`)** so they
line up perfectly when placed side by side (e.g. a date-range `Select` next to a
"New Survey" button in a page toolbar). This height/radius lives in the **component
defaults** (`button.tsx`, `input.tsx`, `select.tsx`) — CLAUDE.md only records the
rule; changing the doc does not restyle anything, you must set it in the component.
Action buttons additionally take generous horizontal padding (`px-4`). Compact contexts still use `size="sm"`/`size="icon"`;
icon-only buttons keep their square sizes. Never hand-tune an action button's height
or radius inline — rely on the default size so the whole app stays consistent.

### Cards

```tsx
<Card>
  <CardHeader>
    <CardTitle>عنوان البطاقة</CardTitle>
    <CardDescription>وصف مختصر</CardDescription>
  </CardHeader>
  <CardContent>{/* content */}</CardContent>
  <CardFooter>{/* actions */}</CardFooter>
</Card>
```

- Don't nest cards inside cards — flatten the hierarchy
- Use `shadow-sm` in light mode, `dark:shadow-none` in dark mode (borders provide separation in dark)
- The base `<Card>` already ships `rounded-lg` (16px) + `ring-1` + `shadow-sm dark:shadow-none` for elevation — **do not** re-add these or push a card past the 16px radius ceiling. Why: on the navy-tinted off-white background, white cards must lift via a tight radius + soft shadow, not a large curve.
- **Minimize curves:** cards/popups cap at `rounded-lg` (16px) and inner containers/tiles (stat tiles, attribute chips, banners) at `rounded-md` (12px). Tighter radii read as more precise and data-dense — avoid `rounded-lg` on small nested tiles.
- **Don't double the vertical padding.** The base `<Card>` already supplies `py-4` (16px top _and_ bottom). Do **not** add **any** vertical padding to `CardContent` — not `pt-6`, and equally not `py-4`, `pt-4 pb-4`, or an asymmetric `pt-3 pb-2`. Every one of these stacks on the card's own `py-4` (making a doubled/lopsided card); a `py-4` on `CardContent` is redundant, not corrective. Use a bare `<CardContent>` (horizontal-only classes are fine, e.g. `<CardContent className="flex gap-3">`) so vertical padding stays balanced. Applies to every card — header cards, filter cards, chart cards, single-content cards alike.
- **Header cards align center.** When a card's content is a header row (title/badges on one side, action buttons on the other), the flex row uses `lg:items-center` (not `lg:items-start`) so the action buttons sit vertically centered against the title block.

### Badges / Tags

- **Outline tags have a white fill.** The Badge `outline` variant ships `bg-card`
  (white in light, dark surface in dark) — never leave a tag transparent, which makes
  it disappear into tinted backgrounds (e.g. the D-light stage columns). A plain
  `<Badge variant="outline">` is therefore a white pill with a border.
- **Colored tags keep their color.** Tags that set their own background via `className`
  (status pills like `bg-d2-light`, `bg-nb-cyan-100`, …) override the white fill — the
  Badge merges classes with `tailwind-merge`, so the explicit `bg-*` wins. Only
  _backgroundless_ (outline) tags pick up the white fill.

### Forms

- Use shadcn `Input`, `Select`, `Checkbox`, `Textarea`, `Label`
- **Popup surfaces (Select / DropdownMenu) — baked-in component defaults, don't re-fight
  them.** These are set in `select.tsx` / `dropdown-menu.tsx`; new code inherits them:
  - **Menu content sizes to its content (`w-auto`), never to the trigger.** A `DropdownMenu`
    opened from a `size="icon"` (28px) button must NOT inherit `w-(--anchor-width)` — that
    clamps the menu to ~trigger width and wraps items like "Open Builder" onto two lines.
    Menus use `w-auto min-w-40`; items carry `whitespace-nowrap`. (A `Select` popup _does_
    match the trigger width via `w-(--anchor-width)` — that's correct, the two differ.)
  - **Select popup opens below the trigger** (`align="start"`, `alignItemWithTrigger=false`)
    — a clean dropdown, not the native-style overlay where the selected item sits _on top of_
    the trigger (that reads as a misplaced popup).
  - **Popup items breathe:** `py-2` per item + `p-1` inset on the popup. `py-1` items look
    cramped.
- **Stack a label + `Select` with `flex flex-col gap-1.5`, NOT `space-y-1.5`.** base-ui's
  `Select` renders a **trailing hidden node** after the trigger, so the trigger is no longer
  its parent's last child — and `space-y-*` (which margins every non-last child) lands a stray
  `margin-bottom: 6px` **on the select trigger**. In a vertical form that's just 6px of extra
  gap (mostly harmless), but in a filter row aligned with `sm:items-end` it shoves the select
  **6px above** the search input / other controls beside it — a visible misalignment (caught
  via DevTools: trigger box `192×40, margin 0 0 6px`). `gap` spaces flex children without
  per-child margins, so the trailing hidden node can't latch a margin. Rule: **any label+Select
  column that sits in a horizontal `items-end`/`items-center` row MUST use `flex flex-col
gap-1.5`**; plain inputs are fine either way (no trailing node), but prefer `gap` for
  uniformity.
- **Fields are white, not transparent.** Inputs, selects, and textareas use `bg-card`
  (white in light mode; the elevated dark surface in dark mode) — never `bg-transparent`,
  which would let the off-white page background bleed through and make fields look grey.
  The `border-input` edge provides the boundary.
- **Always use visible labels** — never placeholder-only inputs. **The one exception is an
  inline tag/chip adder** (a small "type a value + Enter to add a pill" field, e.g. the
  data-scope value/parameter editors): a floating `<Label>` there is awkward, so give the
  `<Input>` an `aria-label` (mirroring the placeholder) instead. A placeholder alone is still
  a defect — the accessible name is required; `aria-label` supplies it.
- Labels go above inputs, use `<Label htmlFor="...">` for accessibility
- Group fields: `<div className="space-y-4">`
- Required fields: add `<span className="text-destructive">*</span>` next to label
- Validation errors below the field: `<p className="text-sm text-destructive mt-1" role="alert">رسالة الخطأ</p>`
- Error messages must state **cause + fix**, not just "خطأ في الإدخال"
- On submit error, auto-focus the first invalid field
- Use semantic input types: `type="email"`, `type="tel"`, `type="number"` for correct mobile keyboards

### Tabs — always `TabsListSegmented` (one control, app-wide)

**Every tabbed page uses `<TabsListSegmented>` from `@/components/ui/tabs`. Never hand-roll a
tab group, and never use a bare `<TabsList>`.** The segmented control — bordered `bg-muted`
track with a sliding white `TabsIndicator` pill — is the app standard, set by the Surveys
library (`/surveys`) and now shared.

```tsx
import {
  Tabs,
  TabsContent,
  TabsCountPill,
  TabsListSegmented,
  TabsTrigger,
} from "@/components/ui/tabs";

<Tabs value={tab} onValueChange={setTab} className="gap-5">
  <TabsListSegmented>
    <TabsTrigger value="all" data-testid="tab-all">
      {t("…tabAll")}
      <TabsCountPill count={counts.all} />
    </TabsTrigger>
    <TabsTrigger value="custom" data-testid="tab-custom">
      {t("…tabCustom")}
      <TabsCountPill count={counts.custom} />
    </TabsTrigger>
  </TabsListSegmented>
  <TabsContent value="all">…</TabsContent>
</Tabs>;
```

Why the wrapper exists rather than a documented recipe: the styling needs **four
modifier-prefixed overrides** plus a `TabsIndicator` as the list's first child. The base
`TabsList` pins `group-data-horizontal/tabs:h-8` and `data-[variant=line]:rounded-none`, which
outrank plain `h-auto` / `rounded-lg` utilities — so a copy-pasted version silently loses its
trigger padding, and **omitting `TabsIndicator` leaves the control with no visible selection at
all** (that is what "broken-looking tabs" was: a bare `<TabsList>` with no indicator and no
segmented track, on SCR-05 Parameters). `TabsListSegmented` bundles all of it; `variant="segmented"`
on `TabsList` carries the track styling and the triggers adapt via
`group-data-[variant=segmented]/tabs-list:` selectors, so **no page passes tab `className` overrides**.

Rules:

- **Counts go in `<TabsCountPill count={n} />`, never interpolated into the label.** The pill
  tints to the brand primary on the active tab, and renders **nothing** for `null` so a
  still-loading tab shows its label instead of a flash of `0`. Keep the i18n label a plain noun
  ("All", "Built-in") — this also avoids i18next's `{{count}}`, which is a **plural selector**,
  not a formatted value (see "Why `{{n}}`, not `{{count}}`").
- **Tab counts are global** — never narrowed by the filters below the tab strip. A count that
  changes when you type in the search box reads as a bug.
- `data-testid="tab-<value>"` on every trigger; E2E selects tabs by testid, not translated text.
- Use `variant="line"` (underline, no track) **only** for in-page section navigation inside an
  editor shell — not for filtering a list. When in doubt, segmented.
- Put panel content in `<TabsContent>`; if a page has one shared table under all tabs (the
  Parameters pattern), the tab strip may stand alone with no `TabsContent` at all.

### Tables (Data-Dense)

Use shadcn `Table` components. For data-heavy tables:

- **Sticky header: `<TableHeader className="sticky top-0 z-10">` — NO `bg-background`.**
  `TableHeader` now ships its own opaque `bg-muted` band by default (in `table.tsx`), so it
  reads as a subtle raised header in both themes AND stays opaque while pinned. Never re-add
  `bg-background`: a table is almost always wrapped in a card (`bg-card`), and in dark mode
  the page background is _darker_ than the card, so a `bg-background` header renders as a
  jarring dark band that doesn't match the card ("weird header" bug). Just add
  `sticky top-0 z-10` for stickiness; the band comes from the component.
- **Wrap a table in a bordered card with `overflow-hidden`.** The card container is
  `overflow-hidden rounded-lg border border-border bg-card shadow-sm dark:shadow-none`. The
  `overflow-hidden` is **required**, not optional: `<Table>` ships its own
  `overflow-x-auto` inner div, and the sticky header paints an opaque `bg-background` — both
  push **square** corners past the card's rounded border, so the border reads as "cut" at
  the top corners and any horizontal overflow bleeds past the right edge. `overflow-hidden`
  clips the table to the radius. (The box-shadow is outside the border box, so it isn't
  clipped.) Every list-table card in the app now carries it — match that.
- Row hover: add `hover:bg-muted/50` to `<TableRow>`
- Numeric columns: `text-end tabular-nums` — but this is for **pure magnitudes** where
  right-aligning digits aids comparison (counts, amounts, percentages). **Dates/timestamps and
  text-ish columns stay `text-start` like every other column** — a lone `text-end` date column
  floats to the far edge with a gap and reads as misaligned (audit-log "Time" bug). Keep the
  numerals LTR with an inner `<span dir="ltr">`, not by flipping the cell to `text-end`.
- Empty state: never show a blank table — show helpful message with action
- Sortable columns: add `aria-sort` attribute (`ascending` / `descending` / `none`)
- Consider virtualizing lists with 50+ rows

### Row actions in a table — icon-only, muted, unlabelled column

**Every per-row action in a list table is an icon-only ghost button, and its column has no
visible header.** This is the app-wide pattern (SCR-03 Service channels, SCR-05 Parameters):

```tsx
// header — width reserved, label kept for screen readers only
<TableHead className="w-16 text-center">
  <span className="sr-only">{t("…colActions")}</span>
</TableHead>

// cell
<TableCell className="w-16 text-center">
  <Button
    variant="ghost"
    size="icon"
    className="text-muted-foreground hover:text-foreground"
    aria-label={label}
    title={label}
    data-testid={canManage ? `edit-${row.key}` : `view-${row.key}`}
    onClick={…}
  >
    {canManage ? <Pencil className="size-4" /> : <Eye className="size-4" />}
  </Button>
</TableCell>
```

Rules, each from a real defect:

- **Icon-only, `variant="ghost"` — NOT the filled `secondary` compact button.** A soft-cyan
  filled button repeated down 70 rows becomes a solid cyan stripe and competes with the page's
  one primary CTA. The in-row `secondary size="compact"` rule still applies to **accordion rows
  and single-action toolbars** ("Add Touchpoint"), not to a per-row edit in a long list.
- **`text-muted-foreground hover:text-foreground`.** At default `text-foreground` the icon is
  near-black navy and pulls focus away from the row's own content while scanning.
- **`w-16 text-center` on BOTH the header and the cell.** A `w-px` column collapses to the
  icon's own width and pins it against the card edge with no breathing room.
- **The visible header goes, the accessible name stays** (`sr-only`). Never drop the name
  entirely, and never leave a bare `<TableHead />`.
- **Read-only personas get `<Eye>` + a `view-` testid**, not a disabled pencil — the action
  differs, so the affordance should too. E2E selects on `edit-*` / `view-*`, so the prefix
  switch is what proves the permission split.

### Read-only yes/no cells — `Check` / `Minus`, in brand cyan

For boolean **capability** columns (Filterable, Reporting visibility, …), render Lucide icons —
**never the literal `"✓"` / `"—"` characters.** Text glyphs resolve through whatever font falls
back, so they arrive hairline-thin, vertically off-centre, and inconsistent between the Latin and
Arabic stacks (exactly how SCR-05's flag columns looked before). Use `FlagGlyph` in
`ParameterDrawer.tsx` as the reference:

```tsx
{
  on ? (
    <Check
      className="size-4 text-nb-cyan-700 dark:text-nb-cyan-300"
      strokeWidth={3}
    />
  ) : (
    <Minus className="size-4 text-muted-foreground/40" strokeWidth={2.5} />
  );
}
```

- **The tick is brand cyan, NOT semantic green — this is a Two-Palette rule, not a preference.**
  A capability flag ("filterable: yes") is not a health state. Spending D2 green on "yes"
  weakens it everywhere it means "good". The ratified prototype shows green ticks here; we
  deliberately deviate. If that is ever overridden, it needs a Two-Palette amendment, not a
  one-off className.
- **Shape carries the meaning too** (tick vs dash), so the column survives colour-blindness and
  greyscale printing — required by "colour is never the only indicator".
- Wrap in `role="img"` with an `aria-label` that includes the **value** (`"Filterable: yes"`),
  not just the column name.

### Dialogs & Sheets

- Confirmations and quick forms: `<Dialog>` — centered, max-w-md
- Side panels and filters: `<Sheet>` — slides from `start` side (right in RTL)
- **Dialog width: prefix the cap with `sm:` (`sm:max-w-md`), never a bare `max-w-md`.** The
  `DialogContent` base keeps a mobile side-gutter via `max-w-[calc(100%-2rem)]`; a bare
  `max-w-md/lg` overrides it (tailwind-merge drops the calc), so at ~375px the dialog goes
  full-bleed edge-to-edge. `sm:max-w-md` lets the 1rem gutter survive below 640px and the cap
  apply from `sm` up.
- Confirm before destructive actions (delete, discard changes)
- Scrim opacity: 40-60% for clear foreground separation
- Always provide close/dismiss affordance
- **Footer Cancel gets `variant="outline"`, never `variant="ghost"`.** A bare ghost
  Cancel stacked under the filled primary in a Sheet/Dialog footer reads as floating text,
  not a button — it has no rest-state background or border. Use `variant="outline"` so the
  Cancel carries a visible border/surface and pairs with the primary as a real button. (The
  filled action stays the one primary; Cancel is a neutral bordered dismiss, not a soft-cyan
  `secondary` — reserve `secondary` for an actual secondary _action_, not a dismiss.)
- **Tall content rule — `DialogContent` does NOT self-constrain; you must add the height
  cap + body scroll.** The base `<DialogContent>` has **no** `max-h`/`overflow` (it's a
  `grid` that grows with its content), so a form dialog that exceeds the viewport clips its
  title off the top and pushes the footer below the fold (real bug on the touchpoint dialog).
  Any dialog whose content can get tall (multi-field forms, multi-section settings, long
  lists) MUST pin the header/footer and scroll only the body — the same shape as the sheet
  pattern below:
  ```tsx
  <DialogContent className="flex max-h-[90vh] max-w-lg flex-col">
    <DialogHeader className="shrink-0"> … </DialogHeader>
    <form className="flex min-h-0 flex-1 flex-col gap-4">
      <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-1">
        {" "}
        …fields…{" "}
      </div>
      <DialogFooter className="shrink-0 gap-2 sm:gap-2"> … </DialogFooter>
    </form>
  </DialogContent>
  ```
  `flex max-h-[90vh] flex-col` on the content overrides the default `grid` and caps height;
  the body `<div>` carries `min-h-0 flex-1 overflow-y-auto` (the `px-1` keeps focus rings off
  the scroll edge); header + footer get `shrink-0`. Short confirmation dialogs need none of
  this (they never hit `max-h-[90vh]`).
- **Sheet scroll pattern — pin header/footer, scroll only the body.** `SheetContent`
  is already `h-full flex flex-col` for a side sheet. So structure the children as
  `SheetHeader` (fixed) → **body `<div className="flex-1 min-h-0 overflow-y-auto …">`** →
  `SheetFooter` (fixed). Two non-obvious rules, each a real bug we hit on the touchpoint
  sheet: (1) the scroll body MUST include **`min-h-0`** — a flex child defaults to
  `min-height:auto` and won't shrink below its content, so without it the body never scrolls
  and instead pushes the footer off-screen; (2) do **NOT** put `overflow-y-auto` /
  `max-h-screen` on `SheetContent` itself — that scrolls the whole sheet (header + footer
  included) as one block and unpins the footer. The footer stays reachable only when the
  body is the sole scroll container.

### Overriding a component default? Match its variant prefix (the silent-no-op trap)

**`cn()` / tailwind-merge only de-duplicates classes with the SAME variant chain.** A plain
utility does **not** override a modifier-prefixed one — both survive, and CSS specificity hands
the win to the prefixed class. The override looks correct in the source and does nothing in the
browser, with no warning. This has bitten us three times:

| You write                             | The component already has                                            | What actually happens        |
| ------------------------------------- | -------------------------------------------------------------------- | ---------------------------- |
| `sm:max-w-xl` on `SheetContent`       | `data-[side=right]:sm:max-w-sm`                                      | Sheet stays **384px**        |
| `text-xs` on `Input`                  | `text-base md:text-sm`                                               | Reverts to **14px** at `md`+ |
| `h-auto` / `rounded-lg` on `TabsList` | `group-data-horizontal/tabs:h-8`, `data-[variant=line]:rounded-none` | Padding squashed             |

**Rule: before overriding a `components/ui/*` default, grep the component for that utility and
copy its full prefix.** So `data-[side=right]:sm:max-w-lg` (both sides — sheets flip in RTL),
`text-xs md:text-xs`, `group-data-horizontal/tabs:h-auto`.

**Diagnosing it:** if a className appears to do nothing, inspect the element and look for a
prefixed sibling of the same property in the computed styles — do not assume the value is wrong
and start guessing larger numbers. And when the same override keeps recurring across screens,
that is the signal to add a **variant to the component** (as `TabsList`'s `segmented` did) rather
than to keep re-prefixing at every call site.

### Wizards — one shared stepper, one step layout

**Every multi-step flow uses `WizardStepper` from `@/components/ui/wizard-stepper`. Never
hand-roll a row of numbered circles.** The look was set by the survey editor
(`/surveys` → New survey → Survey builder) and is now shared; the Integration Hub's
provisioning wizard (SCR-02) renders the same bar.

```tsx
import { WizardStepper } from "@/components/ui/wizard-stepper";

<WizardStepper
  ariaLabel={t("…stepsAria")}
  steps={STEPS.map((s) => ({
    label: t(`…step${s}Title`),
    state: step === s ? "active" : step > s ? "done" : "todo",
    testId: `step-indicator-${s}`,
    // Omit onClick to make a step unreachable — see the gating note below.
    onClick: canJumpTo(s) ? () => setStep(s) : undefined,
  }))}
/>;
```

- **A bordered bar of flat segments** — reached steps carry the soft **Mint→Cyan** wash, `done` a
  mint check circle, `active` a primary number circle, `todo` a muted one. The mint here is
  _decorative brand marking progress_, _not_ the D2 "Good" state — that is the one sanctioned
  mint-adjacent use inside product chrome (Two-Palette Rule).
- **The stepper never invents gating.** `state` is the caller's to compute, and a step is
  navigable only if the caller passes `onClick`. A validation-gated wizard (SCR-02) passes none;
  a freely-navigable one (the survey editor) passes them all.
- **Below `sm` only the active step keeps its label**, so the bar can never wrap to two lines.

**Step body layout — the shape every wizard step follows:**

1. **Page header** — `h1` + a `max-w-2xl` description, with **Cancel** (`variant="outline"`) as
   the only header action. The step's own primary action lives in the footer, not up here.
2. **No card header on the first form block.** The page title already names the flow; a
   "Basics" `CardTitle` restating it just pushes the first field down. Go straight to
   `<Card><CardContent>` with the fields.
3. **Group sub-blocks with a ruled small-caps heading, NOT a nested card.** A
   `<Card>` inside a step's `<Card>` is a box inside a box carrying no extra information (and
   violates "don't nest cards"). Use the section-heading pattern — `text-xs font-medium uppercase
tracking-wide text-muted-foreground` + a `h-px flex-1 bg-border` rule — with an optional 12px
   lead-in line under it. `IntegrationWizard`'s local `WizardSection` is the reference.
4. **Choice cards (scenario / mechanism pickers)** — `role="radiogroup"` wrapping
   `role="radio"` buttons, each: a **`size-9` rounded icon tile** (cyan-tinted when selected,
   `bg-muted` otherwise), a **radio dot** on the same row (filled primary circle with a `Check`
   when chosen, `border-input` ring when not), then a semibold label, then a 12px description.
   The icon is what makes five options distinguishable _before_ the text is read; the dot means
   selection never depends on border colour alone. Grid to the option count
   (`sm:grid-cols-2 xl:grid-cols-5` for five).
5. **Short peer options are chips, not a stacked checkbox column** — see the OAuth scopes. A
   vertical list of five one-word options reads as a long form.
6. **Code the integrator will copy verbatim goes in a dark block with a Copy button** —
   `bg-nb-navy-800 dark:bg-nb-dark`, mono, `dir="ltr"`. **Deliberately a fixed dark surface in
   both themes**: a code block should read as code. This is a sanctioned exception to
   theme-aware surfaces, alongside third-party/device mockups.
7. **Footer controls** — Back (`variant="outline"`) on the start side, the single primary
   (Continue / Create) on the end side, in a `justify-between` row.

### Routes vs Dialogs

**Use a full page route** for drill-down views that show rich detail (e.g. `/kpi/:id` for KPI
detail, entity profiles, report detail). These deserve their own URL, back-button support,
and full-width layout.

**Use a dialog** only for quick confirmations, small forms, or lightweight previews that don't
need their own URL. Never put complex, scrollable, multi-section content in a dialog.

### Empty States

```tsx
<div className="flex flex-col items-center justify-center py-16 text-center">
  <Inbox className="size-12 text-muted-foreground mb-4" />
  <h3 className="text-lg font-bold mb-2">لا توجد بيانات بعد</h3>
  <p className="text-muted-foreground mb-4 max-w-sm">
    ابدأ بإنشاء أول استبيان لجمع آراء العملاء
  </p>
  <Button className="bg-primary text-primary-foreground">إنشاء استبيان</Button>
</div>
```

Empty states must **teach the interface** — explain what goes here and how to fill it.

---

## Layout Rules

### Page Container

```tsx
<div className="px-8">
```

**Fixed 32px gutters, full width — no `max-w-*` cap, no `mx-auto`.** The page fills
the available width inside the sidebar inset with constant **32px** (`px-8`) left/right
padding. Do **not** use `max-w-7xl mx-auto px-4 sm:px-6 lg:px-8` — that caps content at
1280px and centers it, so on wide screens / zoomed-out the empty side margins grow
unevenly. A fixed `px-8` keeps the gutters identical at every viewport width.

### Page Structure

Every page follows this pattern. **Data-dense pages** (lists, builders, dashboards —
e.g. the Journey module) use the tighter `space-y-5 py-5` rhythm; airier content pages
may use `space-y-6 py-6`:

```tsx
<div className="space-y-5 py-5">
  {/* Page Header — stacks on mobile, row on sm+ so the title never crowds the CTA */}
  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
    <div className="min-w-0">
      <h1 className="text-2xl font-heading font-bold">عنوان الصفحة</h1>
      <p className="text-sm text-muted-foreground mt-1">وصف مختصر</p>
    </div>
    <Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground">
      <Plus className="size-4" />
      إجراء رئيسي
    </Button>
  </div>

  {/* Content */}
  {/* ... */}
</div>
```

- **One primary CTA per page** — secondary actions get `variant="outline"` or `variant="ghost"`
- **Page header is responsive by default — `flex-col … sm:flex-row`, title block `min-w-0`.**
  A bare `flex items-center justify-between` header pins the title and CTA on one row at every
  width; a long (or localized-Arabic, ~30–40% longer) title then crowds the button on mobile.
  Stack them under `sm` (`flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between`)
  and give the title `<div>` `min-w-0` so it can shrink/wrap instead of shoving the CTA.

**Header with many actions — give the toolbar its own row (don't squeeze the title).**
The `flex items-center justify-between` header above works for a title + **one or two**
short actions. Once a header/builder card carries **3+ action buttons** (e.g. the journey
builder: Edit details · KPI & Scoring · Detection · Versions · Activate · Archive · Add
Stage), do **NOT** keep them on the same row as the title with `lg:flex-row
lg:justify-between` — the button group is `shrink-0`, so it steals the width and squeezes
the title/meta into a broken one-word-per-line column (`0 / stages / · 0 TPs / Updated /
Jul 7`). Instead **stack**: the title block on its own full-width row, then the action
toolbar on a second full-width row (`flex flex-wrap items-center gap-2`, optionally a
`border-t border-border pt-4` divider so it reads as a toolbar). Keep the lone filled
primary (one-blue rule) last in DOM so it lands at the end of the toolbar. Also give meta
segments `whitespace-nowrap` and wrap them in a `flex flex-wrap gap-x-3` row so a tight
width breaks _between_ segments ("0 stages · 0 TPs" | "Updated …"), never mid-phrase.

### Filter / Toolbar Row (search + selects) — one consistent shape

Every list page's filter row uses the **same** structure so search inputs and selects line
up identically across KPI Management, Personas, Journeys, Audit Log, etc.:

```tsx
<div className="flex flex-col gap-4 sm:flex-row sm:items-end">
  {/* Search — bounded, never unbounded flex-1 */}
  <div className="flex-1 flex flex-col gap-1.5 sm:max-w-sm">
    {" "}
    <label /> <Input />{" "}
  </div>
  {/* Each select — fixed width; gap-1.5 (NOT space-y) so the base-ui trailing node
      can't drop a 6px margin on the trigger and break items-end alignment */}
  <div className="flex flex-col gap-1.5 sm:w-48">
    {" "}
    <label />{" "}
    <Select>
      <SelectTrigger className="w-full" />
    </Select>{" "}
  </div>
  {/* A label-less control (checkbox) — wrap so items-end bottom-aligns it with the inputs */}
  <div className="flex h-10 items-center gap-2">
    {" "}
    <Checkbox /> <Label />{" "}
  </div>
</div>
```

Hard points, each learned from a real misalignment:

- **Bound the search with `sm:max-w-sm`.** A bare `flex-1` search sprawls to fill the whole
  row and leaves a single filter select stranded at the far edge with a dead gap between
  them (looked disconnected on Personas). Bounded search keeps search + selects **grouped**.
- **Selects get a fixed `sm:w-48`** (via `sm:w-48` on the wrapper + `w-full` on the trigger)
  — never let one select auto-size to its content while its neighbour is full-width.
- **`sm:items-end`** so controls bottom-align even though labelled controls are taller than
  a label-less checkbox; wrap the checkbox in `flex h-10 items-center` so its 40px box
  bottom-aligns with the 40px inputs (it centres on the input row, not the label row).
- **Label style is one or the other, consistently per page** — either the small-caps UI
  label (`text-xs font-medium uppercase tracking-widest text-muted-foreground`) or a plain
  `<Label>`; don't mix the two styles within one filter row.

### Grid Layouts

```tsx
// KPI stat cards — 4 columns
<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">

// Content cards — 3 columns
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">

// Dashboard with sidebar
<div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
  <div className="lg:col-span-2">{/* main content */}</div>
  <div>{/* sidebar/filters */}</div>
</div>
```

### Spacing System

Base unit: **4px**. All spacing values are multiples of 4. Use `rem` for layout-level
spacing, `px` for component-internal gaps.

| Value | Token | Usage                                       | Tailwind          |
| ----- | ----- | ------------------------------------------- | ----------------- |
| 4px   | sp-1  | Icon internal gaps, tight inline elements   | `gap-1`           |
| 8px   | sp-2  | Tag padding, compact list items             | `gap-2`, `p-2`    |
| 12px  | sp-3  | Card inner padding small, form field gaps   | `gap-3`, `p-3`    |
| 16px  | sp-4  | Standard component padding, grid gap        | `gap-4`, `p-4`    |
| 24px  | sp-6  | Card padding, section sub-gaps              | `gap-6`, `p-6`    |
| 32px  | sp-8  | Section content padding, large card padding | `gap-8`, `p-8`    |
| 48px  | sp-12 | Between major sections                      | `gap-12`, `py-12` |
| 64px  | sp-16 | Page-level section breaks                   | `gap-16`, `py-16` |
| 96px  | sp-24 | Hero/cover padding, document margins        | `gap-24`, `py-24` |

**Common patterns:**
| Context | Class |
|---------|-------|
| Between page sections (content/marketing pages) | `space-y-6` or `space-y-8` |
| Between page sections (data-dense pages) | `space-y-5` |
| Between related items | `space-y-4` |
| Inside cards | `p-6` |
| Grid gaps (tight) | `gap-4` |
| Grid gaps (standard) | `gap-6` |
| Between form fields | `space-y-4` |

### Border Radius Scale

| Token               | Value         | Usage                                                                |
| ------------------- | ------------- | -------------------------------------------------------------------- |
| `rounded-sm`        | 8px           | Badges, tags, status chips, small chrome                             |
| `rounded-md`        | 12px          | **Action buttons, inputs, selects**, inner containers / tiles        |
| `rounded-lg`        | 16px          | **Cards, dialogs, sheets, popovers, panels** — the max corner radius |
| `rounded-xl` and up | 16px (capped) | Legacy aliases — resolve to 16px; never exceed it                    |
| `rounded-full`      | pill          | Avatars, pill buttons, progress bars (exempt from the cap)           |

> **16px ceiling (hard rule).** No element's corner radius may exceed **16px**
> (`rounded-lg`). Cards and popups sit at 16px; **all interactive controls — action
> buttons, inputs, and selects — sit at 12px (`rounded-md`)** so they read as one
> consistent control family; tiles use 12px too. The `--radius-lg` … `--radius-4xl`
> tokens are all clamped to `1rem` so even a stray `rounded-2xl` stays at 16px.
> Only `rounded-full` (true pills/circles — avatars, progress bars, status dots) and
> **informative ribbons/badges** (which keep their existing radius) are exempt.

### Sidebar Layout

Use shadcn `Sidebar` component with `collapsible="icon"` for pages with navigation.
The sidebar uses `--sidebar` tokens: navy background, white text, cyan active items.
In dark mode, sidebar goes even deeper (`#070B10`) with navy-tinted borders.

> **Dark-mode sidebar separation = a lightness STEP + a visible divider.** In light mode the
> dark-navy sidebar contrasts hard against the light page; in dark mode the sidebar (`#070B10`)
> and the canvas are both dark, so two things keep them distinct: (1) the canvas (`#14161F`)
> sits a clear step _above_ the sidebar (see the surface ladder — never close that gap), and
> (2) the divider is a real line. `--sidebar-border` (dark) is a solid navy `#313650`
> (derived: `withLightness(neutral, 0.34)`), and the inner border renders at full opacity
> (`dark:border-sidebar-border`, not `/30`). Never drop the dark sidebar divider back to a
> low-alpha navy, and never re-darken the canvas toward the sidebar — the sidebar then bleeds
> into the content with no edge.

**RTL-aware sidebar positioning:**

- Pass `side={isRtl ? "right" : "left"}` so the sidebar is on the physical right in RTL
  and physical left in LTR. Never hardcode `side="left"`.
- The sidebar uses physical CSS positioning (`left: 0` / `right: 0`), which does NOT flip
  with `dir="rtl"`. So rounding/border must also be physical, NOT logical.

**Modern sidebar styling:**

- Inner sidebar rounding/border is computed from the `side` JS variable, not CSS logical
  properties or `group-data` selectors (which can fail across nested DOM layers):
  ```tsx
  side === "left"
    ? "rounded-r-xl border-r shadow-[2px_0_12px_-2px_rgba(0,0,0,0.08)]"
    : "rounded-l-xl border-l shadow-[-2px_0_12px_-2px_rgba(0,0,0,0.08)]";
  ```
- Transitions use `duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]` — never `ease-linear`
- No vertical padding on the container (sidebar spans full viewport height edge-to-edge)
- The rail provides the toggle affordance; the topbar trigger handles click

**Adding a new page to the sidebar — categorize, don't append:**
Every new top-level page MUST be registered in `app-sidebar.tsx`'s `NAV_ITEMS`
under an **existing category group** (`nav.overview`, `nav.platform`, …) that
matches the page's domain. Never tack a new item onto the end of an unrelated
group, and never leave a new route unreachable from the sidebar. If no
existing category fits, add a new `NavGroup` with a meaningful `groupKey`
(and the matching i18n string) — do not invent a generic "Other" bucket.
Also add the item's key to every persona's allowlist in `ROLE_NAV_KEYS` that
should see it; role-level visibility is governed there, not by the route.

### Topbar

The topbar must be `sticky top-0 z-30` so the sidebar toggle and search remain accessible
when scrolling. Use `bg-background/95 backdrop-blur-sm` for a frosted-glass effect.

The topbar also hosts the global **language toggle** and **light/dark theme toggle**,
aligned to the end side (`ms-auto`) next to the sidebar trigger and search. Both are
required app chrome — the platform is bilingual by design (ar/en) and dark mode must
allow user override (see Dark Mode), so every authenticated layout MUST surface both
controls. Language toggle: `Languages` icon + the _target_ language's native name
(shows "العربية" in EN, "English" in AR) wired to `useDirection().toggleLang`. Theme
toggle: icon-only `Sun`/`Moon` button wired to `next-themes` `useTheme` (the project's
theming solution — `ThemeProvider attribute="class"` is mounted at the app root and the
sonner toaster depends on it). Both get an `aria-label` per the icon-only-button rule.

**Unauthenticated pages need them too.** The auth flow (login, MFA challenge/enroll,
password reset) renders through `AuthLayout`, which has **no topbar** — so both toggles
MUST live in `AuthLayout` itself, pinned to the top-end corner (`absolute top-4 end-4`,
logical `end-*` so it flips in RTL). A user must be able to pick language and light/dark
**before** signing in (Arabic-first audience; a dark-mode user shouldn't get a white flash
at the login screen). Use the **same** `useDirection().toggleLang` + `next-themes`
`useTheme` wiring and the same `mounted` guard as the topbar, so the choice persists
seamlessly into the authenticated app. Any future full-screen chrome-less layout (onboarding,
error/404, standalone wizards) follows the same rule: no topbar ⇒ surface both toggles in a corner.

### Scroll Restoration

The content area (`<div>` inside `SidebarInset`) uses `overflow-auto`. React Router does NOT
auto-reset scroll position for inner scrollable containers. `AppLayout` handles this:

```tsx
const mainRef = useRef<HTMLDivElement>(null);
useEffect(() => {
  mainRef.current?.scrollTo(0, 0);
}, [location.pathname]);
```

Always scroll to top on route change — failing to do so makes the new page appear "below" the
previous scroll offset, creating blank space above the content.

### Content Area Bottom Padding

The content div uses `pb-20` on mobile (space for bottom nav) and `lg:pb-[50px]` on desktop
(breathing room above the persona switcher bar). Every page benefits from this — never remove it.

---

## Interaction & Animation

### Transitions

- All hover/focus transitions: `transition-colors duration-150`
- Accordion/expand: `transition-all duration-200 ease-out`
- Modal/sheet entrance: `duration-200 ease-out`
- Modal/sheet exit: `duration-150 ease-in` (exits faster than enters)
- Sidebar open/close: `duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]` — smooth, spring-like
- Respect `prefers-reduced-motion`:
  ```tsx
  className = "motion-safe:transition-all motion-safe:duration-200";
  ```

### Expand/Collapse Animation Pattern

**Never use `{condition && <div>...}</div>}`** for expandable panels — this causes instant
show/hide with no transition. Instead use the CSS `grid-rows` trick:

```tsx
<div
  className={cn(
    "grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]",
    expanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0",
  )}
>
  <div className="overflow-hidden">{/* expandable content */}</div>
</div>
```

This animates height to/from zero smoothly without needing a fixed `max-height` value.
Used in the persona switcher and anywhere panels expand/collapse.

**Body padding (don't skip the top).** An expanded accordion/panel body needs real
**top** padding below the header divider — use `pt-4` (16px), never `pt-1`. Match it
with `pb-4` at the bottom so the first item has the same breathing room as the last.
Content that hugs the divider line reads as a rendering glitch. (Builder stage bodies:
`px-4 pb-4 pt-4 … border-t`.)

### Loading States

- Buttons: disable + show `<Loader2 className="animate-spin" />`
- Content areas: use `Skeleton` component, not spinners
- Tables: show skeleton rows matching expected data shape
- Never block the user during loading — keep UI interactive where possible

### Hover States

```tsx
// Cards
className =
  "hover:shadow-md transition-shadow duration-150 dark:hover:border-primary/30";

// Table rows
className = "hover:bg-muted/50 transition-colors";

// Interactive list items
className = "hover:bg-accent transition-colors cursor-pointer";
```

### Focus States

- Never remove focus rings — they are critical for keyboard navigation
- Default: `outline-ring/50` (set globally)
- Custom: `focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2`

---

## Accessibility Checklist (WCAG AA)

### Must Have

- [ ] Color contrast: 4.5:1 for normal text, 3:1 for large text — in BOTH themes
- [ ] All images have descriptive `alt` text
- [ ] All icon-only buttons have `aria-label`
- [ ] Tab order matches visual order
- [ ] Focus rings visible on all interactive elements
- [ ] Form inputs have associated `<Label>`
- [ ] Error messages use `role="alert"` or `aria-live="polite"`
- [ ] Color is never the only indicator (always pair with icon/text)
- [ ] Sequential heading hierarchy (`h1` -> `h2` -> `h3`, no skipping)
- [ ] Skip-to-content link for keyboard users
- [ ] `prefers-reduced-motion` respected for all animations

### RTL-Specific Accessibility

- [ ] Logical tab order works correctly in RTL
- [ ] Screen reader announces content in correct reading direction
- [ ] `lang` attribute set correctly on mixed-language content
- [ ] Icon placement follows reading direction (using `ms-*` / `me-*`)

---

## Backend Integration

The Nabadat backend is ASP.NET Core (.NET 10). The default System.Text.Json
configuration does NOT register `JsonStringEnumConverter`, which has cascading
implications for the frontend. Before writing any TS code that calls a `.NET`
endpoint, run the following pre-flight checks.

> **"Sample data" toggle ≠ API connected — and analytics/report read from
> Elasticsearch, not the relational DB.** The Report/Analytics pages ship a **Sample data**
> switch that renders local `SAMPLE_REPORT` / `SAMPLE_ANALYTICS` constants with **no fetch**
> (`load()` returns early). A page looking correct in sample mode proves the **UI**, never
> that the endpoint is wired or returning data. With the toggle **off** it does call the real
> `GET /surveys/{id}/report|analytics` — but those aggregate from **Elasticsearch** (AD-04),
> and **dev has no response-ingestion pipeline**, so the adapter degrades to `Empty`
> (`UnavailableAnalyticsAggregator` if ES isn't running; the real adapter finds no indexed
> responses if it is). So live analytics render **empty** in dev by design. This is unlike
> **survey lifecycle / status** writes (`POST /surveys/{id}/status`), which are real
> transactional writes to the relational survey record and _do_ persist in dev.

### 1. Enum serialization — assume integers

Run:

```sh
grep -rn "JsonStringEnumConverter\|AddJsonOptions" src/
```

- If the converter is **not** registered globally: every .NET enum (`LifecycleState`,
  `ContactType`, `QuotaType`, `ProvisioningOutcome`, …) arrives on the wire as an
  **integer**, not the string name. Build normalize helpers at the api.ts response
  boundary (`normalize<Enum>(value: T | string | number): T`) AND int-converters at
  the request boundary. The TS types stay as string unions so the components don't
  have to know.
- Wire-format symmetry can be asymmetric: an endpoint may accept snake_case in the URL
  path (`/resource-limits/max_platform_users`) while serializing the same enum as an
  integer in JSON responses. Always read both the controller and the DTO to confirm.
- The defensive shape on display components: never index a static map with the wire
  value directly. Pass it through `normalize<Enum>()` first; default to the safest
  member of the union if it doesn't match.

### 2. DTO contract — read the source

Before writing a TS interface for a request/response, read the actual C# DTO file.
Pay attention to:

- Field names — they camelCase on the wire, but the _base name_ matters. `contactId`
  is NOT the same as `newPrimaryContactId`. Do not guess.
- C# record positional parameters with `[property: Required]` will throw at request
  binding (the validator reads constructor parameters). Flag this if you see it in
  a DTO — it is a backend bug, not a frontend issue.

### 3. API-05 error envelope

Every non-2xx response from the platform follows `{ error: { code, message, request_id, tenant_id } }`.
The `callJson` helper in `tenants/api.ts` parses this — reuse the same pattern for any new feature.

### 4. `callJson` edge cases (non-negotiable)

A successful response is **not always JSON**. Endpoints that return `Ok()` or `NoContent()`
send a 2xx with an empty body and no `content-type` header. The fetch helper MUST:

- Treat 204 as `undefined`.
- Treat 2xx with `content-length: 0` (or missing) and an empty body as `undefined`.
- Only error out if the response is non-2xx OR the body claims JSON but fails to parse.

### 5. Vite dev proxy targets HTTPS, not HTTP

The .NET backend's `app.UseHttpsRedirection()` 307-redirects every HTTP request to
the HTTPS port. Node's `fetch` follows the redirect, hits the self-signed dev cert,
and throws. Configure the proxy as:

```ts
server: {
  proxy: {
    "/api": { target: "https://localhost:7286", changeOrigin: true, secure: false },
  },
},
```

Do not target `http://localhost:5209`. `secure: false` accepts the self-signed dev cert.

### 6. Auth header convention

The TenantAdmin backend's `PortalSessionAdmin` scheme reads `Authorization: Bearer <opaque session token>`.
The login page stores it as `localStorage.session_token`; every authenticated `fetch` MUST
send it. The `useQbsRole` hook supplies the `X-Qbs-Role` header for the role gate stub.

---

## Dev Environment Workflow

These are mechanical steps the agent must take automatically — they are easy to forget
and break the dev loop silently.

### When creating a new top-level feature folder under `src/`

After adding `src/<feature>/`, the Vite dev server's optimize-deps cache can fail to
discover the new module paths and emits `Failed to resolve import "@/..."` errors for
files that exist. **Always** restart the dev server and clear the cache:

```powershell
Stop-Process -Name "node" -Force   # only if you know which node is vite
Remove-Item -Recurse -Force frontend/portal/node_modules/.vite
cd frontend/portal; npm run dev
```

### Before `dotnet build` on the backend

The running `Nabadat.TenantAdmin.exe` locks its own DLLs. Build fails with
MSB3026/MSB3027 (file locked). Stop the process first:

```powershell
Get-Process -Name "Nabadat.TenantAdmin" -ErrorAction SilentlyContinue |
    Stop-Process -Force
```

### Before applying control-plane migrations

`tools/Nabadat.Migrations` is a separate project. Migration classes are discovered by
reflecting over the _built_ assembly — if you added a new `IMigration` and forgot to
rebuild, the runner silently skips it and reports "N migrations applied" with the wrong N.
Always `dotnet build` the tool before `dotnet run --target=control-plane`.

### Library convention check before writing UI

This repo uses **`@base-ui/react`**, not `@radix-ui`. The two libraries have a
near-identical API surface with one crucial difference: Base UI does NOT support
the Radix `asChild` prop. Passing `asChild` is silently ignored (React warns about
the unknown DOM attribute), and wrapping a Base UI trigger (`PopoverTrigger`,
`DropdownMenuTrigger`, `Dialog.Trigger`, `Tooltip.Trigger`) with a `<Button>` or
`<button>` creates the `<button>` cannot be a descendant of `<button>` hydration
error. Always apply styling **directly on the trigger** using `buttonVariants(...)`.

```tsx
// Wrong (Base UI)
<PopoverTrigger asChild>
  <Button variant="ghost" size="icon"><Bell /></Button>
</PopoverTrigger>

// Right
<PopoverTrigger
  className={cn(buttonVariants({ variant: "ghost", size: "icon" }))}
  aria-label="…"
>
  <Bell />
</PopoverTrigger>
```

### Defensive rendering for tab-style components

Base UI `Tabs.Panel` mounts every panel eagerly (and hides inactive ones with the
`hidden` attribute). That means a `useState(() => obj.nested.field)` initializer in
an inactive panel still runs on mount. Any tab/accordion/stepper that reads nested
config MUST run that config through a `withDefaults()` shim that supplies safe
defaults for every nested object. Never assume the backend will populate a field.

---

## DO NOT

- Use physical direction properties (`pl-*`, `ml-*`, `left-*`, `text-left`) — always logical.
  **Exception:** the sidebar inner div uses physical rounding/border (`rounded-r-xl`, `border-r`)
  because the sidebar itself uses physical CSS positioning (`left: 0` / `right: 0`) which does
  not flip with `dir="rtl"`. Logical properties like `rounded-e-xl` break in RTL for this case.
- **Read or copy from the click-through checkout while implementing** — it voids the
  `/clickthrough-parity` audit (see "Click-through-blind implementation"). Build from `spec.md`;
  the design is compared afterwards, not copied beforehand.
- Use raw hex colors — always use Tailwind tokens (`bg-primary`, `text-nb-cyan`, etc.)
- Use inline styles for colors — use design tokens
- Use emojis as icons — use Lucide React SVG icons
- Use `px` values when Tailwind spacing exists
- Use arbitrary Tailwind values like `bg-[#0D8BBC]` — use the token classes
- Create custom components when shadcn has one
- Add fonts not in the design system (no Inter, Roboto, or Arial)
- Use `text-justify` with Arabic text
- Use `break-all` with Arabic text
- Use `text-xs` for Arabic body text
- Nest cards inside cards
- Use pure black `#000000` in dark mode — use Navy or Dark tokens
- Make everything a primary button — use visual hierarchy
- Rely on hover-only interactions — everything must be clickable/tappable
- Show blank empty states — always provide guidance and action
- Skip heading levels
- Remove focus rings
- Put complex, multi-section detail views in dialogs — use full page routes instead
- Use `{condition && <div>}` for expandable panels — use CSS `grid-rows` animation trick
- Use `ease-linear` for sidebar or panel transitions — use `cubic-bezier(0.16,1,0.3,1)`
- Forget scroll restoration on route changes — inner scrollable containers don't auto-reset
- Use Mint as a dashboard background fill — conflicts with D2 Good semantic state. This
  extends to **solid/tinted badges & type-pills** (a `bg-nb-mint` MoT tag, a "Standard" KPI-type
  pill, etc.): a mint fill on a KPI/dashboard surface reads as the D2 "Good" state. Use
  **cyan** (`bg-nb-cyan` / `bg-nb-cyan-100`) for such brand pills — it carries no semantic
  meaning and stays on-brand.
- Use semantic red or green decoratively — they must always signal KPI state
- Reverse the gradient (Cyan → Mint in LTR layouts) — always Mint → Cyan
- Recolor the logo gradient — it is fixed and non-negotiable
- Use QBS brand colors (Blue #2B68B4, Violet #7B5EA7) inside Nabadat products
- Translate Arabic from English — write it natively in فصحى
- Treat accent #2ECC8F as a UI color — it's logo-only

## DO

- Build pages **click-through-blind** from `spec.md`, then run `/clickthrough-parity` after the
  E2E checkpoint and triage its report — an audit only informs if the two sides were independent
- Use logical properties (`ps-*`, `ms-*`, `text-start`, `rounded-s-*`) for RTL
- Use semantic shadcn tokens first, brand tokens (`nb-*`) second
- Use Cyan (#0D8BBC) for primary CTAs, active nav states, and headline KPI values
- Use Cyan for chart series 1 inside dashboards — safer near semantic states
- Apply semantic D1–D5 only for KPI states — never decoratively
- Use Sora for all English headings and display text
- Use Arabic wordmark in all RTL/Arabic-language contexts
- Write Arabic copy natively (فصحى for enterprise/government)
- Keep wordmark color at #1E2235 — never use pure black
- Test every component in both light and dark mode
- Test every page in both RTL (Arabic) and LTR (English)
- Pair status colors with icons (never color alone)
- Use `tabular-nums` for numeric data columns
- Use `leading-relaxed` for Arabic paragraph text
- Add `aria-label` to every icon-only button
- Use `role="alert"` for form error messages
- Add hover/focus states to all interactive elements
- Use Skeleton components for loading states
- Confirm before destructive actions
- Auto-focus first invalid field on form error
- Keep one primary CTA per screen — others get secondary styling
- Use full page routes for drill-down detail views (e.g. `/kpi/:id`)
- Make topbar sticky (`sticky top-0 z-30`) so sidebar toggle is always accessible
- Build custom SVG when Recharts doesn't offer the chart type needed
- Use `grid-rows-[0fr]`/`grid-rows-[1fr]` for smooth expand/collapse animations
- Ensure `pb-[50px]` bottom padding on desktop content area for persona switcher clearance
- Run the gradient always Mint → Cyan (never reversed in LTR)
<!-- FRONTEND END -->

## Backend Module Structure _(applies to .NET backend stories only)_

Backend modules follow a **fixed folder structure** — constitution **AMENDMENT-009** /
architecture-constitution **Article 1A**, with `Nabadat.UserManagement` as the reference.
A backend story adds files into the existing layout; it never invents a new top-level
folder kind. Within `src/Nabadat.<DomainName>/`:

- **`Api/`** — `Controllers/`, `Contracts/` (request/response DTOs), `Middleware/`,
  `Accessors/`, `Interfaces/`, `Tenancy/`. Thin controllers that delegate to Application.
- **`Application/`** — `Interfaces/` (EF context ports `ITenantDbContext` /
  `IControlPlaneDbContext` / `ICurrentTenant`) + one **`<SubDomain>/`** folder per bounded
  concern (`Auth`, `Users`, `Permissions`, `Events`, …), each holding its services plus
  `Interfaces/` (the unit-test mock seam), `Dtos/`, `Exceptions/`.
- **`Domain/`** — `Entities/`, `ValueObjects/`, `Interfaces/` (the **published cross-module
  interfaces** + cross-cutting client ports). References nothing.
- **`Infrastructure/`** — **data-access (fixed, when the module owns tables):**
  `Persistence/` (+`Configurations/`), `ControlPlane/` (+`Configurations/`, only if it
  owns control-plane tables), `Migrations/` (`_Baseline.sql`/`_ControlPlane.sql`). **Every
  other sub-folder is a module-specific adapter grouping** named by the external concern it
  wraps — open set, not a fixed list (M-10 happens to have `Crypto/`, `Auth/`, `Audit/`,
  `Notifications/`). All persistence lives here only.

Dependency direction is inward-only (`Api`/`Infrastructure` → `Application` → `Domain`;
`Api` and `Infrastructure` never reference each other); wiring lives only in
`<DomainName>ServiceCollectionExtensions`. One type per file. Data-access specifics:
DB-08 / database-constitution Article 7.

**Naming — "Context" is reserved for the EF `DbContext`** (architecture-constitution
Article 1A rule 7). Don't put _Context_ in `Api`/`Application` type names for
request-scoped state — name those for what they hold (`ICurrentTenant`, a current-session
accessor like `ICurrentSessionAccessor`). An unqualified _Context_ always means a
`DbContext` (`ITenantDbContext` / `IControlPlaneDbContext`).

<!-- TESTING START -->

## Unit Test Policy _(applies to .NET backend stories only)_

**Scope.** This policy applies to **.NET backend code only** — i.e., user stories
that ship code under `src/Nabadat.*` and unit-tested via `tests/Nabadat.*` xUnit v3
projects. **Frontend testing (`frontend/portal/`, Vitest) is currently out of
scope** of the spec-kit-enforced flow. Frontend stories may include unit tests at
the team's discretion but no `/speckit-tasks`, `/speckit-implement`, or
`/speckit-analyze` enforcement applies to them; the build gate for frontend stories
is `npm run build` only.

Spec-kit features in this repo treat backend unit tests as a first-class story
artifact, not an optional polish task. The full rules are encoded in the spec template
(`.specify/templates/spec-template.md` → "Unit Test Policy") and the tasks template
(`.specify/templates/tasks-template.md`). Summary:

1. **Every user story in `spec.md` MUST contain a `Unit Test Coverage` block** listing
   _Units under test_ and _Required cases_. The block is generated by `/speckit-specify`
   and validated by its Spec Quality Checklist.
2. **A story may skip coverage only by writing exactly**
   `unit-tests: skipped — <one-sentence justification>` in place of the bullets.
   Skipping is reserved for stories with no testable units (pure copy / static markup /
   a scaffold that only composes existing components). "We'll add tests later" is not
   a valid justification.
3. **`/speckit-tasks` emits a `Unit Tests for User Story X (REQUIRED — write FIRST,
must FAIL before implementation)` subsection per non-skipped story.** It refuses to
   generate tasks for a story that has neither populated coverage nor an explicit skip
   declaration — that is a spec defect, fix the spec first.
4. **`/speckit-implement` runs the unit-test tasks before their implementation
   tasks** (red → green). If the unit tests pass on first run against empty stubs,
   that is a defect (the tests are not exercising real behaviour) and must be fixed
   before implementation proceeds.
5. **Test projects are split by kind — never combined.** A single test assembly
   never mixes unit tests with integration tests. The split is a hard separation
   at the `.csproj` level, not a `[Trait]` filter inside one project. This makes
   the per-task build gate fast and Docker-free.
   - **Project naming (constitution AMENDMENT-008)**: backend modules use a
     meaningful domain name `Nabadat.<DomainName>` (the `<Area>` placeholder below)
     — the `M{NN}` token MUST NOT appear in any project / assembly / namespace /
     type name. (Reference module M-10 → `Nabadat.UserManagement`.)
   - **Unit tests**: pure logic, no I/O, no containers, runs in milliseconds.
     `tests/Nabadat.<Area>.UnitTests/<Type>Tests.cs` (e.g.
     `Nabadat.UserManagement.UnitTests`).
   - **Integration tests**: real Postgres / Elasticsearch via Testcontainers,
     ASP.NET Core `WebApplicationFactory`, end-to-end module flows.
     `tests/Nabadat.<Area>.IntegrationTests/`.
   - **Contract tests** (only when a story ships an interface contract):
     `tests/Nabadat.<Area>.ContractTests/`.
   - **Migration note**: any legacy unified `*.Tests` project (mixing unit +
     integration) must be carved into separate `Nabadat.<DomainName>.UnitTests` /
     `Nabadat.<DomainName>.IntegrationTests` projects BEFORE the next story lands in
     that module — track the carve-out as a Foundational task in that story's tasks.md.
6. **Every build = compile + unit tests green** (backend stories only). The build
   is green only when **both** the compile step **and** the affected unit-test
   suite pass. A failing unit test is a build failure and blocks the workflow
   exactly like a compile error.
   - **Per-task gate (between implementation tasks within a story)**: run only
     the affected `Nabadat.<Area>.UnitTests` project. Integration and contract
     tests do NOT run here — they are slow and would force a Docker spin-up
     between every commit. Command: `dotnet test tests/Nabadat.<Area>.UnitTests`.
   - **Per-story checkpoint (end of a user-story phase)**: run the affected unit
     project AND the affected integration project (if the story has one):
     `dotnet test tests/Nabadat.<Area>.UnitTests && dotnet test tests/Nabadat.<Area>.IntegrationTests`.
     Both must be green to declare the checkpoint reached.
   - **Feature-end / CI**: run the full solution: `dotnet test Nabadat.sln`.
     Contract tests included if the feature ships any.
   - **Order on failure**: compile error → stop immediately, do not run tests
     against a project that didn't compile. Test failure → stop, surface the
     failing test, do not advance to the next task.
   - Frontend stories use `npm run build` only (typecheck + bundle). No
     test-pass requirement is enforced for frontend.
7. **Red Checkpoint between tests and implementation (mandatory).** Inside
   `/speckit-implement`, every non-skipped backend user story has a `T0XXR` "Red
   Checkpoint" task between the Unit Tests subsection and the Implementation
   subsection. It:
   - runs `dotnet test tests/Nabadat.<Area>.UnitTests` after the unit-test files
     are written,
   - verifies the run is **red for the right reason** — see "Valid red states"
     below,
   - pastes the failing transcript as evidence,
   - and **commits the red baseline via `/speckit-git-commit`** before any
     implementation task is read or written.

   **Valid red states** (either is accepted, depending on phase state):
   - **Compile error is valid red IF no production type exists yet.** When the
     first test references `RoleAssignmentService` and the class doesn't yet
     exist, the test project fails to compile. That's an honest red state.
     Capture the compiler error in the red commit transcript and proceed.
   - **Assertion failure is required once the production type exists.** Once
     the implementer has scaffolded the class (with stub methods returning
     `default`/`throw new NotImplementedException()`), subsequent test runs MUST
     fail with `Xunit.Sdk.*Exception` (assertion failure) — not a compile
     error, not "no tests found." A test passing against a `null`-returning
     stub is a defect; strengthen the test.

   This makes "tests written before implementation" auditable: `git show <red-commit>`
   shows exactly what the tests asserted before any code existed. Skipping the
   red-commit step defeats the split and is a process defect.

8. **Time is injected, never read directly.** Production code that depends on
   the current time MUST take a `System.TimeProvider` (BCL since .NET 8) via
   constructor injection. Tests inject `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider`
   to control time deterministically. **`DateTime.UtcNow`, `DateTimeOffset.Now`,
   and `Stopwatch.GetTimestamp()` MUST NOT appear in tested production code** —
   replace with `_timeProvider.GetUtcNow()` / `GetTimestamp()`. Existing code
   that violates this rule is grandfathered but must be migrated when its
   owning module is next touched.
9. **Soft case-concreteness check.** `/speckit-specify`'s Spec Quality Checklist
   warns (do NOT block) when a Required-case bullet in any Unit Test Coverage
   block contains no literal input/output. Cases like
   `validateRoleAssignment(new RoleAssignmentRequest(userId, roleId)) → Invalid("user.not_found")`
   pass; cases like "validator rejects invalid input" trigger a warning. The
   reason: concrete cases shrink the room a downstream implementer has to
   shape the test to match the implementation. Concreteness is a recommendation
   today; it may be hardened to a blocking gate later.
10. **Integration tests are MANDATORY for backend stories with observable
    side-effects.** Each backend-bearing story carries an **Integration Test
    Coverage** block in spec.md describing what gets tested end-to-end (which
    endpoints, which service paths). No per-acceptance-scenario ID matrix is
    tracked — the block is authoring guidance, not a verified matrix.

    **Qualifier rule** — a story REQUIRES integration tests if ANY of its
    acceptance scenarios has a "Then" clause involving:
    - **HTTP layer** — status code, response body shape, headers, routing,
      middleware behavior, API-05 envelope.
    - **Database side-effect** — a row written, updated, deleted; a transaction
      committed; an advisory lock acquired.
    - **M-17 event emission** — a row appended to `event_log`.
    - **Cross-module call** — a live in-process call to another module's
      interface (e.g., M-10 → M-11's `IOrgHierarchyService`).

    **Skip declaration**: a backend-bearing story whose acceptance scenarios are
    all pure-logic (verified by unit tests alone) MAY write
    `integration-tests: skipped — <one-sentence justification>` in place of the
    coverage block. Rare — most backend stories ship at least one HTTP/DB/event
    scenario.

11. **API, service, and scenario tests all live inside the IntegrationTests
    project — never separate projects.** They share the same fixture
    (Testcontainers Postgres, migration runner, application factory) so
    splitting them would duplicate infrastructure for no testability gain.
    Inside `tests/Nabadat.<Area>.IntegrationTests/`, organise by intent in
    subfolders:
    - `Infrastructure/` — `<DomainName>ApplicationFactory.cs` (e.g.
      `UserManagementApplicationFactory`), shared seeding helpers, fixture lifecycle.
    - `Endpoints/` — API tests. One test per `<HTTP method> <route>` outcome,
      entered via `WebApplicationFactory<Program>` HTTP requests.
    - `Services/` — service-method integration tests. Skip the HTTP layer to
      verify inner concerns (concurrency, advisory locks, atomic writes,
      transaction isolation) that don't translate cleanly through HTTP.
    - `Scenarios/` — multi-step business-cycle tests. **One test per user
      story whose `Independent Test` field in spec.md describes a flow
      spanning ≥2 endpoints or ≥2 sequential user actions with state carried
      between them** (challenge tokens, JWTs, invitation links, session
      supersession). Each scenario test walks the steps named in the spec's
      Independent Test in order, asserts the final state-of-the-world (event
      counts, user status, session existence) at the end — not after every
      step. The scenario test method name MUST match the user journey it
      verifies (e.g., `NewUser_completes_onboarding_and_logs_in`) so a
      reviewer can map test → story → business requirement at a glance.

    **Scenario-test qualifier rule** — a story REQUIRES a scenario test if
    its `Independent Test` describes a flow that:
    - spans 2+ distinct endpoints or service entry points (e.g., login →
      totp → logout), OR
    - carries state between calls (challenge token round-trips through the
      challenge cache, JWT carries forward to the next call, invitation token
      gates the next call), OR
    - asserts on an aggregate side-effect that only manifests after multiple
      calls (e.g., "after the full onboarding flow, exactly one
      `password.changed` AND one `session.established` event were emitted").

    A story whose `Independent Test` is a single API round-trip (e.g.,
    "lists notes; returns sorted result") does NOT need a scenario test —
    its API test covers the same surface. Declare this with
    `scenario-test: not-needed — <one-sentence reason>` in the spec.

    Keep the scenario-test count small: roughly one per user story that
    qualifies, not one per permutation. The goal is to prove the business
    journey holds together, not to re-test each step.

12. **First-feature-in-module carve-out.** The first backend-bearing story that
    lands in a module without `tests/Nabadat.<DomainName>.IntegrationTests/`
    (named per constitution AMENDMENT-008 — not `Nabadat.Platform.M{NN}`)
    MUST include a Foundational task that creates the project AND the
    `<DomainName>ApplicationFactory : WebApplicationFactory<TEntryPoint>,
IAsyncLifetime` fixture (boots Testcontainers Postgres, applies the
    module's `_Baseline.sql` migration, exposes per-test `HttpClient` and
    seeding helpers). Subsequent stories in that module reuse the factory; the
    one-time cost is amortised by every later story.

13. **Integration tests run at the per-story checkpoint only — not between
    implementation tasks** (already encoded in rule 6's build-gate scope) **and
    do NOT get a Red Checkpoint** (rule 7 is unit-only). Integration tests
    exercise existing types end-to-end; red→green discipline applies to
    unit-test authoring alone. Integration tests are written and run as-is at
    the checkpoint.

14. **Test conventions** (binding across all `.UnitTests` and `.IntegrationTests`
    projects):
    - **Framework**: xUnit v3 (`xunit.v3` `1.*`, `xunit.runner.visualstudio` `3.*`).
    - **Assertion library**: FluentAssertions, **pinned to `6.12.*`** (last MIT-licensed
      release; v7+ requires a paid XCEED commercial license). New test projects
      copy this pin. Bare `Assert.*` is allowed inside FluentAssertions code but
      prefer FluentAssertions for readability.
    - **Mocking / substitution**: NSubstitute `5.*`. Hand-rolled in-memory
      fakes (`Dictionary<Guid, Foo>`-backed `IFooRepository`) are also
      acceptable when stateful behaviour matters — use NSubstitute for
      behaviour verification (call count, argument matchers), fakes for
      stateful collaborators.
    - **Time**: `Microsoft.Extensions.TimeProvider.Testing` `9.*` provides
      `FakeTimeProvider`. Inject it wherever production code takes a
      `TimeProvider`. Shared anchor in
      `tests/Nabadat.TestSupport/Time/TestTime.cs`.
    - **Integration / API testing (`.IntegrationTests` projects only)**:
      `Testcontainers.PostgreSql` `4.*` provisions a fresh Dockerised Postgres
      per fixture lifecycle; `Microsoft.AspNetCore.Mvc.Testing` `10.*` provides
      `WebApplicationFactory<Program>` for in-process HTTP. Docker must be
      running locally and on CI for these tests to execute. Both API and
      service integration tests use this single stack — no parallel
      mock-Postgres or in-memory-EF alternatives are permitted.
    - **Test naming**: `<MethodOrSubject>_<expected_result>_when_<condition>`,
      snake_case for the predicate parts, PascalCase for the subject. Examples:
      `Validate_returns_invalid_when_title_is_empty`,
      `CreateNote_persists_to_storage_when_input_is_valid`,
      `POST_auth_login_returns_401_and_emits_event_when_user_is_deactivated`.
      This convention is mandatory for all new test methods.
    - **One test class per file** (project memory rule
      `feedback_one_type_per_file`).
    - **Test method = public, no parameters unless `[Theory]`**, no `async void`
      (use `async Task` for async tests).

**Frontend testing — unit/Vitest is non-scope; the E2E browser lane IS enforced.**
The **component/unit** layer of `frontend/portal/` and `frontend/admin-portal/` is
NOT covered by this Unit Test Policy: the spec template, tasks template, and
`/speckit-*` skills do not require Unit Test Coverage, Integration Test Coverage,
or red checkpoints for frontend stories, and frontend authors may write Vitest
tests at their discretion without spec-kit enforcement. **However**, the
**end-to-end browser lane is now a first-class, enforced frontend lane** — see the
**E2E Test Policy** section immediately below. Any frontend story that ships
pages/routes in **any `frontend/*` SPA workspace** (`frontend/portal/`,
`frontend/tenant-app/`, …) carries enforced E2E coverage; only the
Vitest/unit layer remains optional. (If the team later brings the unit layer
under enforcement too, the changes are: re-add a Vitest layout bullet to rule 14
and re-introduce `npm test` to the build gate rule 6.)

## E2E Test Policy _(frontend browser lane — applies to stories that ship pages in any `frontend/_` SPA workspace)\*

Each frontend SPA's pages are proven end-to-end by a **Playwright browser lane** authored
with the **`e2e-testing` skill** (`.claude/skills/e2e-testing/SKILL.md`). This is the
frontend's enforced lane — it sits ON TOP OF the backend lanes, it does not replace
them.

1. **Stack & location — one E2E project, organized by module.** The lane uses **MSTest +
   `Microsoft.Playwright.MSTest`** in a **single project, `tests/Nabadat.E2ETests/`**, covering the
   `frontend/` SPA (the repo ships **one** frontend app today — there is no `frontend/portal/` or
   `frontend/tenant-app/` subfolder). Tests are grouped into **module-named folders** (mirroring the
   `Nabadat.<Module>` unit/integration taxonomy — `KpiManagement/`, `CustomerJourneyManagement/`,
   `UserManagement/`, `OrganizationSettings/`, `Accessibility/`). **MSTest is a deliberate exception**
   to the backend's xUnit rule: Playwright ships an official `PageTest` base for MSTest, and MSTest
   `TestContext.AddResultFile` screenshot+trace attachments render in the VS Test Explorer
   _Attachments_ section. The project references no other project and drives the **running** Vite SPA
   over HTTP via `E2E_BASE_URL` (dev default `http://e2e.localhost:5173`, proxying `/api` →
   `https://localhost:7286`). One `COVERAGE.md` matrix (at the project root) carries a row per test.

   **All tests share one harness, `Infrastructure/E2ETestBase.cs`** — a `PageTest` subclass that signs
   in through the app's real, MFA-gated flow (`/login` → TOTP → `sessionStorage.session_token`) and
   exposes persona / active-user / explicit-credential sign-in plus dev-fixture reseed/reset helpers;
   `Infrastructure/E2ETenantDb.cs` seeds the one KPI-binding row no UI can create. If a **second,
   separately authenticated SPA** is ever added (e.g. a future tenant-app), give it its own
   `Infrastructure/<App>/E2ETestBase.cs` rather than branching the existing one — do **not** create a
   second project. Below, **`<E2E project>`** means `tests/Nabadat.E2ETests`.

2. **Mandatory for page-bearing frontend stories.** A frontend story that ships pages or
   routes in any `frontend/*` workspace MUST carry an **E2E Test Coverage** block in
   `spec.md` (user flows under test + required scenarios). It MAY skip only by writing exactly
   `e2e-tests: skipped — <one-sentence justification>` in place of the bullets. Skipping is
   reserved for changes that ship no navigable flow (pure copy edits, a leaf component with
   no route or user journey). "We'll add E2E later" is not a valid justification.

3. **Authored AFTER the pages exist; run at the per-story checkpoint; NO red checkpoint.**
   Unlike backend unit tests, E2E tests are NOT written first and are NOT run between
   implementation tasks (they need a running app + a browser, which is too slow for a
   per-task gate). `/speckit-tasks` emits the **E2E Tests for User Story X 🎭** subsection
   _after_ the Implementation subsection; `/speckit-implement` runs it at the per-story
   checkpoint. Rule 7's red→green discipline (unit lane) does NOT apply to E2E — they
   exercise existing pages, exactly like the integration lane (rule 13).

4. **Build gate for page-bearing frontend stories.** The frontend per-story checkpoint is
   green only when BOTH `npm run build` (typecheck + bundle, from the workspace) AND
   the story's E2E filter pass: `dotnet test <E2E project> --filter
"FullyQualifiedName~<Feature>Tests"`. The E2E run requires the stack up (Postgres + the
   workspace's backend host + `npm run dev`), `E2E_BASE_URL` set to the dev-server URL, and
   the Playwright browsers installed (`playwright.ps1 install`, one-time). A failing E2E test
   that reflects a real app bug is a finding — do not weaken the assertion to force green.

5. **Auth model.** E2E points at the running Vite dev server (which proxies `/api` to the backend
   host) and signs in through the real, **MFA-gated** flow with a **seeded test user** (`SignInAsync`;
   creds + TOTP secret in the gitignored `appsettings.local.json`). The flow is `/login` → TOTP
   challenge, landing the opaque session token in `sessionStorage.session_token` (the app's
   `features/auth/session-token.ts` key). **Token reuse for speed:** `SignInAsync(...)` runs the real
   login+MFA flow only on the _first_ sign-in per user in a run, caches the token, and boots later
   tests pre-authenticated by re-seeding it (no form, no TOTP, no 30s anti-replay wait); it self-heals
   if a token goes stale (the AuthGuard bounce falls back to a real sign-in). **A test that is
   verifying the login/MFA flow itself MUST pass `SignInAsync(forceLogin: true)`** — that bypasses the
   cache and always drives the real login UI (otherwise the test would short-circuit and prove nothing).
   E2E writes are **real DB rows** (no transaction rollback, unlike the integration lane) — keep
   inputs unique, clean up, or assert read-only.

6. **One shared harness inside the project (first feature owns it).** The `tests/Nabadat.E2ETests/`
   project is registered once in `Nabadat.TenantAdmin.sln`; the single `Infrastructure/E2ETestBase.cs`
   fixture is reused by every page-bearing story. Because the repo has one frontend app, there is one
   harness. A future second, separately authenticated SPA would add its own
   `Infrastructure/<App>/E2ETestBase.cs` (see the skill's "Adding a new SPA to the merged project") —
   it does NOT create a new project.

7. **Conventions.** One scenario per `[TestMethod]`; one feature class per file
   (`feedback_one_type_per_file`); every test traces to a `COVERAGE.md` row carrying its
   `ID`; method naming follows the same `<Subject>_<expected>_when_<condition>` pattern as
   the backend lanes; prefer stable `id`/`role`/`data-testid` selectors over translated
   text (the portal is bilingual ar/en + RTL); read the screenshot/trace before concluding
   a test passed.

If this policy ever conflicts with the platform constitution
(`.specify/memory/constitution.md`), surface it as a constitution amendment proposal —
do **not** silently resolve it in the spec.

<!-- TESTING END -->
