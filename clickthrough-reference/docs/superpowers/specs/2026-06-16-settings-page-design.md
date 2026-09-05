# Settings Page — Design Spec
**Date:** 2026-06-16  
**Status:** Approved  
**Source SRS:** Section 12 (Settings Page)

---

## 1. Overview

Tenant-level configuration surface. Two sections in v1: **Organization** and **Customer Journey**. Master → detail navigation: the landing page lists sections; selecting one opens a dedicated page. Back returns to the landing list.

---

## 2. Routes

| Path | Component | Description |
|------|-----------|-------------|
| `/settings` | `SettingsPage` | Landing — navigable section list |
| `/settings/organization` | `SettingsOrganizationPage` | Name, Logo, Industry |
| `/settings/customer-journey` | `SettingsCustomerJourneyPage` | ScoringConfig (5 params) |

All three are wrapped in the existing `LayoutRoute` (sidebar + topbar).

---

## 3. Files

### New Files

| File | Purpose |
|------|---------|
| `src/types/settings.ts` | `OrgConfig` and `ScoringConfig` TypeScript interfaces |
| `src/data/mock-settings.ts` | Default values matching SRS spec |
| `src/contexts/settings-context.tsx` | Shared React context; same pattern as `kpi-context.tsx` |
| `src/pages/SettingsPage.tsx` | Landing page — two section rows |
| `src/pages/SettingsOrganizationPage.tsx` | Organization form |
| `src/pages/SettingsCustomerJourneyPage.tsx` | Customer Journey / ScoringConfig form |

### Modified Files

| File | Change |
|------|--------|
| `src/App.tsx` | Add 3 `LayoutRoute` entries for `/settings`, `/settings/organization`, `/settings/customer-journey` |
| `src/components/layout/app-sidebar.tsx` | Add `settings` `NavItem` under `cx.navPlatform` group; add `"settings"` key to `cx_manager` and `tenant_admin` in `ROLE_NAV_KEYS` |
| `src/i18n/locales/en.json` | Add `settings.*` translation keys |
| `src/i18n/locales/ar.json` | Add matching Arabic translations |

---

## 4. Data Types (`src/types/settings.ts`)

```ts
export interface OrgConfig {
  name: string           // max 150 chars, required
  logoUrl: string | null // object URL or null
  industry: Industry
}

export type Industry =
  | "Banking"
  | "Telecommunications"
  | "Government"
  | "Automotive"
  | "Entertainment"
  | "Services"

export interface ScoringConfig {
  alpha: number           // 0.000–1.000, default 0.500; β = 1 − α (never stored)
  motMultiplier: number   // 1.0–2.0, default 1.5
  nFloor: number          // integer ≥ 1, default 500
  flagPercentile: number  // integer 1–49, default 25
  rollingWindowDays: number // integer ≥ 7, default 30
}
```

---

## 5. Mock Data (`src/data/mock-settings.ts`)

```ts
export const INITIAL_ORG_CONFIG: OrgConfig = {
  name: "Nabadat Demo Tenant",
  logoUrl: null,
  industry: "Banking",
}

export const INITIAL_SCORING_CONFIG: ScoringConfig = {
  alpha: 0.5,
  motMultiplier: 1.5,
  nFloor: 500,         // Q-S1 resolved: 500 for this UI
  flagPercentile: 25,
  rollingWindowDays: 30,
}
```

---

## 6. Context (`src/contexts/settings-context.tsx`)

Provides `orgConfig`, `scoringConfig`, `saveOrg(updated: OrgConfig)`, `saveScoring(updated: ScoringConfig)`. Uses `useState` initialised from mock data. Wraps app in `App.tsx` alongside existing providers.

---

## 7. Settings Landing Page (`src/pages/SettingsPage.tsx`)

- Page title: `t("settings.title")` — "Nabadat Settings"
- Subtitle: `t("settings.subtitle")`
- Two section rows rendered from a config array, each row:
  - Icon (Building2 for Org, Route for Customer Journey)
  - Title + description
  - Chevron end-side arrow → `navigate("/settings/<section>")`
  - Hover: `hover:bg-muted/50` + `cursor-pointer`
- Rows sit in a `<Card>` with `divide-y divide-border` for separation (no individual cards per row)
- Group header "CONFIGURATION" above the card (same `text-xs font-medium uppercase tracking-widest text-muted-foreground` style used elsewhere)

---

## 8. Organization Section (`src/pages/SettingsOrganizationPage.tsx`)

### Layout
Two-column grid `grid-cols-[55fr_45fr]` matching KpiConfigPage pattern:
- **Left:** form card
- **Right:** sticky preview card showing current logo + tenant name

### Fields

| Field | Component | Rules |
|-------|-----------|-------|
| Name | `Input` | Required, max 150 chars |
| Logo | File input (`<input type="file" accept=".png,.jpg,.jpeg,.svg">`) + current logo preview | Show thumbnail of current; "Replace" button triggers re-pick; store as `objectURL`; 2 MB soft warning |
| Industry | `Select` | Options: Banking, Telecommunications, Government, Automotive, Entertainment, Services |

### Actions
- **Save** (`bg-primary`) — validates Name non-empty, calls `saveOrg()`
- **Cancel** (ghost) — if `isDirty`, shows unsaved-changes AlertDialog

### Unsaved-Changes Guard
`isDirty` flag; AlertDialog on back/navigate-away: "You have unsaved changes. Are you sure you want to leave?" — Cancel stays, Confirm leaves.

---

## 9. Customer Journey Section (`src/pages/SettingsCustomerJourneyPage.tsx`)

### Layout
Single full-width card (no right preview panel — no visual preview needed for numeric config).

### Fields

| Field | Control | Range | Default |
|-------|---------|-------|---------|
| Alpha (α) | `Slider` (min=0, max=1, step=0.001) + read-only β display | 0.000–1.000 | 0.500 |
| MOT Multiplier | `Slider` (min=1, max=2, step=0.1) + numeric `Input` beside it (two-way linked) | 1.0–2.0 | 1.5 |
| Responses Count Floor | `Input` type=number, min=1 | ≥ 1 | 500 |
| Flag Percentile | `Input` type=number, min=1, max=49 | 1–49 | 25 |
| Rolling Window Days | `Input` type=number, min=7 | ≥ 7 | 30 |

**Alpha/β display:** The slider value controls α; β shown read-only as `(1 − α).toFixed(3)` in a muted box to the right of the slider. Live-updating.

**Each field row has a `?` icon** (Info from Lucide) at top-right of its label using the existing shadcn `Tooltip` + `TooltipTrigger` pattern (keyboard-focusable, Esc-dismissible per WCAG AA).

### Tooltip Content (EN)

**Alpha (α):** "Customer importance blend weight. Alpha is the weight given to Importance to Customer versus Importance to Business when calculating a touchpoint's weight within its stage. A higher alpha means customer importance counts more; a lower alpha means business importance counts more. Beta is automatically set to (1 − alpha), so the two always sum to 1.0. Range 0.0–1.0, default 0.5 (balanced)."

**MOT Multiplier:** "Score amplification applied to touchpoints flagged as a Moment of Truth (MoT). A MoT touchpoint's weight in stage scoring is multiplied by this value; non-MoT touchpoints are unaffected. Range 1.0–2.0, default 1.5. Use 1.0 to disable MoT amplification; raise toward 2.0 to make Moments of Truth dominate stage scores more strongly."

**Responses Count Floor:** "The hard minimum number of responses a touchpoint must collect before its score is trusted. Below this floor, the touchpoint is excluded from scoring and shown as '—'. Set higher for high-volume tenants; lower for low-traffic programmes. Range: ≥ 1, default 500."

**Flag Percentile:** "Sets how aggressively low-sample touchpoints are flagged as 'low confidence'. The system finds the response count at this percentile across all touchpoints; any below it gets a low-sample badge. Range 1–49, default 25."

**Rolling Window Days:** "The time window over which response counts are measured to compute adaptive confidence thresholds. Also the cold-start period for new journeys. Range: ≥ 7, default 30."

### Validation (on Save)

| Field | Rule | Error |
|-------|------|-------|
| Alpha | 0.0 ≤ α ≤ 1.0 | "Alpha must be between 0.0 and 1.0." |
| MOT Multiplier | 1.0 ≤ value ≤ 2.0 | "MOT multiplier must be between 1.0 and 2.0." |
| n_floor | integer ≥ 1 | "Responses count floor must be at least 1." |
| Flag Percentile | integer 1–49 | "Flag percentile must be between 1 and 49." |
| Rolling Window Days | integer ≥ 7 | "Rolling window must be at least 7 days." |

Errors displayed below each field using `<p role="alert" className="text-xs text-destructive">`.

### Actions
- **Save** — validates, calls `saveScoring()`; navigates back to `/settings` on success
- **Cancel** (ghost) — unsaved-changes guard if `isDirty`

---

## 10. Sidebar

- Nav item added to `cx.navPlatform` group: `{ key: "settings", labelKey: "cx.navSettings", icon: Settings, href: "/settings" }`
- `"settings"` key added to `cx_manager` and `tenant_admin` roles in `ROLE_NAV_KEYS`
- `cx.navSettings` already exists in both en.json and ar.json — no new nav key needed

---

## 11. Translation Keys (`settings.*`)

All keys added to both `en.json` and `ar.json`:

```
settings.title                 — "Nabadat Settings" / "إعدادات نبضات"
settings.subtitle              — "Manage your organisation and platform configuration." / "..."
settings.sectionGroup          — "CONFIGURATION" / "الإعدادات"
settings.orgTitle              — "Organisation" / "المؤسسة"
settings.orgDesc               — "Name, logo, and industry vertical." / "..."
settings.cjTitle               — "Customer Journey" / "رحلة العميل"
settings.cjDesc                — "Scoring model parameters for all journeys." / "..."
settings.orgName               — "Organisation Name" / "اسم المؤسسة"
settings.orgNamePlaceholder    — "e.g. National Bank" / "..."
settings.orgLogo               — "Logo" / "الشعار"
settings.orgLogoHint           — "PNG, JPG or SVG. Recommended max 2 MB." / "..."
settings.orgLogoReplace        — "Replace" / "استبدال"
settings.orgIndustry           — "Industry" / "القطاع"
settings.orgIndustryBanking    — "Banking" / "البنوك"
settings.orgIndustryTelecom    — "Telecommunications" / "الاتصالات"
settings.orgIndustryGovt       — "Government" / "الحكومة"
settings.orgIndustryAuto       — "Automotive" / "السيارات"
settings.orgIndustryEntertain  — "Entertainment" / "الترفيه"
settings.orgIndustryServices   — "Services" / "الخدمات"
settings.cjAlpha               — "Alpha (α)" / "ألفا (α)"
settings.cjBeta                — "Beta (β)" / "بيتا (β)"
settings.cjBetaDerived         — "Derived: 1 − α" / "مشتق: 1 − α"
settings.cjMot                 — "MOT Multiplier" / "مضاعف اللحظة الحاسمة"
settings.cjNFloor              — "Responses Count Floor" / "الحد الأدنى لعدد الاستجابات"
settings.cjFlagPct             — "Flag Percentile" / "المئين للإشارة"
settings.cjRollingDays         — "Rolling Window Days" / "أيام النافذة المتحركة"
settings.saveSuccess           — "Settings saved." / "تم حفظ الإعدادات."
settings.unsavedTitle          — "You have unsaved changes." / "..."
settings.unsavedBody           — "Are you sure you want to leave?" / "..."
settings.errAlpha              — "Alpha must be between 0.0 and 1.0." / "..."
settings.errMot                — "MOT multiplier must be between 1.0 and 2.0." / "..."
settings.errNFloor             — "Responses count floor must be at least 1." / "..."
settings.errFlagPct            — "Flag percentile must be between 1 and 49." / "..."
settings.errRollingDays        — "Rolling window must be at least 7 days." / "..."
settings.errNameRequired       — "Organisation name is required." / "..."
settings.logoSizeWarning       — "Logo exceeds 2 MB. Consider using a smaller file." / "..."
```

---

## 12. Constraints & Notes

- **Slider API:** `@base-ui/react/slider` — `onValueChange` receives `(value: number | readonly number[], eventDetails)`. Extract `Array.isArray(value) ? value[0] : value`. No `asChild` prop usage.
- **n_floor default:** Resolved as **500** for this UI (Q-S1). Comment in code for future reconciliation with M-16 SRS (which specifies 5).
- **Logo upload:** Client-side only (object URL). No real API call in v1 prototype.
- **RTL:** All layout uses logical properties (`ps-*`, `ms-*`, `text-start`, etc.).
- **Radius cap:** Cards at `rounded-lg`, inputs/selects at `rounded-md`.
- **Slider thumb sizing:** Base UI slider thumb is 12px (`size-3`) — do not add custom `h-*` to the slider container.
