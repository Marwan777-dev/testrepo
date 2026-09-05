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

CLAUDE.md is a **living document** owned by the **frontend lead**.

**Owner identification:** Before modifying CLAUDE.md, check `git config user.email`.
- If the email is `mrwantarik50@gmail.com` or `m.elsayed@qbs.jo` → this is the owner. Apply updates directly.
- If the email is **anything else** → this is a team member. **Do NOT modify CLAUDE.md.**
  Instead, suggest changes as a message so the owner can review.

Only the frontend developer should modify this file directly. All other team members
(backend, business, COO) should **follow** CLAUDE.md but never change it.

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
- **Min/Max labels**: at arc endpoints (e.g., "-100" and "+100" for NPS)
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
className="ps-4 pe-2 ms-auto me-0 text-start rounded-s-lg border-e"

// Wrong — breaks in RTL
className="pl-4 pr-2 ml-auto mr-0 text-left rounded-l-lg border-r"
```

| Physical (NEVER use) | Logical (ALWAYS use) |
|----------------------|---------------------|
| `pl-*` / `pr-*` | `ps-*` / `pe-*` |
| `ml-*` / `mr-*` | `ms-*` / `me-*` |
| `left-*` / `right-*` | `start-*` / `end-*` |
| `text-left` / `text-right` | `text-start` / `text-end` |
| `rounded-l-*` / `rounded-r-*` | `rounded-s-*` / `rounded-e-*` |
| `border-l-*` / `border-r-*` | `border-s-*` / `border-e-*` |
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

// Date formatting — position differs per locale
<time>{isArabic ? "١٢ مايو ٢٠٢٦" : "May 12, 2026"}</time>
```

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
|-------------------------------------|---------------------------------------------|
| Marketing and communications | KPI status indicators only |
| Product chrome and navigation | Dashboard severity states |
| UI components and buttons | Performance threshold badges |
| Illustrations and hero backgrounds | Alert and notification states |
| Chart series colors (non-semantic) | Closed-loop case status |
| Logo and identity elements | Survey score bands |

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

| Degree | Name | Base | Light Token | Dark Token | Threshold |
|--------|------|------|-------------|------------|-----------|
| D1 | Excellent | `#1A7A3C` | `#D4F4E2` | `#0D4A24` | >110% target · Revenue over target · NPS above benchmark |
| D2 | Good | `#2EB85C` | `#C8F5DB` | `#156632` | 90–110% · Pipeline health · CSAT on track |
| D3 | Caution | `#E8A020` | `#FFF0CC` | `#7A5000` | 70–89% · Drifting NPS · Response time rising |
| D4 | Warning | `#E05C1A` | `#FFE4D0` | `#7A2800` | 50–69% · SLA breach risk · Escalation queue full |
| D5 | Critical | `#C01B2A` | `#FFD6DA` | `#6B0010` | <50% · System failure · Major complaint surge |

**Token Usage:**
- **Base color** — text values, icons, dot indicators
- **Light token** — badge backgrounds, row fills
- **Dark token** — text ON light-token backgrounds
- **Scale direction** — always D1 (deep green) to D5 (deep red)

**Hard Rules:**
- Never use semantic colors for chart series or decorative fills
- Never skip degrees — show actual state even if it jumped to D5
- D1 and D5 must be rare — overuse destroys meaning
- D2 should be the most common state on a healthy dashboard

### Semantic shadcn Colors (use these first)
These map to shadcn utility classes. Always prefer semantic tokens over brand tokens:

| Token | Light Mode | Dark Mode | Usage |
|-------|-----------|-----------|-------|
| `bg-primary` | Cyan `#0D8BBC` | Cyan-lite `#59CCEA` | CTAs, active nav, headline KPI values |
| `text-primary-foreground` | White | Navy `#1E2235` | Text on primary backgrounds |
| `bg-secondary` | Mint `#13DB9B` | Mint-lite `#54DE8C` | Secondary accents, chart series 1 |
| `text-secondary-foreground` | White | Navy `#1E2235` | Text on secondary backgrounds |
| `bg-accent` | Cloud `#EEF1F7` | Navy-dark `#161B29` | Feature sections, highlights |
| `text-accent-foreground` | Navy `#1E2235` | Cloud `#EEF1F7` | Text on accent backgrounds |
| `bg-muted` | Cloud `#EEF1F7` | Navy-800 `#0E1018` | Subtle backgrounds |
| `text-muted-foreground` | Stone `#7A8196` | Stone-lite `#B8BFCE` | Secondary text, captions |
| `bg-destructive` | D5 base `#C01B2A` | D5 lite `#FFD6DA` | Errors, delete actions |
| `border-border` | Navy-200 `#C4CADD` | Navy tint `rgba(30,34,53,0.3)` | All borders |
| `bg-background` | Off-white `#F7F9FC` | Dark `#0A0C11` | Page background — lightly navy-tinted, never pure white; just enough that white cards lift without the page reading grey |
| `text-foreground` | Navy `#1E2235` | Off-white `#EDEDEF` | Primary body text |
| `bg-card` | White | Dark-2 `#1B1C27` | Card surfaces |

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
const D1 = "#1A7A3C" // Excellent
const D2 = "#2EB85C" // Good
const D3 = "#E8A020" // Caution
const D4 = "#E05C1A" // Warning
const D5 = "#C01B2A" // Critical

function perfColor(value: number, kpiId?: string): string {
  // KPI-aware thresholds — different metrics have different "good" ranges
  // NPS: 50+ is great (scale is -100 to +100, segment values are 20-60)
  // CES: LOWER is better (30 is great, 60+ is poor — inverted)
  // Default: standard 0-100 percentage scale

  if (kpiId === "ces") {
    // Inverted — lower effort = better
    if (value <= 30) return D1
    if (value <= 40) return D2
    if (value <= 50) return D3
    if (value <= 60) return D4
    return D5
  }
  if (kpiId === "nps") {
    if (value >= 50) return D1
    if (value >= 40) return D2
    if (value >= 30) return D3
    if (value >= 20) return D4
    return D5
  }
  // Default (CSAT, Agent, VFM, FCR — all 0-100%)
  if (value >= 85) return D1
  if (value >= 75) return D2
  if (value >= 60) return D3
  if (value >= 45) return D4
  return D5
}
```

**Always pass `kpiId` when coloring segment/branch bars** so that NPS 52 shows
D1/green (great for NPS) while the same value 52 for CES shows D3/amber (poor for CES).

**Status-tinted containers — use the D-scale light tokens.** When a container
*represents* a scored entity — a journey-report **stage column**, a touchpoint card,
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
Two-Palette Rule intact (semantic D-tokens signal state, never decorate). Bar *tracks*
stay neutral (`bg-muted/40`); it is the stage/segment *container* that takes the tint.

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
shade lives in the *column background* behind it (see the Journey Map spec above), so the
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
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  });

  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark");
    localStorage.setItem("theme", theme);
  }, [theme]);

  return { theme, setTheme, toggle: () => setTheme(t => t === "dark" ? "light" : "dark") };
}
```

### Dark Mode Design Rules

**Surface hierarchy (light-to-dark elevation):**
```
Background (deepest):  #0A0C11  — page base (navy-tinted black, deepened so cards lift)
Card (elevated):       #1B1C27  — cards, panels, table rows
Muted (subtle):        #2E3044  — hover states, active items
Accent (highlight):    #161B29  — selected states, feature areas
```

**Key principles:**
1. **Navy-tinted, not grey.** Dark mode uses deep navy-blues, never neutral greys. This keeps brand identity present even in dark mode.
2. **Never use pure black `#000000`.** It causes OLED smearing and feels unnatural. Our deepest color is `#0A0C11`. Use Navy or Dark tokens instead.
3. **Lighten, don't invert.** Primary cyan goes from `#0D8BBC` (light) to `#59CCEA` (dark) — brighter, not simply inverted.
4. **Borders go translucent.** Use `rgba(30, 34, 53, 0.3)` not solid colors. This creates depth.
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

| Role | Font | Size | Weight | Notes |
|------|------|------|--------|-------|
| Display/Hero | Poppins/Sora | 40–64px | 800 | Letter-spacing -0.02em. Cover & hero only |
| Heading 1 | Poppins/Sora | 26–32px | 700 | Line-height 1.2. Section titles |
| Heading 2 | Poppins/Sora | 18–22px | 700 | Line-height 1.3. Card titles |
| Body | Poppins | 14–16px | 400 | Line-height 1.7. Paragraphs and descriptions |
| UI Label | Poppins | 10–12px | 500 | Uppercase. Letter-spacing 0.12em. Tags, badges |
| Data / KPI | Poppins | 12–14px | 400 | Tabular numerals. KPI values, hex codes, data tables |

**Tailwind mapping:**

| Element | Classes | Notes |
|---------|---------|-------|
| Page title | `text-2xl font-heading font-bold` | One per page |
| Section title | `text-lg font-bold` | |
| Card title | `text-base font-bold` | |
| Body text | `text-sm` or `text-base leading-relaxed` | Use `leading-relaxed` for Arabic paragraphs |
| Caption/meta | `text-sm text-muted-foreground` | |
| Small label / UI Label | `text-xs font-medium uppercase tracking-widest text-muted-foreground` | Avoid for Arabic body text |
| Stat number | `text-3xl font-heading font-bold tabular-nums` | Use `tabular-nums` for aligned numbers |
| Data table cell | `text-sm tabular-nums` | Numeric columns get `tabular-nums` |

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
import { Plus, Search, Download, Trash2 } from "lucide-react"
<Plus className="size-4" />
```
- Icon-only buttons MUST have `aria-label`: `<Button size="icon" aria-label="حذف"><Trash2 /></Button>`
- Standard size: `size-4` (16px) inline, `size-5` (20px) in nav

### Button Variants
| Action | Component | Example |
|--------|-----------|---------|
| Primary action | `<Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground">` (hover shadow is built into the variant) | اشتراك, إنشاء, حفظ, نشر |
| Secondary action | `<Button variant="secondary">` — soft cyan, **derived from the primary** | تفاصيل, تحليلات, إضافة مرحلة |
| Destructive | `<Button variant="destructive">` | حذف, إزالة |
| Ghost/subtle | `<Button variant="ghost">` | Navigation, toolbar |
| Loading state | `<Button disabled><Loader2 className="size-4 animate-spin ms-2" />جاري الحفظ...</Button>` | During async ops |

**Action-button hierarchy (the one-blue rule).** A page header may show several
action buttons, but **exactly one is the filled blue primary** (`variant="default"`,
`bg-primary`). **Every other action uses the secondary style — regardless of how many
there are.** With three actions (e.g. "Analytics" + "Add Stage" + "Publish") the
2nd *and* 3rd are both secondary; with four, the 2nd/3rd/4th are all secondary. Only
one stays the filled primary. Per-row actions like "Add Touchpoint" follow the same
rule. Secondary actions use `variant="secondary"`: a **lighter shade of the same
primary cyan** (`bg-nb-cyan-100 / text-nb-cyan-800`; dark `bg-nb-cyan-900/40 /
text-nb-cyan-200`). It stays in the brand family but reads visibly quieter than the
solid primary, so the one primary remains *the* action. Rules:
- Never promote a second button to the **filled** blue — only one filled primary per page.
- Never leave a secondary action as a plain neutral/`outline` button — it looks empty
  next to the primary; the secondary must carry the soft-cyan fill so the pair reads
  as a deliberate primary + secondary set.
- Never use the **mint** token for action buttons — mint clashes with the D2 "Good"
  semantic state. The button `secondary` variant has been redefined to the cyan-derived
  style specifically for action buttons (it no longer renders mint).
- **Compact secondary (inside accordions / tables).** A secondary action that lives in
  an accordion row or a table uses `variant="secondary" size="compact"` — the *same*
  soft-cyan style at **35px** tall (`size="compact"`, still `rounded-md`/12px) instead
  of the full 40px, so it doesn't crowd the row. Example: the per-stage "Add Touchpoint"
  button. Don't hand-set `h-7`/`h-8` on these — use `size="compact"`.

**Action-button shadows are hover-only.** Both the blue primary and the soft-cyan
secondary carry **no shadow at rest** — on hover, *both* lift with a soft colored
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
- **Don't double the top padding.** The base `<Card>` already supplies `py-4` (16px top *and* bottom). Do **not** add `pt-6` (or any extra `pt-*`) to `CardContent` — that stacks on the card's own padding and makes the top visibly heavier than the bottom. Use a bare `<CardContent>` so vertical padding stays balanced (this applies to every card: header cards, filter cards, etc.).
- **Header cards align center.** When a card's content is a header row (title/badges on one side, action buttons on the other), the flex row uses `lg:items-center` (not `lg:items-start`) so the action buttons sit vertically centered against the title block.

### Badges / Tags
- **Outline tags have a white fill.** The Badge `outline` variant ships `bg-card`
  (white in light, dark surface in dark) — never leave a tag transparent, which makes
  it disappear into tinted backgrounds (e.g. the D-light stage columns). A plain
  `<Badge variant="outline">` is therefore a white pill with a border.
- **Colored tags keep their color.** Tags that set their own background via `className`
  (status pills like `bg-d2-light`, `bg-nb-cyan-100`, …) override the white fill — the
  Badge merges classes with `tailwind-merge`, so the explicit `bg-*` wins. Only
  *backgroundless* (outline) tags pick up the white fill.

### Forms
- Use shadcn `Input`, `Select`, `Checkbox`, `Textarea`, `Label`
- **Fields are white, not transparent.** Inputs, selects, and textareas use `bg-card`
  (white in light mode; the elevated dark surface in dark mode) — never `bg-transparent`,
  which would let the off-white page background bleed through and make fields look grey.
  The `border-input` edge provides the boundary.
- **Always use visible labels** — never placeholder-only inputs
- Labels go above inputs, use `<Label htmlFor="...">` for accessibility
- Group fields: `<div className="space-y-4">`
- Required fields: add `<span className="text-destructive">*</span>` next to label
- Validation errors below the field: `<p className="text-sm text-destructive mt-1" role="alert">رسالة الخطأ</p>`
- Error messages must state **cause + fix**, not just "خطأ في الإدخال"
- On submit error, auto-focus the first invalid field
- Use semantic input types: `type="email"`, `type="tel"`, `type="number"` for correct mobile keyboards

### Tables (Data-Dense)
Use shadcn `Table` components. For data-heavy tables:
- Sticky header: `<TableHeader className="sticky top-0 bg-background z-10">`
- Row hover: add `hover:bg-muted/50` to `<TableRow>`
- Numeric columns: `text-end tabular-nums`
- Empty state: never show a blank table — show helpful message with action
- Sortable columns: add `aria-sort` attribute (`ascending` / `descending` / `none`)
- Consider virtualizing lists with 50+ rows

### Dialogs & Sheets
- Confirmations and quick forms: `<Dialog>` — centered, max-w-md
- Side panels and filters: `<Sheet>` — slides from `start` side (right in RTL)
- Confirm before destructive actions (delete, discard changes)
- Scrim opacity: 40-60% for clear foreground separation
- Always provide close/dismiss affordance
- **Tall content rule:** Any dialog whose content can exceed the viewport
  (provisioning forms, multi-section settings, long contact lists) MUST be
  constrained with `max-h-[90vh]` and scroll internally with
  `overflow-y-auto`. The default styling on `<DialogContent>` already
  applies this — do not override it back. Never let a dialog clip off
  screen edges or push the viewport into double-scrollbar territory.
  Confirmation dialogs (short, fixed copy) inherit the same default
  harmlessly because `overflow-y-auto` is a no-op when content fits.

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
  <p className="text-muted-foreground mb-4 max-w-sm">ابدأ بإنشاء أول استبيان لجمع آراء العملاء</p>
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
  {/* Page Header */}
  <div className="flex items-center justify-between">
    <div>
      <h1 className="text-2xl font-heading font-bold">عنوان الصفحة</h1>
      <p className="text-sm text-muted-foreground mt-1">وصف مختصر</p>
    </div>
    <Button className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground">
      <Plus className="size-4 ms-2" />
      إجراء رئيسي
    </Button>
  </div>

  {/* Content */}
  {/* ... */}
</div>
```
- **One primary CTA per page** — secondary actions get `variant="outline"` or `variant="ghost"`

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

| Value | Token | Usage | Tailwind |
|-------|-------|-------|----------|
| 4px | sp-1 | Icon internal gaps, tight inline elements | `gap-1` |
| 8px | sp-2 | Tag padding, compact list items | `gap-2`, `p-2` |
| 12px | sp-3 | Card inner padding small, form field gaps | `gap-3`, `p-3` |
| 16px | sp-4 | Standard component padding, grid gap | `gap-4`, `p-4` |
| 24px | sp-6 | Card padding, section sub-gaps | `gap-6`, `p-6` |
| 32px | sp-8 | Section content padding, large card padding | `gap-8`, `p-8` |
| 48px | sp-12 | Between major sections | `gap-12`, `py-12` |
| 64px | sp-16 | Page-level section breaks | `gap-16`, `py-16` |
| 96px | sp-24 | Hero/cover padding, document margins | `gap-24`, `py-24` |

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
| Token | Value | Usage |
|-------|-------|-------|
| `rounded-sm` | 8px | Badges, tags, status chips, small chrome |
| `rounded-md` | 12px | **Action buttons, inputs, selects**, inner containers / tiles |
| `rounded-lg` | 16px | **Cards, dialogs, sheets, popovers, panels** — the max corner radius |
| `rounded-xl` and up | 16px (capped) | Legacy aliases — resolve to 16px; never exceed it |
| `rounded-full` | pill | Avatars, pill buttons, progress bars (exempt from the cap) |

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
    : "rounded-l-xl border-l shadow-[-2px_0_12px_-2px_rgba(0,0,0,0.08)]"
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

### Scroll Restoration
The content area (`<div>` inside `SidebarInset`) uses `overflow-auto`. React Router does NOT
auto-reset scroll position for inner scrollable containers. `AppLayout` handles this:
```tsx
const mainRef = useRef<HTMLDivElement>(null)
useEffect(() => { mainRef.current?.scrollTo(0, 0) }, [location.pathname])
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
  className="motion-safe:transition-all motion-safe:duration-200"
  ```

### Expand/Collapse Animation Pattern
**Never use `{condition && <div>...}</div>}`** for expandable panels — this causes instant
show/hide with no transition. Instead use the CSS `grid-rows` trick:
```tsx
<div className={cn(
  "grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]",
  expanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
)}>
  <div className="overflow-hidden">
    {/* expandable content */}
  </div>
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
className="hover:shadow-md transition-shadow duration-150 dark:hover:border-primary/30"

// Table rows
className="hover:bg-muted/50 transition-colors"

// Interactive list items
className="hover:bg-accent transition-colors cursor-pointer"
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
- Field names — they camelCase on the wire, but the *base name* matters. `contactId`
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
    "/api": { target: "https://localhost:7002", changeOrigin: true, secure: false },
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
reflecting over the *built* assembly — if you added a new `IMigration` and forgot to
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
- Use Mint as a dashboard background fill — conflicts with D2 Good semantic state
- Use semantic red or green decoratively — they must always signal KPI state
- Reverse the gradient (Cyan → Mint in LTR layouts) — always Mint → Cyan
- Recolor the logo gradient — it is fixed and non-negotiable
- Use QBS brand colors (Blue #2B68B4, Violet #7B5EA7) inside Nabadat products
- Translate Arabic from English — write it natively in فصحى
- Treat accent #2ECC8F as a UI color — it's logo-only

## DO
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
