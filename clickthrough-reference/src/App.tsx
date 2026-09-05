import { BrowserRouter, Routes, Route, Navigate } from "react-router"
import { AuthProvider, useAuth } from "./contexts/auth-context"
import { PersonaProvider } from "./contexts/persona-context"
import { AppLayout } from "./components/layout/app-layout"
import LoginPage from "./pages/LoginPage"
import CxDashboard from "./pages/CxDashboard"
import VocDashboard from "./pages/VocDashboard"
import ComponentGuide from "./pages/ComponentGuide"
import KpiDetailPage from "./pages/KpiDetailPage"
import FeedbackPage from "./pages/FeedbackPage"
import SurveysLibraryPage from "./pages/SurveysLibraryPage"
import SurveyBuilderPage from "./pages/SurveyBuilderPage"
import TemplateBuilderPage from "./pages/TemplateBuilderPage"
import TemplatePickerPage from "./pages/TemplatePickerPage"
import AiSurveyPage from "./pages/AiSurveyPage"
import SurveyTranslationsPage from "./pages/SurveyTranslationsPage"
import SurveyPreviewPage from "./pages/SurveyPreviewPage"
import SurveyFunnelPage from "./pages/SurveyFunnelPage"
import SurveyStatsPage from "./pages/SurveyStatsPage"
import PlaceholderPage from "./pages/PlaceholderPage"
import JourneysPage from "./pages/JourneysPage"
import JourneyBuilderPage from "./pages/JourneyBuilderPage"
import JourneyStatsPage from "./pages/JourneyStatsPage"
import KpiManagementPage from "./pages/KpiManagementPage"
import KpiConfigPage from "./pages/KpiConfigPage"
import SettingsPage from "./pages/SettingsPage"
import SettingsOrganizationPage from "./pages/SettingsOrganizationPage"
import SettingsCustomerJourneyPage from "./pages/SettingsCustomerJourneyPage"
import { SettingsProvider } from "./contexts/settings-context"

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isLoggedIn } = useAuth()
  if (!isLoggedIn) return <Navigate to="/login" replace />
  return <>{children}</>
}

function LayoutRoute({ children }: { children: React.ReactNode }) {
  return (
    <ProtectedRoute>
      <AppLayout>{children}</AppLayout>
    </ProtectedRoute>
  )
}

function AppRoutes() {
  const { isLoggedIn } = useAuth()

  return (
    <Routes>
      <Route
        path="/login"
        element={isLoggedIn ? <Navigate to="/" replace /> : <LoginPage />}
      />
      <Route path="/" element={<LayoutRoute><CxDashboard /></LayoutRoute>} />
      <Route path="/dashboard" element={<LayoutRoute><CxDashboard /></LayoutRoute>} />
      <Route path="/kpi/:id" element={<LayoutRoute><KpiDetailPage /></LayoutRoute>} />
      <Route path="/voc" element={<LayoutRoute><VocDashboard /></LayoutRoute>} />
      <Route path="/feedback" element={<LayoutRoute><FeedbackPage /></LayoutRoute>} />
      {/* Survey Builder module (M-01) */}
      <Route path="/surveys" element={<LayoutRoute><SurveysLibraryPage /></LayoutRoute>} />
      <Route path="/surveys/new/builder" element={<LayoutRoute><SurveyBuilderPage /></LayoutRoute>} />
      <Route path="/surveys/new/from-template/:templateId" element={<LayoutRoute><SurveyBuilderPage /></LayoutRoute>} />
      <Route path="/surveys/new/ai" element={<LayoutRoute><AiSurveyPage /></LayoutRoute>} />
      <Route path="/surveys/templates/pick" element={<LayoutRoute><TemplatePickerPage /></LayoutRoute>} />
      <Route path="/surveys/templates/new" element={<LayoutRoute><TemplateBuilderPage /></LayoutRoute>} />
      <Route path="/surveys/templates/:id/edit" element={<LayoutRoute><TemplateBuilderPage /></LayoutRoute>} />
      <Route path="/surveys/:id/edit" element={<LayoutRoute><SurveyBuilderPage /></LayoutRoute>} />
      <Route path="/surveys/:id/preview" element={<LayoutRoute><SurveyPreviewPage /></LayoutRoute>} />
      <Route path="/surveys/:id/translations" element={<LayoutRoute><SurveyTranslationsPage /></LayoutRoute>} />
      <Route path="/surveys/:id/funnel" element={<LayoutRoute><SurveyFunnelPage /></LayoutRoute>} />
      <Route path="/surveys/:id/stats" element={<LayoutRoute><SurveyStatsPage /></LayoutRoute>} />
      <Route path="/distribution" element={<LayoutRoute><PlaceholderPage titleKey="cx.navDistribution" /></LayoutRoute>} />
      <Route path="/sending-rules" element={<LayoutRoute><PlaceholderPage titleKey="cx.navSendingRules" /></LayoutRoute>} />
      <Route path="/analytics" element={<LayoutRoute><PlaceholderPage titleKey="cx.navAnalyticsReports" /></LayoutRoute>} />
      <Route path="/closed-loop" element={<LayoutRoute><PlaceholderPage titleKey="cx.navClosedLoop" /></LayoutRoute>} />
      <Route path="/actions" element={<LayoutRoute><PlaceholderPage titleKey="cx.navActions" /></LayoutRoute>} />
      <Route path="/journeys" element={<LayoutRoute><JourneysPage /></LayoutRoute>} />
      <Route path="/journeys/:id" element={<LayoutRoute><JourneyBuilderPage /></LayoutRoute>} />
      <Route path="/journeys/:id/stats" element={<LayoutRoute><JourneyStatsPage /></LayoutRoute>} />
      <Route path="/kpi-management" element={<LayoutRoute><KpiManagementPage /></LayoutRoute>} />
      <Route path="/kpi-management/new" element={<LayoutRoute><KpiConfigPage /></LayoutRoute>} />
      <Route path="/kpi-management/:id" element={<LayoutRoute><KpiConfigPage /></LayoutRoute>} />
      <Route path="/settings" element={<LayoutRoute><SettingsPage /></LayoutRoute>} />
      <Route path="/settings/organization" element={<LayoutRoute><SettingsOrganizationPage /></LayoutRoute>} />
      <Route path="/settings/customer-journey" element={<LayoutRoute><SettingsCustomerJourneyPage /></LayoutRoute>} />
      <Route path="/guide" element={<ComponentGuide />} />
    </Routes>
  )
}

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
