// Unified Platform Settings page (/settings). Merges the former standalone /settings/organization
// and /settings/customer-journey section pages into a single screen: a left sub-nav (scroll-spy +
// jump links), a search filter, and the Organization + Customer Journey sections stacked in the main
// column. Each section owns its own form + Save/Cancel; this page only wires the chrome, the search
// filter, and a navigation guard that warns before leaving with unsaved edits in either section.
// Notifications / Branding / Localization remain v1 placeholders and are not shown here yet.

import { useEffect, useMemo, useRef, useState } from "react"
import { useTranslation } from "react-i18next"
import { Building2, Search, SlidersHorizontal, type LucideIcon } from "lucide-react"

import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"
import { CustomerJourneySection } from "@/features/settings/components/CustomerJourneySection"
import { OrganizationSection } from "@/features/settings/components/OrganizationSection"

type SectionId = "organization" | "customer-journey"

interface SectionMeta {
  id: SectionId
  anchor: string
  icon: LucideIcon
  navKey: string
  /** Extra terms (beyond the nav label) the search box should match against. */
  keywordKeys: string[]
}

const SECTIONS: SectionMeta[] = [
  {
    id: "organization",
    anchor: "settings-organization",
    icon: Building2,
    navKey: "settings.page.nav.organization",
    keywordKeys: ["settings.organization.nameLabel", "settings.organization.industryLabel", "settings.organization.logoLabel"],
  },
  {
    id: "customer-journey",
    anchor: "settings-customer-journey",
    icon: SlidersHorizontal,
    navKey: "settings.page.nav.customerJourney",
    keywordKeys: [
      "settings.customerJourney.alphaLabel",
      "settings.customerJourney.motLabel",
      "settings.customerJourney.nFloorLabel",
      "settings.customerJourney.flagPercentileLabel",
      "settings.customerJourney.rollingWindowLabel",
    ],
  },
]

export default function SettingsPage() {
  const { t } = useTranslation()

  const [query, setQuery] = useState("")
  const [activeId, setActiveId] = useState<SectionId>("organization")
  const [orgDirty, setOrgDirty] = useState(false)
  const [journeyDirty, setJourneyDirty] = useState(false)
  const sectionRefs = useRef<Partial<Record<SectionId, HTMLDivElement | null>>>({})

  const anyDirty = orgDirty || journeyDirty

  // Browser tab close / refresh guard. (In-app navigation can't use react-router's useBlocker here —
  // the app mounts a <BrowserRouter>, not a data router — so per-section Cancel handles discarding.)
  useEffect(() => {
    if (!anyDirty) return
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      e.returnValue = ""
    }
    window.addEventListener("beforeunload", handler)
    return () => window.removeEventListener("beforeunload", handler)
  }, [anyDirty])

  const normalizedQuery = query.trim().toLowerCase()

  const visibleSections = useMemo(() => {
    if (!normalizedQuery) return SECTIONS
    return SECTIONS.filter((s) => {
      const haystack = [s.navKey, ...s.keywordKeys].map((k) => t(k).toLowerCase()).join(" ")
      return haystack.includes(normalizedQuery)
    })
  }, [normalizedQuery, t])

  // Scroll-spy: highlight the section nearest the top of the viewport.
  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)
        if (visible[0]) {
          const id = (visible[0].target as HTMLElement).dataset.sectionId as SectionId | undefined
          if (id) setActiveId(id)
        }
      },
      { rootMargin: "-20% 0px -70% 0px", threshold: 0 },
    )
    for (const s of visibleSections) {
      const el = sectionRefs.current[s.id]
      if (el) observer.observe(el)
    }
    return () => observer.disconnect()
  }, [visibleSections])

  const jumpTo = (id: SectionId) => {
    sectionRefs.current[id]?.scrollIntoView({ behavior: "smooth", block: "start" })
    setActiveId(id)
  }

  return (
    <div className="space-y-5 py-5">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-heading font-bold">{t("settings.page.title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("settings.page.subtitle")}</p>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[220px_1fr]">
        {/* Sub-nav */}
        <nav
          aria-label={t("settings.page.title")}
          className="sticky top-0 z-10 self-start bg-background py-2 lg:top-4"
        >
          <ul className="flex flex-row gap-2 overflow-x-auto lg:flex-col">
            {SECTIONS.map((s) => {
              const Icon = s.icon
              const hidden = !visibleSections.some((v) => v.id === s.id)
              const active = activeId === s.id
              return (
                <li key={s.id} className={cn(hidden && "hidden")}>
                  <button
                    type="button"
                    onClick={() => jumpTo(s.id)}
                    aria-current={active ? "true" : undefined}
                    className={cn(
                      "flex w-full items-center gap-2.5 rounded-md px-3 py-2 text-start text-sm transition-colors",
                      active
                        ? "bg-nb-cyan-100 font-semibold text-nb-cyan-800 dark:bg-nb-cyan-900/40 dark:text-nb-cyan-200"
                        : "text-foreground hover:bg-accent",
                    )}
                  >
                    <Icon className="size-4 shrink-0" aria-hidden />
                    <span className="truncate">{t(s.navKey)}</span>
                  </button>
                </li>
              )
            })}
          </ul>
        </nav>

        {/* Content */}
        <div className="space-y-6">
          {/* Search */}
          <div className="relative">
            <Search
              className="pointer-events-none absolute top-1/2 size-4 -translate-y-1/2 text-muted-foreground start-3"
              aria-hidden
            />
            <Input
              type="search"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder={t("settings.page.searchPlaceholder")}
              aria-label={t("settings.page.searchPlaceholder")}
              className="ps-9"
            />
          </div>

          {visibleSections.length === 0 ? (
            <div className="rounded-md border border-border bg-card px-4 py-10 text-center text-sm text-muted-foreground">
              {t("settings.page.noResults")}
            </div>
          ) : (
            visibleSections.map((s, index) => (
              <div key={s.id}>
                {index > 0 && <div className="mb-6 border-t border-border" aria-hidden />}
                <div
                  id={s.anchor}
                  data-section-id={s.id}
                  ref={(el) => {
                    sectionRefs.current[s.id] = el
                  }}
                  className="scroll-mt-20"
                >
                  {s.id === "organization" ? (
                    <OrganizationSection onDirtyChange={setOrgDirty} />
                  ) : (
                    <CustomerJourneySection onDirtyChange={setJourneyDirty} />
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  )
}
