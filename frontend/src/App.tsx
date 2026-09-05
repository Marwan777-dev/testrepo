import { lazy, Suspense, type ReactNode } from "react"
import { BrowserRouter, Navigate, Route, Routes } from "react-router"
import { useTranslation } from "react-i18next"
import { ThemeProvider } from "next-themes"

import { Skeleton } from "@/components/ui/skeleton"

import { AppLayout } from "@/components/layout/AppLayout"
import { Toaster } from "@/components/ui/sonner"
import { AuthGuard } from "@/features/auth/components/AuthGuard"
import { RequirePermission } from "@/features/auth/components/RequirePermission"
import { SessionProvider, useSession } from "@/features/auth/hooks/useSession"
import AuditLogPage from "@/features/audit-log/pages/AuditLogPage"
import LoginPage from "@/features/auth/pages/LoginPage"
import MfaChallengePage from "@/features/auth/pages/MfaChallengePage"
import MfaEnrollPage from "@/features/auth/pages/MfaEnrollPage"
import PasswordResetPage from "@/features/auth/pages/PasswordResetPage"
import DetectionRulesPage from "@/features/journeys/pages/DetectionRulesPage"
import JourneyBuilderPage from "@/features/journeys/pages/JourneyBuilderPage"
import JourneyListPage from "@/features/journeys/pages/JourneyListPage"
import KpiScoringPage from "@/features/journeys/pages/KpiScoringPage"
import KpiManagementPage from "@/features/kpi-management/pages/KpiManagementPage"
import KpiConfigPage from "@/features/kpi-management/pages/KpiConfigPage"
import SettingsPage from "@/features/settings/pages/SettingsPage"
import PersonaManagementPage from "@/features/journeys/pages/PersonaManagementPage"
import VersionHistoryPage from "@/features/journeys/pages/VersionHistoryPage"
import UserScopePage from "@/features/data-scope/pages/UserScopePage"
import PersonaBaselinePage from "@/features/persona-baselines/pages/PersonaBaselinePage"
import UserDetailPage from "@/features/users/pages/UserDetailPage"
import UserManagementPage from "@/features/users/pages/UserManagementPage"

// Integration Hub (M-13). Screens land story by story; every route exists from T020 so the nav,
// deep links, and the permission gate are exercisable before the screens themselves are built.
import {
  AllIntegrationsPlaceholder,
  IntegrationWizardPlaceholder,
  ParameterMappingsPlaceholder,
  RequestLogsPlaceholder,
} from "@/features/integration-hub/pages/ScreenPlaceholderPages"
import AllParametersPage from "@/features/integration-hub/pages/AllParametersPage"
import AllServiceChannelsPage from "@/features/integration-hub/pages/AllServiceChannelsPage"
import ServiceChannelFormPage from "@/features/integration-hub/components/ServiceChannelForm"

// Survey & Form Builder (M-01) pages are code-split — the builder tree is heavy
// (drag-and-drop canvas, rich text editing) and most sessions never open it.
const SurveyLibraryPage = lazy(() => import("@/features/surveys/pages/SurveyLibraryPage"))
const BuildMethodPage = lazy(() => import("@/features/surveys/pages/BuildMethodPage"))
const SurveyEditorRoutes = lazy(() => import("@/features/surveys/pages/SurveyEditorRoutes"))
const PostExpiryStorePage = lazy(() => import("@/features/surveys/pages/PostExpiryStorePage"))
const TemplateEditorPage = lazy(() => import("@/features/surveys/pages/TemplateEditorPage"))

// Skeleton block shown while a lazy page chunk loads (design system: Skeletons, not spinners).
function LazyPage({ children }: { children: ReactNode }) {
  return (
    <Suspense
      fallback={
        <div className="space-y-5 py-5">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-96" />
          <Skeleton className="h-64 w-full" />
        </div>
      }
    >
      {children}
    </Suspense>
  )
}

// Placeholder authenticated landing. Replaced by the real dashboard when a later
// feature ships it; exists now so the post-login redirect (`/`) lands somewhere.
function AuthenticatedHome() {
  const { t } = useTranslation()
  const { session } = useSession()
  return (
    <div className="space-y-5 py-5">
      <h1 className="text-2xl font-heading font-bold">{t("common.appName")}</h1>
      {session && (
        <p dir="ltr" className="font-mono text-sm text-muted-foreground">
          {session.userId}
        </p>
      )}
    </div>
  )
}

function App() {
  return (
    <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
      <BrowserRouter>
        <SessionProvider>
          <Routes>
            {/* Public auth routes */}
            <Route path="/login" element={<LoginPage />} />
            <Route path="/auth/mfa" element={<MfaChallengePage />} />
            <Route path="/auth/mfa/enroll" element={<MfaEnrollPage />} />
            <Route path="/auth/password-reset" element={<PasswordResetPage />} />

            {/* Authenticated routes — guarded, then rendered inside the app shell */}
            <Route element={<AuthGuard />}>
              <Route element={<AppLayout />}>
                <Route path="/" element={<AuthenticatedHome />} />

                {/* Customer Journey Mapping (M-16). Author access (P-01/P-02) is gated in the
                    sidebar; data-layer RBAC is enforced server-side per the M-16 contracts. */}
                <Route path="/journeys" element={<JourneyListPage />} />
                <Route path="/journeys/:id/builder" element={<JourneyBuilderPage />} />
                <Route path="/journeys/:id/scoring" element={<KpiScoringPage />} />
                <Route path="/journeys/:id/detection" element={<DetectionRulesPage />} />
                <Route path="/journeys/:id/versions" element={<VersionHistoryPage />} />
                <Route path="/personas" element={<PersonaManagementPage />} />

                {/* Survey & Form Builder (M-01). Authoring (P-01/P-03) vs read-only (P-02/P-06)
                    is gated in the sidebar; data-layer RBAC is enforced server-side per the
                    M-01 contracts. Pages are lazy-loaded (see LazyPage above). */}
                <Route path="/surveys" element={<LazyPage><SurveyLibraryPage /></LazyPage>} />
                <Route path="/surveys/new" element={<LazyPage><BuildMethodPage /></LazyPage>} />
                {/* Static M-07 shell (sample data) — the static segment outranks :id. */}
                <Route path="/surveys/post-expiry" element={<LazyPage><PostExpiryStorePage /></LazyPage>} />
                <Route path="/surveys/:id/*" element={<LazyPage><SurveyEditorRoutes /></LazyPage>} />
                {/* Templates now live as a tab of the Survey Library (clickthrough parity). */}
                <Route path="/templates" element={<Navigate to="/surveys?tab=templates" replace />} />
                <Route path="/templates/:id/edit" element={<LazyPage><TemplateEditorPage /></LazyPage>} />

                {/* CX Metrics & KPI Engine (M-06). Catalogue read access (P-01/P-02/P-06) is gated
                    in the sidebar; data-layer RBAC is enforced server-side per the M-06 contracts. */}
                <Route path="/kpi-management" element={<KpiManagementPage />} />
                <Route path="/kpi-management/new" element={<KpiConfigPage />} />
                <Route path="/kpi-management/:shortName" element={<KpiConfigPage />} />

                {/* Integration Hub (M-13). P-07 (Tenant IT Admin) owns integrations + request
                    logs; P-01 (CX Manager) owns the data model (channels, parameters, mappings)
                    and sees integrations read-only. Visibility is gated in the sidebar; the
                    server enforces the Permissions Matrix independently. */}
                <Route path="/integration-hub/integrations" element={<AllIntegrationsPlaceholder />} />
                <Route path="/integration-hub/integrations/new" element={<IntegrationWizardPlaceholder />} />
                <Route path="/integration-hub/integrations/:id" element={<IntegrationWizardPlaceholder />} />
                <Route path="/integration-hub/logs" element={<RequestLogsPlaceholder />} />
                <Route path="/integration-hub/service-channels" element={<AllServiceChannelsPage />} />
                <Route path="/integration-hub/service-channels/new" element={<ServiceChannelFormPage />} />
                <Route path="/integration-hub/service-channels/:id" element={<ServiceChannelFormPage />} />
                <Route path="/integration-hub/parameters" element={<AllParametersPage />} />
                <Route path="/integration-hub/mappings" element={<ParameterMappingsPlaceholder />} />

                {/* Platform Settings → Organization (M-06, US-6). Requires the TenantConfiguration
                    module (P-01 / P-07 per FR-052); the data layer enforces it server-side too. */}
                <Route element={<RequirePermission module="TenantConfiguration" />}>
                  {/* Unified Settings page — Organization + Customer Journey ScoringConfig sections
                      on one screen (M-06, US-4/US-6). Read = P-01/P-07; Customer Journey write =
                      P-01 only (enforced in-section + server-side). The former per-section routes
                      now redirect here. */}
                  <Route path="/settings" element={<SettingsPage />} />
                  <Route path="/settings/organization" element={<Navigate to="/settings" replace />} />
                  <Route path="/settings/customer-journey" element={<Navigate to="/settings" replace />} />
                </Route>

                {/* User & role management — requires the UserManagement module (FR-007) */}
                <Route element={<RequirePermission module="UserManagement" />}>
                  <Route path="/users" element={<UserManagementPage />} />
                  <Route path="/users/:userId" element={<UserDetailPage />} />
                  <Route path="/users/:userId/scope" element={<UserScopePage />} />
                  <Route path="/audit-log" element={<AuditLogPage />} />
                  <Route path="/settings/persona-baselines" element={<PersonaBaselinePage />} />
                </Route>
              </Route>
            </Route>

            {/* Unknown path → home (the guard sends unauthenticated users to /login) */}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </SessionProvider>
      </BrowserRouter>
      {/* App-wide toast outlet (sonner). Inside ThemeProvider so it tracks light/dark. */}
      <Toaster />
    </ThemeProvider>
  )
}

export default App
