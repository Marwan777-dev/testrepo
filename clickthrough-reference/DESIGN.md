---
name: Nabadat
description: Voice of Customer SaaS platform for enterprise and government
colors:
  nb-cyan: "#0D8BBC"
  nb-cyan-100: "#CCF0FB"
  nb-cyan-200: "#98E1F2"
  nb-cyan-300: "#59CCEA"
  nb-cyan-700: "#0087A8"
  nb-cyan-800: "#005F7A"
  nb-cyan-900: "#003A4D"
  nb-mint: "#13DB9B"
  nb-mint-100: "#CBF5EB"
  nb-mint-200: "#96EDD4"
  nb-mint-300: "#54DE8C"
  nb-mint-700: "#0DA670"
  nb-mint-800: "#07704E"
  nb-mint-900: "#034530"
  nb-navy: "#1E2235"
  nb-navy-100: "#E3EBF4"
  nb-navy-200: "#C4CADD"
  nb-navy-300: "#8B90A5"
  nb-navy-700: "#161B29"
  nb-navy-800: "#0E1018"
  nb-navy-900: "#070B10"
  nb-dark: "#0D0F14"
  nb-dark-2: "#1B1C27"
  nb-dark-3: "#2E3044"
  nb-stone: "#7A8196"
  nb-stone-lt: "#B8BFCE"
  nb-cloud: "#EEF1F7"
  d1-excellent: "#1A7A3C"
  d1-light: "#D4F4E2"
  d2-good: "#2EB85C"
  d2-light: "#C8F5DB"
  d3-caution: "#E8A020"
  d3-light: "#FFF0CC"
  d4-warning: "#E05C1A"
  d4-light: "#FFE4D0"
  d5-critical: "#C01B2A"
  d5-light: "#FFD6DA"
  gradient-start: "#1EC99A"
  gradient-end: "#00B4D8"
typography:
  display:
    fontFamily: "'Sora', 'Poppins', 'IBM Plex Sans Arabic', sans-serif"
    fontSize: "40px-64px"
    fontWeight: 800
    lineHeight: 1.1
    letterSpacing: "-0.02em"
  heading-1:
    fontFamily: "'Sora', 'Poppins', 'IBM Plex Sans Arabic', sans-serif"
    fontSize: "26px-32px"
    fontWeight: 700
    lineHeight: 1.2
  heading-2:
    fontFamily: "'Sora', 'Poppins', 'IBM Plex Sans Arabic', sans-serif"
    fontSize: "18px-22px"
    fontWeight: 700
    lineHeight: 1.3
  body:
    fontFamily: "'Poppins', 'IBM Plex Sans Arabic', sans-serif"
    fontSize: "14px-16px"
    fontWeight: 400
    lineHeight: 1.7
  label:
    fontFamily: "'Poppins', 'IBM Plex Sans Arabic', sans-serif"
    fontSize: "10px-12px"
    fontWeight: 500
    lineHeight: 1.4
    letterSpacing: "0.12em"
  data:
    fontFamily: "'Fira Code', monospace"
    fontSize: "12px-14px"
    fontWeight: 400
    lineHeight: 1.5
rounded:
  sm: "8px"
  md: "12px"
  lg: "20px"
  xl: "32px"
  full: "9999px"
spacing:
  sp-1: "4px"
  sp-2: "8px"
  sp-3: "12px"
  sp-4: "16px"
  sp-6: "24px"
  sp-8: "32px"
  sp-12: "48px"
  sp-16: "64px"
  sp-24: "96px"
components:
  button-primary:
    backgroundColor: "{colors.nb-cyan}"
    textColor: "#ffffff"
    rounded: "{rounded.md}"
    padding: "10px 24px"
  button-primary-hover:
    backgroundColor: "{colors.nb-cyan-700}"
    textColor: "#ffffff"
    rounded: "{rounded.md}"
    padding: "10px 24px"
  button-outline:
    backgroundColor: "transparent"
    textColor: "{colors.nb-navy}"
    rounded: "{rounded.md}"
    padding: "10px 24px"
  button-destructive:
    backgroundColor: "{colors.d5-critical}"
    textColor: "#ffffff"
    rounded: "{rounded.md}"
    padding: "10px 24px"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.nb-navy}"
    rounded: "{rounded.md}"
    padding: "10px 24px"
  card:
    backgroundColor: "#ffffff"
    textColor: "{colors.nb-navy}"
    rounded: "{rounded.lg}"
    padding: "24px"
  input:
    backgroundColor: "#ffffff"
    textColor: "{colors.nb-navy}"
    rounded: "{rounded.md}"
    padding: "8px 12px"
---

# Design System: Nabadat

## 1. Overview

**Creative North Star: "The Command Bridge"**

Nabadat is the bridge of a vessel navigating customer sentiment. Every instrument is
precise, every readout is trusted, and every display leads to a decision. The interface
exists to make customer data legible and actionable for people who carry institutional
responsibility: banking executives, government quality directors, telecom operations leads.

The aesthetic is clean, data-dense, and warm. Not cold enterprise, not playful startup.
Nabadat occupies a narrow band between institutional credibility and human approachability.
The Cyan/Mint brand palette keeps it from feeling sterile; the navy-tinted dark mode
keeps it from feeling generic. Every surface is designed for someone who needs to trust
the numbers and act on them within minutes.

The system explicitly rejects generic Bootstrap dashboards, busy over-decorated interfaces,
and "AI slop" aesthetics (neon-on-dark, gradient text, hero metric cards). It also rejects
clinical coldness. Behind every score is a person, and the interface reflects that.

**Key Characteristics:**
- Data-dense without feeling cluttered: whitespace is structural, not decorative
- Bilingual Arabic/English as a first-class architectural decision (RTL-first, logical properties)
- Navy-tinted dark mode that preserves brand identity instead of collapsing to generic grey
- Two-palette discipline: brand colors (Cyan/Mint/Navy) never signal KPI status; semantic colors (D1-D5) never appear decoratively
- Professional warmth: Poppins body type and mint accents prevent the interface from feeling hostile

## 2. Colors: The Coastal Palette

A teal-and-navy palette anchored in the colors of deep water and reef light. Three brand
families (Cyan, Mint, Navy) carry the chrome, and a five-degree semantic scale (D1-D5)
carries all KPI status signaling. These two systems never cross.

### Primary

- **Nabadat Cyan** (#0D8BBC): primary brand color. CTAs, active navigation states, headline
  KPI values, chart series 1. In dark mode, lightens to Cyan-300 (#59CCEA) for contrast.
- **Nabadat Mint** (#13DB9B): secondary brand accent. Chart series 2, secondary CTAs,
  decorative highlights. In dark mode, lightens to Mint-300 (#54DE8C).

### Secondary

- **Nabadat Navy** (#1E2235): wordmark color, dark sidebar surfaces, primary text in light
  mode. The anchor of the entire palette. Never use pure black (#000000) anywhere; Navy
  is the deepest brand-aligned dark.

### Neutral

- **Deepest Surface** (#0D0F14): dark mode page background. Navy-tinted black, not neutral grey.
- **Elevated Surface** (#1B1C27): dark mode cards, panels, table rows. One step above deepest.
- **Dark Divider** (#2E3044): dark mode hover states, active items, subtle separation.
- **Deep Nav** (#161B29): dark mode selected states, feature areas, sidebar accent.
- **Stone** (#7A8196): muted text, secondary icons, placeholder content.
- **Stone Light** (#B8BFCE): disabled states, captions, tertiary text.
- **Cloud** (#EEF1F7): subtle backgrounds in light mode, table alternate rows, muted fills.
- **Navy-200** (#C4CADD): all borders in light mode, input strokes, dividers.

### Semantic (D1-D5 KPI Scale)

These five degrees are a shared standard with the QBS platform. Enterprise clients see
consistent KPI signaling across products. Each degree has a base (text/icons), light
(badge backgrounds), and dark (text on light backgrounds) variant.

- **D1 Excellent** (#1A7A3C / light #D4F4E2 / dark #0D4A24): above 110% of target. Rare by design.
- **D2 Good** (#2EB85C / light #C8F5DB / dark #156632): 90-110% of target. The healthy default.
- **D3 Caution** (#E8A020 / light #FFF0CC / dark #7A5000): 70-89%. Drifting metrics.
- **D4 Warning** (#E05C1A / light #FFE4D0 / dark #7A2800): 50-69%. SLA breach risk.
- **D5 Critical** (#C01B2A / light #FFD6DA / dark #6B0010): below 50%. Immediate action needed.

### Named Rules

**The Two-Palette Rule.** Brand colors (Cyan, Mint, Navy) never signal KPI status. Semantic
colors (D1-D5) never appear decoratively. A mint-colored background tint that happens to
look like D2 green is a violation. This is the hardest rule in the system and the most
important. Mixing the palettes destroys the reliability of status signaling.

**The Named Gradient Rule.** The brand gradient always runs Mint to Cyan (#1EC99A to #00B4D8),
never reversed in LTR layouts. The gradient is used for brand identity elements only, not
for KPI or status indication.

**The KPI-Aware Color Rule.** Performance colors must account for the KPI being displayed.
NPS 52 is excellent (D1) because NPS ranges from -100 to +100. CES 52 is caution (D3)
because lower effort is better. Always pass the KPI identifier when applying `perfColor()`.

## 3. Typography

**Heading Font:** Sora (with Poppins and IBM Plex Sans Arabic fallbacks)
**Body Font:** Poppins (with IBM Plex Sans Arabic fallback)
**Monospace/Data Font:** Fira Code

**Character:** Sora's geometric confidence pairs with Poppins' friendly openness. Sora
carries authority in headings; Poppins keeps body text approachable. IBM Plex Sans Arabic
provides native Arabic rendering throughout the fallback chain. The pairing says "we are
precise and we are human" simultaneously.

### Hierarchy

- **Display** (800 weight, 40-64px, line-height 1.1, letter-spacing -0.02em): cover and hero
  sections only. Sora. Reserved for the rarest, most prominent text on screen.
- **Heading 1** (700 weight, 26-32px, line-height 1.2): section titles. Sora. One per page
  maximum.
- **Heading 2** (700 weight, 18-22px, line-height 1.3): card titles, panel headers. Sora.
- **Body** (400 weight, 14-16px, line-height 1.7): paragraphs, descriptions, form help text.
  Poppins. Arabic body text uses `leading-relaxed` (1.625) for comfortable reading.
- **Label** (500 weight, 10-12px, uppercase, letter-spacing 0.12em): tags, badges, small
  metadata. Poppins. Never use for Arabic body text (minimum 14px for Arabic).
- **Data** (400 weight, 12-14px, tabular-nums): KPI values, table cells, hex codes. Fira Code.
  Always apply `tabular-nums` for aligned numeric columns.

### Named Rules

**The Arabic Minimum Rule.** Arabic body text is never smaller than 14px (text-sm). The label
scale (10-12px) is prohibited for Arabic body content. Arabic paragraph text always uses
`leading-relaxed` (1.625 line-height). Arabic text is never justified (`text-justify`) and
never broken with `break-all`.

**The No Translation Rule.** Arabic copy is written natively in Modern Standard Arabic. It is
never translated from English. The formal register is required for government and banking
contexts.

## 4. Elevation

The system uses a hybrid approach: subtle shadows in light mode for gentle lift, and tonal
layering in dark mode where shadows are invisible against deep surfaces.

### Shadow Vocabulary

- **Card Rest** (`shadow-sm`): light mode cards. Gentle, barely perceptible lift. Enough to
  separate card from background without drawing attention.
- **Card Hover** (`shadow-md`): interactive cards on hover. Slight deepening to signal
  interactivity. Applied via `hover:shadow-md motion-safe:transition-shadow`.
- **None in Dark Mode** (`dark:shadow-none`): dark mode relies entirely on border and
  surface-color differentiation. Shadows on dark backgrounds look artificial.

### Dark Mode Depth Strategy

Depth is conveyed through surface color steps, not shadows:
- Page base: #0D0F14 (deepest)
- Cards/panels: #1B1C27 (one step up)
- Hover/active: #2E3044 (two steps up)
- Selected/feature: #161B29 (accent layer)

Borders go translucent in dark mode (`rgba(30, 34, 53, 0.3-0.5)`) to create soft edges
that suggest depth without hard lines.

### Named Rules

**The Navy-Tinted Rule.** Dark mode surfaces are always navy-tinted, never neutral grey. This
keeps brand identity present even when the interface is dark. The deepest surface (#0D0F14)
has a visible blue undertone. Pure black (#000000) is prohibited; it causes OLED smearing
and feels lifeless.

**The Lighten-Not-Invert Rule.** Primary colors in dark mode brighten rather than invert.
Cyan goes from #0D8BBC (light mode) to #59CCEA (dark mode). Mint goes from #13DB9B to
#54DE8C. This maintains recognizability while ensuring contrast on dark surfaces.

## 5. Components

All components are sourced from shadcn/ui first, customized with Nabadat tokens second,
and built from scratch only when shadcn has no equivalent. Every component must look like
it was designed for Nabadat from the start; no library defaults survive to production.

### Buttons

- **Shape:** gently curved edges (12px radius, `rounded-md`)
- **Primary:** Cyan background (#0D8BBC), white text. Padding 10px 24px. Used for one
  primary CTA per screen.
- **Hover:** deepens to Cyan-700 (#0087A8). Transition: `transition-colors duration-150`.
- **Focus:** `focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2`.
- **Outline:** transparent background, navy text, border. For secondary actions (details, cancel).
- **Ghost:** transparent, no border. For navigation, toolbar items.
- **Destructive:** D5 red (#C01B2A), white text. For delete and remove actions.
- **Loading:** disabled state with `Loader2` spinning icon and descriptive text.
- **Icon-only:** requires `aria-label`. Standard icon size: 16px (`size-4`).

### Cards / Containers

- **Corner Style:** generous curve (20px radius, `rounded-lg`)
- **Background:** white in light mode, #1B1C27 in dark mode
- **Shadow Strategy:** `shadow-sm` light mode, `dark:shadow-none` dark mode
- **Border:** `border-border` (Navy-200 light, translucent navy dark)
- **Internal Padding:** 24px (`p-6`)
- **Hover (interactive):** `hover:shadow-md motion-safe:transition-shadow dark:hover:border-primary/30`
- **Nesting:** prohibited. Flatten hierarchy instead.
- **Structure:** always use `CardHeader` + `CardTitle` + `CardDescription` + `CardContent` + `CardFooter`

### Inputs / Fields

- **Style:** white background, Navy-200 border (#C4CADD), 12px radius
- **Focus:** ring shifts to primary cyan. `focus-visible:ring-2 focus-visible:ring-primary`
- **Labels:** always visible above the input, never placeholder-only. Connected via `htmlFor`.
- **Error:** red text below field with `role="alert"`. States cause and fix, not just "error".
- **Required:** red asterisk next to label text
- **Grouping:** `space-y-4` between form fields

### Navigation (Sidebar)

- **Surface:** Navy (#1E2235) background, white text. Even deeper (#070B10) in dark mode.
- **Active item:** Cyan text/icon with navy accent background
- **Hover:** subtle navy-700 (#161B29) background shift
- **Collapsible:** `collapsible="icon"` mode with smooth cubic-bezier transition
- **RTL:** sidebar uses physical CSS positioning (not logical) because `left: 0`/`right: 0`
  don't flip with `dir="rtl"`. Rounding and border are physical (`rounded-r-xl` not `rounded-e-xl`).
- **Topbar:** sticky (`sticky top-0 z-30`), frosted glass (`bg-background/95 backdrop-blur-sm`)

### KPI Gauge (Signature Component)

Semicircular dual-ring SVG gauge: the signature visualization of the platform.
- **Outer ring** (thick, ~6% of size): value arc in KPI brand color, unfilled in `stroke-muted/20`
- **Inner ring** (thin, ~1.5%): three colored zones (red 0-33%, amber 33-55%, green 55-100%)
- **Needle dot:** filled circle at current value on outer arc, white stroke for contrast
- **Target marker:** tick line + arrow at target position, small "T" label
- **Center:** large value text + small KPI label below
- **Below gauge:** 3-tile segment grid (e.g. Promoters | Passives | Detractors) with tinted backgrounds
- **Below segments:** change indicator (triangle + value) + total responses count

### Spider/Radar Chart (Signature Component)

Custom SVG radar with radial gradient performance zones.
- **Gradient fill:** red center (opacity 0.6) through orange, amber to green outer (opacity 0.2)
- **White cutout:** `fill-rule="evenodd"` mask between outer boundary and data polygon
- **Per-vertex dots:** colored by performance level using `perfColor()` per KPI
- **Value labels:** next to each dot, colored to match
- **Grid levels:** polygons at 20/40/60/80/100 with subtle stroke

## 6. Do's and Don'ts

### Do:

- **Do** use logical CSS properties everywhere (`ps-*`, `pe-*`, `ms-*`, `me-*`, `text-start`,
  `rounded-s-*`). Exception: sidebar inner div uses physical rounding because the sidebar
  uses physical CSS positioning.
- **Do** use semantic shadcn tokens first (`bg-primary`, `text-muted-foreground`), brand
  tokens second (`bg-nb-cyan`, `text-nb-mint`).
- **Do** apply the D1-D5 scale with `perfColor(value, kpiId)` and always pass the KPI
  identifier so NPS and CES thresholds are correct.
- **Do** use `tabular-nums` on all numeric data columns and KPI values.
- **Do** test every component in both light/dark mode AND both RTL/LTR directions.
- **Do** pair every status color with an icon or text label. Color alone is never sufficient.
- **Do** use `role="alert"` on form error messages and state cause + fix.
- **Do** build custom SVG for gauges, radar charts, sentiment rings, and any visualization
  requiring gradients, zones, or per-element coloring. Recharts is for standard x/y plots only.
- **Do** use CSS `grid-rows-[0fr]`/`grid-rows-[1fr]` for expand/collapse animations.
  Never use `{condition && <div>}` for expandable panels.
- **Do** run the brand gradient Mint to Cyan (#1EC99A to #00B4D8), never reversed.
- **Do** write Arabic copy natively in Modern Standard Arabic. Formal register for government
  and banking.

### Don't:

- **Don't** use physical direction properties (`pl-*`, `ml-*`, `left-*`, `text-left`).
  Always logical. (Sidebar exception noted above.)
- **Don't** use raw hex colors inline. Use Tailwind tokens (`bg-primary`, `text-nb-cyan`).
- **Don't** use arbitrary Tailwind values like `bg-[#0D8BBC]`. Use the defined token classes.
- **Don't** use pure black (#000000) anywhere. The deepest color is Navy Dark (#0D0F14).
- **Don't** use generic Bootstrap dashboards with default blue-grey color schemes.
- **Don't** create "AI slop" aesthetics: neon accents on dark backgrounds, gradient text
  (`background-clip: text`), hero metric templates (big number + small label + gradient),
  identical card grids.
- **Don't** use glassmorphism decoratively. Rare and purposeful, or nothing.
- **Don't** use border-left/border-right greater than 1px as colored accent stripes.
- **Don't** use Mint as a dashboard background fill. It conflicts with D2 Good semantic state.
- **Don't** use semantic red or green decoratively. They must always signal KPI state.
- **Don't** use `text-justify` with Arabic text. It distorts letter connections.
- **Don't** use `break-all` with Arabic text. Use `break-words`.
- **Don't** use `text-xs` for Arabic body text. Minimum is `text-sm` (14px).
- **Don't** nest cards inside cards. Flatten the hierarchy.
- **Don't** put complex multi-section detail views in dialogs. Use full page routes.
- **Don't** use `ease-linear` for sidebar or panel transitions. Use `cubic-bezier(0.16,1,0.3,1)`.
- **Don't** use Inter, Roboto, or Arial. They are off-brand.
- **Don't** use emojis as icons. Lucide React SVG icons only.
- **Don't** use em dashes in UI copy. Use commas, colons, semicolons, periods, or parentheses.
- **Don't** translate English to Arabic. Write Arabic natively.
