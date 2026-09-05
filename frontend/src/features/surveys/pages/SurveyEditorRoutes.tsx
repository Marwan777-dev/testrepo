// Inner route table for /surveys/:id/* (T033/T090; completed by US6–US9). Owns every
// per-survey sub-route. `id === "new"` is create-mode: only Settings renders (FR-5.5 —
// no survey row exists until Continue), matched because React Router ranks the static
// /surveys/new route above this splat for the bare path, while /surveys/new/settings
// lands here.

import { Navigate, Route, Routes, useParams } from "react-router"

import AnalyticsPage from "./AnalyticsPage"
import PreviewPage from "./PreviewPage"
import ReportPage from "./ReportPage"
import SurveyAppearancePage from "./SurveyAppearancePage"
import SurveyBuilderPage from "./SurveyBuilderPage"
import SurveySettingsPage from "./SurveySettingsPage"
import TranslateWorkspacePage from "./TranslateWorkspacePage"

export default function SurveyEditorRoutes() {
  const { id } = useParams<{ id: string }>()
  if (!id) return <Navigate to="/surveys" replace />
  const isCreate = id === "new"

  return (
    <Routes>
      {/* Deep links land on Settings (FR-1.4 row-click → Settings pre-filled). */}
      <Route index element={<Navigate to="settings" replace />} />
      <Route path="settings" element={<SurveySettingsPage surveyId={id} />} />
      {!isCreate && (
        <>
          <Route path="appearance" element={<SurveyAppearancePage surveyId={id} />} />
          <Route path="builder" element={<SurveyBuilderPage surveyId={id} />} />
          <Route path="preview" element={<PreviewPage surveyId={id} />} />
          <Route path="report" element={<ReportPage surveyId={id} />} />
          <Route path="analytics" element={<AnalyticsPage surveyId={id} />} />
          <Route path="translate" element={<TranslateWorkspacePage surveyId={id} />} />
        </>
      )}
      <Route path="*" element={<Navigate to="settings" replace />} />
    </Routes>
  )
}
