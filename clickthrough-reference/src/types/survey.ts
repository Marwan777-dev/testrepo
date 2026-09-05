export type SurveyStatus = "Draft" | "Active" | "Paused" | "Archived"
export type SurveyType = "Transactional" | "Relational"
export type TemplateClass = "Platform" | "Tenant"
export type ShuffleMode = "Random" | "LowResponse" | "RoundRobin"
export type TranslationState = "untranslated" | "ai_draft" | "human_edited" | "approved" | "stale"

export interface Survey {
  id: string
  nameAr: string
  nameEn: string
  type: SurveyType
  status: SurveyStatus
  journeyId?: string
  journeyNameAr?: string
  journeyNameEn?: string
  version: string
  updatedAt: string
  responseCount: number
  questionCount: number
  surveyJson?: object
  settings?: SurveySettings
}

export interface SurveyTemplate {
  id: string
  nameAr: string
  nameEn: string
  templateClass: TemplateClass
  sectors: string[]
  usedBySurveys: number
  updatedAt: string
  questionCount: number
  surveyJson?: object
}

export interface SurveySettings {
  type: SurveyType
  journeyId?: string
  stageIds?: string[]
  shuffleEnabled: boolean
  shuffleMode: ShuffleMode
  postExpiryEnabled: boolean
  postExpiryMessage: string
}

export interface KpiQuestionConfig {
  questionName: string
  kpiId: string
  representationStyle: "Number" | "Stars" | "Faces" | "Slider" | "Thumbs"
  touchpointId?: string
  touchpointName?: string
  justificationEnabled: boolean
  justificationUnsatisfiedReasons: string[]
  justificationSatisfiedReasons: string[]
  justificationMultiSelect: boolean
  justificationHasOther: boolean
}

export interface QuestionBinding {
  questionName: string
  bindingLevel: "journey" | "stage" | "touchpoint"
  stageId?: string
  touchpointId?: string
  touchpointName?: string
}

export const DEFAULT_POST_EXPIRY_MESSAGE =
  "على الرغم من انتهاء فترة الاستبيان الأصلية، نحن نقدر آراءكم حول تجربتكم الأخيرة."

export const INDUSTRY_OPTIONS = [
  { value: "Banking", labelAr: "البنوك والمالية", labelEn: "Banking" },
  { value: "Telecommunications", labelAr: "الاتصالات", labelEn: "Telecommunications" },
  { value: "Government", labelAr: "الحكومة والقطاع العام", labelEn: "Government" },
  { value: "Automotive", labelAr: "السيارات", labelEn: "Automotive" },
  { value: "Entertainment", labelAr: "الترفيه", labelEn: "Entertainment" },
  { value: "Services", labelAr: "الخدمات", labelEn: "Services" },
]
