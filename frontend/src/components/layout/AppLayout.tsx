// Authenticated app shell: a navy shadcn Sidebar (RTL-aware side) + content area.
// Nav entries are permission-gated — the User Management and Persona Baselines links
// appear only when the session holds the UserManagement module. Scroll resets to the
// top on every route change (inner scroll containers don't auto-reset).

import { useEffect, useRef, useState } from "react"
import { Link, Outlet, useLocation } from "react-router"
import { useTranslation } from "react-i18next"
import { useTheme } from "next-themes"
import {
  ArrowLeftRight,
  Building2,
  ClipboardList,
  Clock,
  Gauge,
  Home,
  Languages,
  LogOut,
  Moon,
  Plug,
  Route as RouteIcon,
  ScrollText,
  Settings,
  ShieldOff,
  SlidersHorizontal,
  Sun,
  Table2,
  Users,
  UsersRound,
} from "lucide-react"

import { Button } from "@/components/ui/button"

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarRail,
  SidebarTrigger,
} from "@/components/ui/sidebar"
import { useSession } from "@/features/auth/hooks/useSession"
import { useDirection } from "@/hooks/use-direction"

export function AppLayout() {
  const { t } = useTranslation()
  const { isRtl, lang, toggleLang } = useDirection()
  const { resolvedTheme, setTheme } = useTheme()
  const { session, signOut } = useSession()
  const location = useLocation()
  const mainRef = useRef<HTMLDivElement>(null)

  // next-themes resolves the theme only after mount; guard so we don't flash the
  // wrong icon on first client render.
  const [mounted, setMounted] = useState(false)
  useEffect(() => setMounted(true), [])
  const isDark = resolvedTheme === "dark"

  const canViewUsers = (session?.permissionSnapshot.modules["UserManagement"]?.length ?? 0) > 0
  // Platform Settings → Organization (M-06, US-6 / FR-052): visible to a persona holding the
  // TenantConfiguration module (P-01 / P-07). The data layer enforces it server-side too.
  const canConfigureTenant =
    (session?.permissionSnapshot.modules["TenantConfiguration"]?.length ?? 0) > 0
  // Customer Journey authoring (M-16) is restricted to P-01 (CX Program Manager) and P-02
  // (CX Analyst) per the spec's persona RBAC; the data layer enforces it server-side.
  const canAuthorJourneys = session?.persona === "P-01" || session?.persona === "P-02"
  // CX Metrics & KPI Engine (M-06): the catalogue is visible to P-01, P-02, and P-06 per the
  // spec's persona RBAC; the data layer enforces it server-side.
  const canViewKpis =
    session?.persona === "P-01" || session?.persona === "P-02" || session?.persona === "P-06"
  // Survey & Form Builder (M-01): P-01 and P-03 author surveys/templates; P-02 and P-06 get
  // read-only access (Feature 004 persona matrix). The data layer enforces RBAC server-side.
  const canViewSurveys =
    session?.persona === "P-01" ||
    session?.persona === "P-02" ||
    session?.persona === "P-03" ||
    session?.persona === "P-06"
  // Integration Hub (M-13): both owning personas reach the module, with mirrored read-only
  // grants (BR-24) — P-07 (Tenant IT Admin) manages integrations while P-01 (CX Manager) manages
  // the data model, and each sees the other's screens read-only. Request logs are the ONE screen
  // with no cross-persona grant at all: P-07 only, per the Permissions Matrix.
  const canViewIntegrationHub = session?.persona === "P-01" || session?.persona === "P-07"
  const canViewRequestLogs = session?.persona === "P-07"
  // Whether the user can reach any permission-gated feature page. Home is always
  // available, so it doesn't count — when this is false the user has no permissions.
  const hasFeatureNav =
    canViewUsers ||
    canAuthorJourneys ||
    canViewKpis ||
    canConfigureTenant ||
    canViewSurveys ||
    canViewIntegrationHub ||
    canViewRequestLogs

  useEffect(() => {
    mainRef.current?.scrollTo(0, 0)
  }, [location.pathname])

  // h-svh bounds the shell to the viewport so the inner content div (overflow-auto) is the real
  // scroll container. Without it the wrapper's min-h-svh grows with content, the window scrolls
  // instead, and `position: sticky` inside the content div never engages (its scrollport never
  // moves) — which also broke the route-change scroll reset below.
  return (
    <SidebarProvider className="h-svh">
      <Sidebar side={isRtl ? "right" : "left"} collapsible="icon">
        <SidebarHeader>
          <div className="flex items-center gap-2 overflow-hidden px-2 py-1.5">
            <span className="font-heading text-lg font-bold whitespace-nowrap group-data-[collapsible=icon]:hidden">
              {t("common.appName")}
            </span>
          </div>
        </SidebarHeader>

        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel>{t("nav.overview")}</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                <SidebarMenuItem>
                  <SidebarMenuButton isActive={location.pathname === "/"} render={<Link to="/" />}>
                    <Home />
                    <span>{t("nav.home")}</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>

          {canAuthorJourneys && (
            <SidebarGroup>
              <SidebarGroupLabel>{t("nav.experience")}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/journeys")}
                      render={<Link to="/journeys" />}
                    >
                      <RouteIcon />
                      <span>{t("nav.journeys")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/personas")}
                      render={<Link to="/personas" />}
                    >
                      <UsersRound />
                      <span>{t("nav.personas")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          )}

          {/* Survey & Form Builder (M-01): its own "Voice of Customer" section — surveys and
              templates are the collection instruments, distinct from journey authoring above. */}
          {canViewSurveys && (
            <SidebarGroup>
              <SidebarGroupLabel>{t("nav.voc")}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      // Templates live as a Survey Library tab; /templates/:id/edit
                      // deep links still belong to this nav item.
                      isActive={
                        (location.pathname.startsWith("/surveys") &&
                          !location.pathname.startsWith("/surveys/post-expiry")) ||
                        location.pathname.startsWith("/templates")
                      }
                      render={<Link to="/surveys" />}
                    >
                      <ClipboardList />
                      <span>{t("nav.surveys")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/surveys/post-expiry")}
                      render={<Link to="/surveys/post-expiry" />}
                    >
                      <Clock />
                      <span>{t("nav.postExpiry")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          )}

          {/* CX Metrics & KPI Engine (M-06) gets its own distinct sidebar section, separate from
              the journey-authoring "Customer Experience" group above. */}
          {canViewKpis && (
            <SidebarGroup>
              <SidebarGroupLabel>{t("nav.kpiEngine")}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/kpi-management")}
                      render={<Link to="/kpi-management" />}
                    >
                      <Gauge />
                      <span>{t("nav.kpiManagement")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          )}

          {/* Integration Hub (M-13) — the inbound integration edge. Two adjacent groups: the
              runtime surface (integrations + their request logs) and the data model those
              integrations are validated against. */}
          {canViewIntegrationHub && (
            <SidebarGroup>
              <SidebarGroupLabel>{t("nav.integrationHub")}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/integration-hub/integrations")}
                      render={<Link to="/integration-hub/integrations" />}
                    >
                      <Plug />
                      <span>{t("nav.integrations")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                  {canViewRequestLogs && (
                    <SidebarMenuItem>
                      <SidebarMenuButton
                        isActive={location.pathname.startsWith("/integration-hub/logs")}
                        render={<Link to="/integration-hub/logs" />}
                      >
                        <ScrollText />
                        <span>{t("nav.requestLogs")}</span>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  )}
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          )}

          {canViewIntegrationHub && (
            <SidebarGroup>
              <SidebarGroupLabel>{t("nav.dataModel")}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/integration-hub/service-channels")}
                      render={<Link to="/integration-hub/service-channels" />}
                    >
                      <Building2 />
                      <span>{t("nav.serviceChannels")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/integration-hub/parameters")}
                      render={<Link to="/integration-hub/parameters" />}
                    >
                      <Table2 />
                      <span>{t("nav.parameters")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/integration-hub/mappings")}
                      render={<Link to="/integration-hub/mappings" />}
                    >
                      <ArrowLeftRight />
                      <span>{t("nav.parameterMappings")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          )}

          {!hasFeatureNav && (
            <SidebarGroup className="group-data-[collapsible=icon]:hidden">
              <SidebarGroupContent>
                <div className="flex flex-col items-center px-4 py-10 text-center">
                  <ShieldOff className="mb-3 size-8 text-sidebar-foreground/60" />
                  <p className="text-sm font-medium text-sidebar-foreground">
                    {t("nav.noPermissions")}
                  </p>
                  <p className="mt-1 text-sm leading-relaxed text-sidebar-foreground/70">
                    {t("nav.noPermissionsHint")}
                  </p>
                </div>
              </SidebarGroupContent>
            </SidebarGroup>
          )}

          {canViewUsers && (
            <SidebarGroup>
              <SidebarGroupLabel>{t("nav.platform")}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/users")}
                      render={<Link to="/users" />}
                    >
                      <Users />
                      <span>{t("nav.users")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={location.pathname.startsWith("/audit-log")}
                      render={<Link to="/audit-log" />}
                    >
                      <ScrollText />
                      <span>{t("nav.auditLog")}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          )}

          {(canViewUsers || canConfigureTenant) && (
            <SidebarGroup>
              <SidebarGroupLabel>{t("nav.settings")}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  {canConfigureTenant && (
                    <SidebarMenuItem>
                      <SidebarMenuButton
                        isActive={
                          location.pathname === "/settings" ||
                          location.pathname.startsWith("/settings/organization") ||
                          location.pathname.startsWith("/settings/customer-journey")
                        }
                        render={<Link to="/settings" />}
                      >
                        <Settings />
                        <span>{t("nav.settings")}</span>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  )}
                  {canViewUsers && (
                    <SidebarMenuItem>
                      <SidebarMenuButton
                        isActive={location.pathname.startsWith("/settings/persona-baselines")}
                        render={<Link to="/settings/persona-baselines" />}
                      >
                        <SlidersHorizontal />
                        <span>{t("nav.personaBaselines")}</span>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  )}
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          )}
        </SidebarContent>

        <SidebarFooter>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton onClick={() => void signOut()}>
                <LogOut />
                <span>{t("nav.logout")}</span>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarFooter>

        <SidebarRail />
      </Sidebar>

      <SidebarInset>
        <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b border-border bg-background/95 px-4 backdrop-blur-sm">
          <SidebarTrigger />
          <div className="ms-auto flex items-center gap-1">
            <Button
              variant="ghost"
              size="icon"
              onClick={() => setTheme(isDark ? "light" : "dark")}
              aria-label={t("common.toggleTheme")}
            >
              {mounted && isDark ? <Sun className="size-4" /> : <Moon className="size-4" />}
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={toggleLang}
              className="gap-2"
              aria-label={t("common.toggleLanguage")}
            >
              <Languages className="size-4" />
              <span>{lang === "ar" ? t("common.languageNameEn") : t("common.languageNameAr")}</span>
            </Button>
          </div>
        </header>
        <div ref={mainRef} className="flex-1 overflow-auto px-8 pb-20 lg:pb-[50px]">
          <Outlet />
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
