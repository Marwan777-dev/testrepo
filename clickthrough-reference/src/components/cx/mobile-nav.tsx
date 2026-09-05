import { useTranslation } from "react-i18next"
import { useNavigate, useLocation } from "react-router"
import { cn } from "@/lib/utils"
import { useIsMobile } from "@/hooks/use-mobile"
import { usePersona } from "@/contexts/persona-context"
import {
  LayoutDashboard,
  ClipboardList,
  BarChart3,
  RefreshCcw,
  Target,
  Map,
  Plus,
  type LucideIcon,
} from "lucide-react"

interface NavItem {
  id: string
  labelKey: string
  icon: LucideIcon
  href: string
}

function getNavItems(personaId: string): NavItem[] {
  switch (personaId) {
    case "frontline":
      return [
        { id: "home", labelKey: "nav.home", icon: LayoutDashboard, href: "/dashboard" },
        { id: "cases", labelKey: "cx.openCases", icon: RefreshCcw, href: "/closed-loop" },
      ]
    case "executive":
      return [
        { id: "home", labelKey: "nav.home", icon: LayoutDashboard, href: "/dashboard" },
        { id: "analytics", labelKey: "nav.analytics", icon: BarChart3, href: "/analytics" },
        { id: "journey", labelKey: "cx.navJourney", icon: Map, href: "/journey" },
        { id: "actions", labelKey: "common.actions", icon: Target, href: "/actions" },
      ]
    default:
      return [
        { id: "home", labelKey: "nav.home", icon: LayoutDashboard, href: "/dashboard" },
        { id: "surveys", labelKey: "nav.surveys", icon: ClipboardList, href: "/surveys" },
        { id: "analytics", labelKey: "nav.analytics", icon: BarChart3, href: "/analytics" },
        { id: "cases", labelKey: "cx.openCases", icon: RefreshCcw, href: "/closed-loop" },
      ]
  }
}

export function MobileNav() {
  const { t } = useTranslation()
  const isMobile = useIsMobile()
  const { persona } = usePersona()
  const navigate = useNavigate()
  const location = useLocation()

  if (!isMobile) return null

  const navItems = getNavItems(persona.id)

  return (
    <nav className="fixed bottom-0 inset-x-0 z-50 bg-background border-t border-border">
      <div className="flex py-2">
        {navItems.map((item) => {
          const isActive =
            location.pathname === item.href ||
            (item.href === "/dashboard" && location.pathname === "/")
          const Icon = item.icon
          return (
            <button
              key={item.id}
              onClick={() => navigate(item.href)}
              className={cn(
                "flex-1 flex flex-col items-center gap-1 py-1 motion-safe:transition-colors motion-safe:duration-150",
                isActive ? "text-secondary" : "text-muted-foreground",
              )}
              aria-label={t(item.labelKey)}
              aria-current={isActive ? "page" : undefined}
            >
              <Icon className="size-5" />
              <span className="text-[10px] font-medium">{t(item.labelKey)}</span>
              {isActive && <span className="size-1 rounded-full bg-secondary" />}
            </button>
          )
        })}
      </div>
    </nav>
  )
}

export function MobileFAB() {
  const isMobile = useIsMobile()
  const { t } = useTranslation()

  if (!isMobile) return null

  return (
    <button
      className="fixed bottom-16 end-4 z-40 rounded-full size-14 bg-gradient-to-br from-nb-cyan to-nb-cyan-700 shadow-lg flex items-center justify-center hover:shadow-xl hover:scale-105 motion-safe:transition-all motion-safe:duration-150 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
      aria-label={t("common.create")}
    >
      <Plus className="size-6 text-white" />
    </button>
  )
}
