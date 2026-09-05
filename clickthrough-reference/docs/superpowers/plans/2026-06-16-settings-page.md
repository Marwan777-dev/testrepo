# Settings Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Nabadat Settings page with Organization and Customer Journey sections, master-detail navigation, unsaved-changes guard, alpha/beta slider, tooltips, and full EN/AR i18n.

**Architecture:** Master-detail via React Router (3 routes). Shared state lives in `SettingsContext` (same pattern as `kpi-context`). Each section page is self-contained: loads from context on mount, tracks local `isDirty`, guards navigation with an AlertDialog. The α slider derives β live as `1 − α`; β is never stored.

**Tech Stack:** React 19 + TypeScript, React Router, react-i18next, @base-ui/react (Slider, Tooltip — no `asChild`), shadcn/ui (Card, Input, Select, Label, Button, AlertDialog), Lucide React icons.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `src/types/settings.ts` | `OrgConfig`, `ScoringConfig`, `Industry` types |
| Create | `src/data/mock-settings.ts` | Initial mock values |
| Create | `src/contexts/settings-context.tsx` | Shared React context + `useSettings` hook |
| Create | `src/pages/SettingsPage.tsx` | Landing page — section list |
| Create | `src/pages/SettingsOrganizationPage.tsx` | Name / Logo / Industry form |
| Create | `src/pages/SettingsCustomerJourneyPage.tsx` | ScoringConfig form (5 params + sliders + tooltips) |
| Modify | `src/App.tsx` | 3 routes + `SettingsProvider` wrapper |
| Modify | `src/components/layout/app-sidebar.tsx` | `settings` nav item + role keys |
| Modify | `src/i18n/locales/en.json` | Add `settings` namespace |
| Modify | `src/i18n/locales/ar.json` | Add `settings` namespace (Arabic) |

---

## Task 1: Types and Mock Data

**Files:**
- Create: `src/types/settings.ts`
- Create: `src/data/mock-settings.ts`

- [ ] **Step 1: Create `src/types/settings.ts`**

```typescript
// src/types/settings.ts

export type Industry =
  | "Banking"
  | "Telecommunications"
  | "Government"
  | "Automotive"
  | "Entertainment"
  | "Services"

export interface OrgConfig {
  name: string         // max 150 chars, required
  logoUrl: string | null
  industry: Industry
}

export interface ScoringConfig {
  alpha: number          // 0.000–1.000; β = 1 − α (never stored)
  motMultiplier: number  // 1.0–2.0
  nFloor: number         // integer ≥ 1
  flagPercentile: number // integer 1–49
  rollingWindowDays: number // integer ≥ 7
}
```

- [ ] **Step 2: Create `src/data/mock-settings.ts`**

```typescript
// src/data/mock-settings.ts
import type { OrgConfig, ScoringConfig } from "@/types/settings"

export const INITIAL_ORG_CONFIG: OrgConfig = {
  name: "Nabadat Demo Tenant",
  logoUrl: null,
  industry: "Banking",
}

// n_floor default: 500 (resolved for this UI as high-volume tenant default;
// M-16 SRS specifies 5 — reconcile before production deployment, Q-S1)
export const INITIAL_SCORING_CONFIG: ScoringConfig = {
  alpha: 0.5,
  motMultiplier: 1.5,
  nFloor: 500,
  flagPercentile: 25,
  rollingWindowDays: 30,
}
```

- [ ] **Step 3: Commit**

```bash
git add src/types/settings.ts src/data/mock-settings.ts
git commit -m "feat(settings): add types and mock data"
```

---

## Task 2: Settings Context

**Files:**
- Create: `src/contexts/settings-context.tsx`

- [ ] **Step 1: Create `src/contexts/settings-context.tsx`**

```typescript
// src/contexts/settings-context.tsx
import { createContext, useContext, useState } from "react"
import { INITIAL_ORG_CONFIG, INITIAL_SCORING_CONFIG } from "@/data/mock-settings"
import type { OrgConfig, ScoringConfig } from "@/types/settings"

interface SettingsContextValue {
  orgConfig: OrgConfig
  scoringConfig: ScoringConfig
  saveOrg: (updated: OrgConfig) => void
  saveScoring: (updated: ScoringConfig) => void
}

const SettingsContext = createContext<SettingsContextValue | null>(null)

export function SettingsProvider({ children }: { children: React.ReactNode }) {
  const [orgConfig, setOrgConfig] = useState<OrgConfig>(INITIAL_ORG_CONFIG)
  const [scoringConfig, setScoringConfig] = useState<ScoringConfig>(INITIAL_SCORING_CONFIG)

  function saveOrg(updated: OrgConfig) {
    setOrgConfig(updated)
  }

  function saveScoring(updated: ScoringConfig) {
    setScoringConfig(updated)
  }

  return (
    <SettingsContext.Provider value={{ orgConfig, scoringConfig, saveOrg, saveScoring }}>
      {children}
    </SettingsContext.Provider>
  )
}

export function useSettings() {
  const ctx = useContext(SettingsContext)
  if (!ctx) throw new Error("useSettings must be used within SettingsProvider")
  return ctx
}
```

- [ ] **Step 2: Commit**

```bash
git add src/contexts/settings-context.tsx
git commit -m "feat(settings): add SettingsContext and useSettings hook"
```

---

## Task 3: i18n — Add `settings` Namespace

**Files:**
- Modify: `src/i18n/locales/en.json`
- Modify: `src/i18n/locales/ar.json`

Both JSON files end with `"surveys": { ... }` as the last namespace. Add `"settings"` after it (before the closing `}`).

- [ ] **Step 1: Add `settings` object to `en.json`**

Open `src/i18n/locales/en.json`. Find the very last line (a single `}`). Replace:

```json
}
```

with:

```json
,
  "settings": {
    "title": "Nabadat Settings",
    "subtitle": "Manage your organisation and platform configuration.",
    "sectionGroup": "CONFIGURATION",
    "orgTitle": "Organisation",
    "orgDesc": "Name, logo, and industry vertical.",
    "cjTitle": "Customer Journey",
    "cjDesc": "Scoring model parameters for all journeys.",
    "orgName": "Organisation Name",
    "orgNamePlaceholder": "e.g. National Bank",
    "orgLogo": "Logo",
    "orgLogoHint": "PNG, JPG or SVG. Recommended max 2 MB.",
    "orgLogoReplace": "Replace",
    "orgLogoUpload": "Upload Logo",
    "orgIndustry": "Industry",
    "orgIndustryBanking": "Banking",
    "orgIndustryTelecom": "Telecommunications",
    "orgIndustryGovt": "Government",
    "orgIndustryAuto": "Automotive",
    "orgIndustryEntertain": "Entertainment",
    "orgIndustryServices": "Services",
    "cjAlpha": "Alpha (α)",
    "cjBeta": "Beta (β)",
    "cjBetaDerived": "Derived: 1 − α",
    "cjMot": "MOT Multiplier",
    "cjNFloor": "Responses Count Floor",
    "cjFlagPct": "Flag Percentile",
    "cjRollingDays": "Rolling Window Days",
    "saveSuccess": "Settings saved.",
    "unsavedTitle": "You have unsaved changes.",
    "unsavedBody": "Are you sure you want to leave? Your changes will be lost.",
    "errAlpha": "Alpha must be between 0.0 and 1.0.",
    "errMot": "MOT multiplier must be between 1.0 and 2.0.",
    "errNFloor": "Responses count floor must be at least 1.",
    "errFlagPct": "Flag percentile must be between 1 and 49.",
    "errRollingDays": "Rolling window must be at least 7 days.",
    "errNameRequired": "Organisation name is required.",
    "logoSizeWarning": "Logo exceeds 2 MB. Consider using a smaller file.",
    "tooltipAlpha": "Customer importance blend weight. Alpha is the weight given to Importance to Customer versus Importance to Business when calculating a touchpoint's weight within its stage. A higher alpha means customer importance counts more; a lower alpha means business importance counts more. Beta is automatically set to (1 − alpha), so the two always sum to 1.0. Range 0.0–1.0, default 0.5 (balanced).",
    "tooltipMot": "Score amplification applied to touchpoints flagged as a Moment of Truth (MoT). A MoT touchpoint's weight in stage scoring is multiplied by this value; non-MoT touchpoints are unaffected. Range 1.0–2.0, default 1.5. Use 1.0 to disable MoT amplification; raise toward 2.0 to make Moments of Truth dominate stage scores more strongly.",
    "tooltipNFloor": "The hard minimum number of responses a touchpoint must collect before its score is trusted. Below this floor, the touchpoint is excluded from scoring and shown as '—'. Set higher for high-volume tenants; lower for low-traffic programmes. Range: ≥ 1, default 500.",
    "tooltipFlagPct": "Sets how aggressively low-sample touchpoints are flagged as 'low confidence'. The system finds the response count at this percentile across all touchpoints; any below it gets a low-sample badge. Range 1–49, default 25.",
    "tooltipRollingDays": "The time window over which response counts are measured to compute adaptive confidence thresholds. Also the cold-start period for new journeys. Range: ≥ 7, default 30."
  }
}
```

- [ ] **Step 2: Add `settings` object to `ar.json`**

Open `src/i18n/locales/ar.json`. Find the very last line (a single `}`). Replace:

```json
}
```

with:

```json
,
  "settings": {
    "title": "إعدادات نبضات",
    "subtitle": "إدارة إعدادات مؤسستك والنظام.",
    "sectionGroup": "الإعدادات",
    "orgTitle": "المؤسسة",
    "orgDesc": "الاسم والشعار والقطاع.",
    "cjTitle": "رحلة العميل",
    "cjDesc": "معاملات نموذج التقييم لجميع الرحلات.",
    "orgName": "اسم المؤسسة",
    "orgNamePlaceholder": "مثال: البنك الوطني",
    "orgLogo": "الشعار",
    "orgLogoHint": "PNG أو JPG أو SVG. الحجم الأقصى الموصى به 2 ميغابايت.",
    "orgLogoReplace": "استبدال",
    "orgLogoUpload": "رفع شعار",
    "orgIndustry": "القطاع",
    "orgIndustryBanking": "البنوك",
    "orgIndustryTelecom": "الاتصالات",
    "orgIndustryGovt": "الحكومة",
    "orgIndustryAuto": "السيارات",
    "orgIndustryEntertain": "الترفيه",
    "orgIndustryServices": "الخدمات",
    "cjAlpha": "ألفا (α)",
    "cjBeta": "بيتا (β)",
    "cjBetaDerived": "مشتق: 1 − α",
    "cjMot": "مضاعف اللحظة الحاسمة",
    "cjNFloor": "الحد الأدنى لعدد الاستجابات",
    "cjFlagPct": "المئين للإشارة",
    "cjRollingDays": "أيام النافذة المتحركة",
    "saveSuccess": "تم حفظ الإعدادات.",
    "unsavedTitle": "لديك تغييرات غير محفوظة.",
    "unsavedBody": "هل أنت متأكد من المغادرة؟ ستُفقد تغييراتك.",
    "errAlpha": "يجب أن تكون ألفا بين 0.0 و 1.0.",
    "errMot": "يجب أن يكون مضاعف اللحظة الحاسمة بين 1.0 و 2.0.",
    "errNFloor": "يجب أن يكون الحد الأدنى لعدد الاستجابات 1 على الأقل.",
    "errFlagPct": "يجب أن يكون المئين للإشارة بين 1 و 49.",
    "errRollingDays": "يجب أن تكون النافذة المتحركة 7 أيام على الأقل.",
    "errNameRequired": "اسم المؤسسة مطلوب.",
    "logoSizeWarning": "الشعار يتجاوز 2 ميغابايت. يُنصح باستخدام ملف أصغر حجماً.",
    "tooltipAlpha": "وزن مزج أهمية العميل. ألفا هو الوزن المُعطى لأهمية العميل مقابل أهمية العمل عند حساب وزن نقطة التفاعل. كلما زاد ألفا كانت أهمية العميل أكبر، وكلما انخفض كانت أهمية العمل أكبر. يُحدَّد بيتا تلقائياً بـ (1 − ألفا). النطاق: 0.0–1.0، الافتراضي 0.5.",
    "tooltipMot": "تضخيم الوزن لنقاط التفاعل المصنّفة كـ 'لحظة حاسمة'. يُضرب وزن نقطة اللحظة الحاسمة بهذه القيمة؛ النقاط الأخرى غير متأثرة. النطاق: 1.0–2.0، الافتراضي 1.5.",
    "tooltipNFloor": "الحد الأدنى لعدد الاستجابات المطلوبة قبل احتساب نقطة التفاعل في التقييم. دون هذا الحد تُستبعد النقطة وتُعرض كـ '—'. النطاق: ≥ 1، الافتراضي 500.",
    "tooltipFlagPct": "يحدد مدى صرامة الإشارة لنقاط التفاعل ذات العينة المنخفضة. النطاق: 1–49، الافتراضي 25.",
    "tooltipRollingDays": "نافذة الوقت لحساب عتبات الثقة التكيفية وفترة البدء البارد للرحلات الجديدة. النطاق: ≥ 7، الافتراضي 30."
  }
}
```

- [ ] **Step 3: Verify JSON is valid**

```bash
node -e "require('./src/i18n/locales/en.json'); console.log('en.json OK')"
node -e "require('./src/i18n/locales/ar.json'); console.log('ar.json OK')"
```

Expected output:
```
en.json OK
ar.json OK
```

- [ ] **Step 4: Commit**

```bash
git add src/i18n/locales/en.json src/i18n/locales/ar.json
git commit -m "feat(settings): add settings i18n keys (EN + AR)"
```

---

## Task 4: Settings Landing Page

**Files:**
- Create: `src/pages/SettingsPage.tsx`

- [ ] **Step 1: Create `src/pages/SettingsPage.tsx`**

```tsx
// src/pages/SettingsPage.tsx
import { useTranslation } from "react-i18next"
import { useNavigate } from "react-router"
import { useDirection } from "@/hooks/use-direction"
import { Building2, SlidersHorizontal, ChevronLeft, ChevronRight } from "lucide-react"
import { Card } from "@/components/ui/card"
import type { LucideIcon } from "lucide-react"

interface Section {
  titleKey: string
  descKey: string
  icon: LucideIcon
  href: string
}

const SECTIONS: Section[] = [
  {
    titleKey: "settings.orgTitle",
    descKey: "settings.orgDesc",
    icon: Building2,
    href: "/settings/organization",
  },
  {
    titleKey: "settings.cjTitle",
    descKey: "settings.cjDesc",
    icon: SlidersHorizontal,
    href: "/settings/customer-journey",
  },
]

export default function SettingsPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isRtl } = useDirection()
  const Chevron = isRtl ? ChevronLeft : ChevronRight

  return (
    <div className="space-y-5 py-5 px-8">
      <div>
        <h1 className="text-2xl font-heading font-bold">{t("settings.title")}</h1>
        <p className="text-sm text-muted-foreground mt-1">{t("settings.subtitle")}</p>
      </div>

      <div className="space-y-2">
        <p className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
          {t("settings.sectionGroup")}
        </p>
        <Card className="overflow-hidden divide-y divide-border p-0">
          {SECTIONS.map((section) => (
            <button
              key={section.href}
              type="button"
              onClick={() => navigate(section.href)}
              className="w-full flex items-center gap-4 px-6 py-4 hover:bg-muted/50 transition-colors text-start cursor-pointer"
            >
              <div className="flex size-10 items-center justify-center rounded-md bg-primary/10 text-primary shrink-0">
                <section.icon className="size-5" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold">{t(section.titleKey)}</p>
                <p className="text-xs text-muted-foreground mt-0.5">{t(section.descKey)}</p>
              </div>
              <Chevron className="size-5 text-muted-foreground shrink-0" />
            </button>
          ))}
        </Card>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Commit**

```bash
git add src/pages/SettingsPage.tsx
git commit -m "feat(settings): add Settings landing page"
```

---

## Task 5: Wire Routes, Provider, and Sidebar

**Files:**
- Modify: `src/App.tsx`
- Modify: `src/components/layout/app-sidebar.tsx`

- [ ] **Step 1: Update `src/App.tsx`**

Add 3 import lines after the existing `KpiConfigPage` import (line 17):

```tsx
import SettingsPage from "./pages/SettingsPage"
import SettingsOrganizationPage from "./pages/SettingsOrganizationPage"
import SettingsCustomerJourneyPage from "./pages/SettingsCustomerJourneyPage"
import { SettingsProvider } from "./contexts/settings-context"
```

Add 3 routes after the `/kpi-management/:id` route (after line 58):

```tsx
<Route path="/settings" element={<LayoutRoute><SettingsPage /></LayoutRoute>} />
<Route path="/settings/organization" element={<LayoutRoute><SettingsOrganizationPage /></LayoutRoute>} />
<Route path="/settings/customer-journey" element={<LayoutRoute><SettingsCustomerJourneyPage /></LayoutRoute>} />
```

Wrap `<AppRoutes />` with `SettingsProvider` inside the `App` component. Change:

```tsx
export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <PersonaProvider>
          <AppRoutes />
        </PersonaProvider>
      </AuthProvider>
    </BrowserRouter>
  )
}
```

to:

```tsx
export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <PersonaProvider>
          <SettingsProvider>
            <AppRoutes />
          </SettingsProvider>
        </PersonaProvider>
      </AuthProvider>
    </BrowserRouter>
  )
}
```

- [ ] **Step 2: Update `src/components/layout/app-sidebar.tsx`**

Add `Settings` to the lucide-react import (on the existing import line around line 19):

```tsx
import {
  LayoutDashboard,
  ClipboardList,
  MessageSquareText,
  Send,
  Zap,
  BarChart3,
  RefreshCcw,
  Target,
  Map,
  Sparkles,
  Users,
  Gauge,
  Settings,
  type LucideIcon,
} from "lucide-react"
```

Add `settings` item to the `cx.navPlatform` group. Change:

```tsx
  {
    groupKey: "cx.navPlatform",
    items: [
      { key: "kpi_management", labelKey: "cx.navKpiManagement", icon: Gauge, href: "/kpi-management" },
    ],
  },
```

to:

```tsx
  {
    groupKey: "cx.navPlatform",
    items: [
      { key: "kpi_management", labelKey: "cx.navKpiManagement", icon: Gauge, href: "/kpi-management" },
      { key: "settings", labelKey: "cx.navSettings", icon: Settings, href: "/settings" },
    ],
  },
```

Add `"settings"` to `cx_manager` and `tenant_admin` in `ROLE_NAV_KEYS`. Change:

```tsx
const ROLE_NAV_KEYS: Record<string, string[]> = {
  cx_manager: ["dashboard", "surveys", "feedback", "distribution", "sending_rules", "analytics", "ai_insights", "closed_loop", "actions", "journey", "profiles", "kpi_management"],
  analyst: ["dashboard", "surveys", "feedback", "analytics", "ai_insights", "journey", "profiles"],
  tenant_admin: ["dashboard", "surveys", "feedback", "distribution", "sending_rules", "analytics", "ai_insights", "closed_loop", "actions", "journey", "profiles", "kpi_management"],
  executive: ["dashboard", "analytics", "journey", "actions"],
  frontline: ["dashboard", "closed_loop"],
}
```

to:

```tsx
const ROLE_NAV_KEYS: Record<string, string[]> = {
  cx_manager: ["dashboard", "surveys", "feedback", "distribution", "sending_rules", "analytics", "ai_insights", "closed_loop", "actions", "journey", "profiles", "kpi_management", "settings"],
  analyst: ["dashboard", "surveys", "feedback", "analytics", "ai_insights", "journey", "profiles"],
  tenant_admin: ["dashboard", "surveys", "feedback", "distribution", "sending_rules", "analytics", "ai_insights", "closed_loop", "actions", "journey", "profiles", "kpi_management", "settings"],
  executive: ["dashboard", "analytics", "journey", "actions"],
  frontline: ["dashboard", "closed_loop"],
}
```

- [ ] **Step 3: Verify TypeScript compiles**

```bash
npx tsc --noEmit 2>&1 | grep -E "settings|Settings|SettingsPage|SettingsOrg|SettingsCJ"
```

Expected: no output (no errors in the settings files).

- [ ] **Step 4: Commit**

```bash
git add src/App.tsx src/components/layout/app-sidebar.tsx
git commit -m "feat(settings): wire routes, SettingsProvider, and sidebar nav"
```

---

## Task 6: Organisation Section Page

**Files:**
- Create: `src/pages/SettingsOrganizationPage.tsx`

- [ ] **Step 1: Create `src/pages/SettingsOrganizationPage.tsx`**

```tsx
// src/pages/SettingsOrganizationPage.tsx
import { useState, useEffect, useRef } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { ArrowRight, Upload } from "lucide-react"
import { useSettings } from "@/contexts/settings-context"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"
import {
  Card, CardContent, CardHeader, CardTitle,
} from "@/components/ui/card"
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { cn } from "@/lib/utils"
import type { Industry } from "@/types/settings"

const INDUSTRIES: Industry[] = [
  "Banking", "Telecommunications", "Government",
  "Automotive", "Entertainment", "Services",
]

const INDUSTRY_LABEL_KEY: Record<Industry, string> = {
  Banking:          "settings.orgIndustryBanking",
  Telecommunications: "settings.orgIndustryTelecom",
  Government:       "settings.orgIndustryGovt",
  Automotive:       "settings.orgIndustryAuto",
  Entertainment:    "settings.orgIndustryEntertain",
  Services:         "settings.orgIndustryServices",
}

export default function SettingsOrganizationPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { orgConfig, saveOrg } = useSettings()
  const fileRef = useRef<HTMLInputElement>(null)

  const [name, setName] = useState(orgConfig.name)
  const [logoUrl, setLogoUrl] = useState<string | null>(orgConfig.logoUrl)
  const [logoWarning, setLogoWarning] = useState(false)
  const [industry, setIndustry] = useState<Industry>(orgConfig.industry)
  const [isDirty, setIsDirty] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [showLeaveModal, setShowLeaveModal] = useState(false)

  // Re-sync when context is updated externally
  useEffect(() => {
    setName(orgConfig.name)
    setLogoUrl(orgConfig.logoUrl)
    setIndustry(orgConfig.industry)
    setIsDirty(false)
  }, [orgConfig])

  function markDirty() { setIsDirty(true) }

  function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setLogoWarning(file.size > 2 * 1024 * 1024)
    setLogoUrl(URL.createObjectURL(file))
    markDirty()
  }

  function validate() {
    const e: Record<string, string> = {}
    if (!name.trim()) e.name = t("settings.errNameRequired")
    setErrors(e)
    return Object.keys(e).length === 0
  }

  function handleSave() {
    if (!validate()) return
    saveOrg({ name: name.trim(), logoUrl, industry })
    setIsDirty(false)
    navigate("/settings")
  }

  function handleBack() {
    if (isDirty) { setShowLeaveModal(true) } else { navigate("/settings") }
  }

  return (
    <div className="space-y-5 py-5 px-8">
      {/* Back + Title */}
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" aria-label={t("common.back")} onClick={handleBack}>
          <ArrowRight className="size-5" />
        </Button>
        <div>
          <h1 className="text-2xl font-heading font-bold">{t("settings.orgTitle")}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">{t("settings.orgDesc")}</p>
        </div>
      </div>

      {/* Two-column grid — form left, live preview right */}
      <div className="grid grid-cols-[55fr_45fr] gap-6 items-start">

        {/* Form card */}
        <Card>
          <CardContent className="space-y-5 pt-5">

            {/* Name */}
            <div className="space-y-1.5">
              <Label htmlFor="orgName">
                {t("settings.orgName")}<span className="text-destructive ms-0.5">*</span>
              </Label>
              <Input
                id="orgName"
                value={name}
                maxLength={150}
                placeholder={t("settings.orgNamePlaceholder")}
                onChange={(e) => { setName(e.target.value); markDirty() }}
                className={cn(errors.name && "border-destructive")}
              />
              {errors.name && (
                <p className="text-xs text-destructive" role="alert">{errors.name}</p>
              )}
            </div>

            {/* Logo */}
            <div className="space-y-1.5">
              <Label>{t("settings.orgLogo")}</Label>
              <div className="flex items-center gap-3">
                {logoUrl ? (
                  <img
                    src={logoUrl}
                    alt="Tenant logo"
                    className="h-12 w-12 rounded-md object-contain border border-border bg-muted/30"
                  />
                ) : (
                  <div className="flex h-12 w-12 items-center justify-center rounded-md border border-dashed border-border bg-muted/30 text-muted-foreground">
                    <Upload className="size-4" />
                  </div>
                )}
                <div className="space-y-1">
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    onClick={() => fileRef.current?.click()}
                  >
                    {logoUrl ? t("settings.orgLogoReplace") : t("settings.orgLogoUpload")}
                  </Button>
                  <p className="text-xs text-muted-foreground">{t("settings.orgLogoHint")}</p>
                </div>
              </div>
              {/* Hidden file input — triggered by the button above */}
              <input
                ref={fileRef}
                type="file"
                accept=".png,.jpg,.jpeg,.svg"
                className="hidden"
                onChange={handleLogoChange}
              />
              {logoWarning && (
                <p className="text-xs text-d3">{t("settings.logoSizeWarning")}</p>
              )}
            </div>

            {/* Industry */}
            <div className="space-y-1.5">
              <Label>{t("settings.orgIndustry")}</Label>
              <Select
                value={industry}
                onValueChange={(v) => { setIndustry(v as Industry); markDirty() }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {INDUSTRIES.map((ind) => (
                    <SelectItem key={ind} value={ind}>
                      {t(INDUSTRY_LABEL_KEY[ind])}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Actions */}
            <div className="flex items-center justify-end gap-3 pt-2 border-t border-border">
              <Button variant="ghost" onClick={handleBack}>{t("common.cancel")}</Button>
              <Button
                disabled={!isDirty}
                onClick={handleSave}
                className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              >
                {t("common.save")}
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Live preview */}
        <div className="sticky top-20">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm">{t("common.preview")}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col items-center gap-4 py-6">
              {logoUrl ? (
                <img
                  src={logoUrl}
                  alt="Tenant logo preview"
                  className="h-20 w-20 rounded-lg object-contain border border-border bg-muted/20"
                />
              ) : (
                <div className="flex h-20 w-20 items-center justify-center rounded-lg border-2 border-dashed border-border text-muted-foreground bg-muted/20">
                  <Upload className="size-6" />
                </div>
              )}
              <div className="text-center">
                <p className="font-heading font-bold text-lg">
                  {name.trim() || t("settings.orgNamePlaceholder")}
                </p>
                <p className="text-xs text-muted-foreground mt-0.5">
                  {t(INDUSTRY_LABEL_KEY[industry])}
                </p>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Unsaved-changes guard */}
      <AlertDialog open={showLeaveModal} onOpenChange={setShowLeaveModal}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("settings.unsavedTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("settings.unsavedBody")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.no")}</AlertDialogCancel>
            <AlertDialogAction onClick={() => navigate("/settings")}>
              {t("common.yes")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
```

- [ ] **Step 2: Commit**

```bash
git add src/pages/SettingsOrganizationPage.tsx
git commit -m "feat(settings): add Organisation section page"
```

---

## Task 7: Customer Journey Section Page

**Files:**
- Create: `src/pages/SettingsCustomerJourneyPage.tsx`

- [ ] **Step 1: Create `src/pages/SettingsCustomerJourneyPage.tsx`**

```tsx
// src/pages/SettingsCustomerJourneyPage.tsx
import { useState, useEffect } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { ArrowRight, Info } from "lucide-react"
import { useSettings } from "@/contexts/settings-context"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Slider } from "@/components/ui/slider"
import { Card, CardContent } from "@/components/ui/card"
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import {
  Tooltip, TooltipContent, TooltipTrigger,
} from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"
import type { ScoringConfig } from "@/types/settings"

export default function SettingsCustomerJourneyPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { scoringConfig, saveScoring } = useSettings()

  const [alpha, setAlpha] = useState(scoringConfig.alpha)
  const [motMultiplier, setMotMultiplier] = useState(scoringConfig.motMultiplier)
  const [nFloor, setNFloor] = useState(scoringConfig.nFloor)
  const [flagPercentile, setFlagPercentile] = useState(scoringConfig.flagPercentile)
  const [rollingWindowDays, setRollingWindowDays] = useState(scoringConfig.rollingWindowDays)
  const [isDirty, setIsDirty] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [showLeaveModal, setShowLeaveModal] = useState(false)

  // β is always derived — never stored
  const beta = parseFloat((1 - alpha).toFixed(3))

  // Re-sync when context updated externally
  useEffect(() => {
    setAlpha(scoringConfig.alpha)
    setMotMultiplier(scoringConfig.motMultiplier)
    setNFloor(scoringConfig.nFloor)
    setFlagPercentile(scoringConfig.flagPercentile)
    setRollingWindowDays(scoringConfig.rollingWindowDays)
    setIsDirty(false)
  }, [scoringConfig])

  function markDirty() { setIsDirty(true) }

  function validate() {
    const e: Record<string, string> = {}
    if (alpha < 0 || alpha > 1) e.alpha = t("settings.errAlpha")
    if (motMultiplier < 1.0 || motMultiplier > 2.0) e.motMultiplier = t("settings.errMot")
    if (!Number.isInteger(nFloor) || nFloor < 1) e.nFloor = t("settings.errNFloor")
    if (!Number.isInteger(flagPercentile) || flagPercentile < 1 || flagPercentile > 49) {
      e.flagPercentile = t("settings.errFlagPct")
    }
    if (!Number.isInteger(rollingWindowDays) || rollingWindowDays < 7) {
      e.rollingWindowDays = t("settings.errRollingDays")
    }
    setErrors(e)
    return Object.keys(e).length === 0
  }

  function handleSave() {
    if (!validate()) return
    const updated: ScoringConfig = {
      alpha, motMultiplier, nFloor, flagPercentile, rollingWindowDays,
    }
    saveScoring(updated)
    setIsDirty(false)
    navigate("/settings")
  }

  function handleBack() {
    if (isDirty) { setShowLeaveModal(true) } else { navigate("/settings") }
  }

  // Base UI Slider onValueChange receives (value: number | readonly number[], eventDetails)
  function extractSliderValue(v: number | readonly number[]): number {
    return Array.isArray(v) ? (v as readonly number[])[0] : (v as number)
  }

  return (
    <div className="space-y-5 py-5 px-8">
      {/* Back + Title */}
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" aria-label={t("common.back")} onClick={handleBack}>
          <ArrowRight className="size-5" />
        </Button>
        <div>
          <h1 className="text-2xl font-heading font-bold">{t("settings.cjTitle")}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">{t("settings.cjDesc")}</p>
        </div>
      </div>

      <Card className="max-w-2xl">
        <CardContent className="space-y-8 pt-6">

          {/* ── Alpha (α) ──────────────────────────────── */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-1.5">
                <Label className="text-sm font-semibold">{t("settings.cjAlpha")}</Label>
                <Tooltip>
                  <TooltipTrigger
                    className="inline-flex items-center text-muted-foreground cursor-help"
                    aria-label={t("settings.cjAlpha")}
                  >
                    <Info className="size-3.5" />
                  </TooltipTrigger>
                  <TooltipContent className="max-w-xs text-xs leading-relaxed">
                    {t("settings.tooltipAlpha")}
                  </TooltipContent>
                </Tooltip>
              </div>
              <span className="text-sm font-mono font-bold tabular-nums text-primary">
                {alpha.toFixed(3)}
              </span>
            </div>
            <Slider
              value={[alpha]}
              min={0}
              max={1}
              step={0.001}
              onValueChange={(v) => {
                setAlpha(parseFloat(extractSliderValue(v).toFixed(3)))
                markDirty()
              }}
            />
            {/* β read-only display */}
            <div className="flex items-center gap-3 pt-1">
              <div className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-muted/40 border border-border">
                <span className="text-xs text-muted-foreground">{t("settings.cjBeta")}</span>
                <span className="text-sm font-mono font-bold tabular-nums">
                  {beta.toFixed(3)}
                </span>
              </div>
              <span className="text-xs text-muted-foreground">{t("settings.cjBetaDerived")}</span>
            </div>
            {errors.alpha && (
              <p className="text-xs text-destructive" role="alert">{errors.alpha}</p>
            )}
          </div>

          {/* ── MOT Multiplier ─────────────────────────── */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-1.5">
                <Label className="text-sm font-semibold">{t("settings.cjMot")}</Label>
                <Tooltip>
                  <TooltipTrigger
                    className="inline-flex items-center text-muted-foreground cursor-help"
                    aria-label={t("settings.cjMot")}
                  >
                    <Info className="size-3.5" />
                  </TooltipTrigger>
                  <TooltipContent className="max-w-xs text-xs leading-relaxed">
                    {t("settings.tooltipMot")}
                  </TooltipContent>
                </Tooltip>
              </div>
              <Input
                type="number"
                min={1.0}
                max={2.0}
                step={0.1}
                value={motMultiplier}
                onChange={(e) => {
                  const v = parseFloat(e.target.value)
                  if (!isNaN(v)) { setMotMultiplier(parseFloat(v.toFixed(1))); markDirty() }
                }}
                className={cn(
                  "w-20 text-center tabular-nums h-8",
                  errors.motMultiplier && "border-destructive",
                )}
              />
            </div>
            <Slider
              value={[motMultiplier]}
              min={1}
              max={2}
              step={0.1}
              onValueChange={(v) => {
                setMotMultiplier(parseFloat(extractSliderValue(v).toFixed(1)))
                markDirty()
              }}
            />
            {errors.motMultiplier && (
              <p className="text-xs text-destructive" role="alert">{errors.motMultiplier}</p>
            )}
          </div>

          {/* ── Responses Count Floor ──────────────────── */}
          <div className="space-y-1.5">
            <div className="flex items-center gap-1.5">
              <Label htmlFor="nFloor" className="text-sm font-semibold">
                {t("settings.cjNFloor")}
              </Label>
              <Tooltip>
                <TooltipTrigger
                  className="inline-flex items-center text-muted-foreground cursor-help"
                  aria-label={t("settings.cjNFloor")}
                >
                  <Info className="size-3.5" />
                </TooltipTrigger>
                <TooltipContent className="max-w-xs text-xs leading-relaxed">
                  {t("settings.tooltipNFloor")}
                </TooltipContent>
              </Tooltip>
            </div>
            <Input
              id="nFloor"
              type="number"
              min={1}
              value={nFloor}
              onChange={(e) => {
                const v = parseInt(e.target.value, 10)
                if (!isNaN(v)) { setNFloor(v); markDirty() }
              }}
              className={cn("tabular-nums", errors.nFloor && "border-destructive")}
            />
            {errors.nFloor && (
              <p className="text-xs text-destructive" role="alert">{errors.nFloor}</p>
            )}
          </div>

          {/* ── Flag Percentile ────────────────────────── */}
          <div className="space-y-1.5">
            <div className="flex items-center gap-1.5">
              <Label htmlFor="flagPct" className="text-sm font-semibold">
                {t("settings.cjFlagPct")}
              </Label>
              <Tooltip>
                <TooltipTrigger
                  className="inline-flex items-center text-muted-foreground cursor-help"
                  aria-label={t("settings.cjFlagPct")}
                >
                  <Info className="size-3.5" />
                </TooltipTrigger>
                <TooltipContent className="max-w-xs text-xs leading-relaxed">
                  {t("settings.tooltipFlagPct")}
                </TooltipContent>
              </Tooltip>
            </div>
            <Input
              id="flagPct"
              type="number"
              min={1}
              max={49}
              value={flagPercentile}
              onChange={(e) => {
                const v = parseInt(e.target.value, 10)
                if (!isNaN(v)) { setFlagPercentile(v); markDirty() }
              }}
              className={cn("tabular-nums", errors.flagPercentile && "border-destructive")}
            />
            {errors.flagPercentile && (
              <p className="text-xs text-destructive" role="alert">{errors.flagPercentile}</p>
            )}
          </div>

          {/* ── Rolling Window Days ────────────────────── */}
          <div className="space-y-1.5">
            <div className="flex items-center gap-1.5">
              <Label htmlFor="rollingDays" className="text-sm font-semibold">
                {t("settings.cjRollingDays")}
              </Label>
              <Tooltip>
                <TooltipTrigger
                  className="inline-flex items-center text-muted-foreground cursor-help"
                  aria-label={t("settings.cjRollingDays")}
                >
                  <Info className="size-3.5" />
                </TooltipTrigger>
                <TooltipContent className="max-w-xs text-xs leading-relaxed">
                  {t("settings.tooltipRollingDays")}
                </TooltipContent>
              </Tooltip>
            </div>
            <Input
              id="rollingDays"
              type="number"
              min={7}
              value={rollingWindowDays}
              onChange={(e) => {
                const v = parseInt(e.target.value, 10)
                if (!isNaN(v)) { setRollingWindowDays(v); markDirty() }
              }}
              className={cn("tabular-nums", errors.rollingWindowDays && "border-destructive")}
            />
            {errors.rollingWindowDays && (
              <p className="text-xs text-destructive" role="alert">{errors.rollingWindowDays}</p>
            )}
          </div>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3 pt-2 border-t border-border">
            <Button variant="ghost" onClick={handleBack}>{t("common.cancel")}</Button>
            <Button
              disabled={!isDirty}
              onClick={handleSave}
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            >
              {t("common.save")}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Unsaved-changes guard */}
      <AlertDialog open={showLeaveModal} onOpenChange={setShowLeaveModal}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("settings.unsavedTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("settings.unsavedBody")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.no")}</AlertDialogCancel>
            <AlertDialogAction onClick={() => navigate("/settings")}>
              {t("common.yes")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
```

- [ ] **Step 2: Final TypeScript check**

```bash
npx tsc --noEmit 2>&1 | grep -v "JourneyStatsPage\|SurveysPage"
```

Expected: no output from settings files (the two pre-existing errors in JourneyStatsPage and SurveysPage are unrelated to this feature).

- [ ] **Step 3: Commit**

```bash
git add src/pages/SettingsCustomerJourneyPage.tsx
git commit -m "feat(settings): add Customer Journey scoring config page"
```

---

## Self-Review Checklist

| Spec requirement | Covered by |
|-----------------|-----------|
| FR-S1: Landing page with section list | Task 4 — SettingsPage |
| FR-S2: Section opens on separate page, back returns | Tasks 4 & 5 — navigate + ArrowRight back |
| FR-S3: Section page title = section name | Tasks 6 & 7 — h1 uses `settings.orgTitle` / `settings.cjTitle` |
| FR-S4: Unsaved-changes guard | Tasks 6 & 7 — `isDirty` + AlertDialog |
| FR-S5: Tenant-scoped (SettingsContext) | Task 2 |
| FR-S6: Sidebar nav + brand layout | Task 5 |
| FR-S7–11: Org fields (Name/Logo/Industry) + Save | Task 6 |
| FR-S13: CJ exposes 5 params | Task 7 |
| FR-S14: `?` tooltip per param | Task 7 — Info icon + Tooltip |
| FR-S15: α slider + read-only β | Task 7 |
| FR-S16: MOT slider + input, step 0.1 | Task 7 |
| FR-S17: n_floor integer ≥ 1, default 500 | Tasks 1 & 7 |
| FR-S18: Flag percentile 1–49 | Tasks 1 & 7 |
| FR-S19: Rolling window ≥ 7 | Tasks 1 & 7 |
| FR-S20: Save validates all constraints | Task 7 — `validate()` |
| FR-S22: β never independently editable | Task 7 — read-only display only |
| NFR-S1: Tooltips keyboard-accessible | Task 7 — `TooltipTrigger` is focusable |
| NFR-S2: EN + AR i18n | Task 3 — both locales |
| NFR-S4: α/β synchronised live | Task 7 — `beta = 1 - alpha` derived on every render |
