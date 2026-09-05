// ─── Module 16 — Customer Journey Mapping ──────────────────
// Data model + mock fixtures for the M-16 module.
// Owned by M-16; consumed by M-06 (computation) and M-07 (display).
//
// All bilingual fields carry both `_en` and `_ar` forms per FR-8.1.

// ─── Enums ─────────────────────────────────────────────────

export type JourneyType =
  | "transactional"
  | "lifecycle"
  | "issue_resolution"
  | "onboarding"

export type JourneyStatus = "draft" | "active" | "archived"
export type PersonaStatus = "draft" | "active" | "archived"

export type StageEmotion =
  | "excited"
  | "neutral"
  | "anxious"
  | "frustrated"
  | "confident"
  | "confused"
  | "relieved"

export type SequenceFlag = "sequential" | "parallel"

export type KpiType =
  | "nps"
  | "csat_5"
  | "csat_10"
  | "ces_5"
  | "ces_7"
  | "fcr"
  | "sentiment"

export type ConfidenceStatus = "reliable" | "low_sample" | "insufficient"

// Channels are tenant-configurable (FR-2.14). Keys are stable; labels are
// resolved via the channel library.
export type ChannelKey =
  | "web"
  | "mobile_app"
  | "email"
  | "sms"
  | "whatsapp"
  | "phone_inbound"
  | "phone_outbound"
  | "branch"
  | "chat"
  | "ivr"
  | "social_media"
  | "kiosk"
  | "other"

// ─── Bilingual helper ──────────────────────────────────────

export interface Bilingual {
  en: string
  ar: string
}

// ─── Channel (tenant-scoped library) ───────────────────────

export interface TenantChannel {
  key: ChannelKey
  label: Bilingual
}

export const DEFAULT_CHANNELS: TenantChannel[] = [
  { key: "web", label: { en: "Web", ar: "الويب" } },
  { key: "mobile_app", label: { en: "Mobile App", ar: "تطبيق الجوال" } },
  { key: "email", label: { en: "Email", ar: "البريد الإلكتروني" } },
  { key: "sms", label: { en: "SMS", ar: "رسائل SMS" } },
  { key: "whatsapp", label: { en: "WhatsApp", ar: "واتساب" } },
  { key: "phone_inbound", label: { en: "Phone (Inbound)", ar: "هاتف (وارد)" } },
  { key: "phone_outbound", label: { en: "Phone (Outbound)", ar: "هاتف (صادر)" } },
  { key: "branch", label: { en: "Branch / In-Person", ar: "الفرع / حضوري" } },
  { key: "chat", label: { en: "Chat", ar: "محادثة" } },
  { key: "ivr", label: { en: "IVR", ar: "الرد الآلي" } },
  { key: "social_media", label: { en: "Social Media", ar: "وسائل التواصل" } },
  { key: "kiosk", label: { en: "Kiosk", ar: "كشك" } },
  { key: "other", label: { en: "Other", ar: "أخرى" } },
]

// ─── KPI Binding ───────────────────────────────────────────

export interface KpiBinding {
  type: KpiType
  weight: number // integer 0–100; all KPIs on a TP must sum to 100
}

export const KPI_LABELS: Record<KpiType, Bilingual> = {
  nps: { en: "NPS", ar: "NPS (صافي الترويج)" },
  csat_5: { en: "CSAT-5pt", ar: "CSAT (5 نقاط)" },
  csat_10: { en: "CSAT-10pt", ar: "CSAT (10 نقاط)" },
  ces_5: { en: "CES-5pt", ar: "CES (5 نقاط)" },
  ces_7: { en: "CES-7pt", ar: "CES (7 نقاط)" },
  fcr: { en: "FCR", ar: "FCR (الحل من أول تواصل)" },
  sentiment: { en: "Sentiment", ar: "تحليل المشاعر" },
}

export const DEFAULT_KPIS_BY_JOURNEY_TYPE: Record<JourneyType, KpiType[]> = {
  transactional: ["csat_5", "ces_5"],
  lifecycle: ["csat_5", "nps"],
  issue_resolution: ["ces_5", "fcr"],
  onboarding: ["csat_5", "ces_5", "nps"],
}

// ─── Touchpoint ────────────────────────────────────────────

export interface Touchpoint {
  id: string
  name: Bilingual
  description: Bilingual
  channels: ChannelKey[]
  importanceCustomer: 1 | 2 | 3 | 4 | 5
  importanceBusiness: 1 | 2 | 3 | 4 | 5
  isMot: boolean
  isMandatory: boolean
  tpWeight: number // positive integer; M-06 normalises within stage
  kpis: KpiBinding[]
  // Display fields (would come from M-06 ScoreSnapshot in production)
  score?: number
  responses?: number
  confidence?: ConfidenceStatus
}

// ─── Stage ─────────────────────────────────────────────────

export interface Stage {
  id: string
  name: Bilingual
  customerGoal: Bilingual
  expectedEmotion: StageEmotion
  expectedDurationDays?: number
  sequenceFlag: SequenceFlag
  stageWeight: number // positive integer; M-06 normalises within journey
  touchpoints: Touchpoint[]
  // Display field
  score?: number
  scoreDelta?: number
}

// ─── Journey ───────────────────────────────────────────────

export interface Journey {
  id: string
  name: Bilingual
  description: Bilingual
  type: JourneyType
  status: JourneyStatus
  version: string // "[MAJOR].[MINOR]" — "—" before first publish
  boundPersonaIds: string[]
  stages: Stage[]
  expectedDurationDays?: number
  createdAt: string
  publishedAt?: string
  updatedAt: string
  // Summary display fields
  totalResponses?: number
  overallScore?: number
}

// ─── Persona ───────────────────────────────────────────────

export interface PersonaAttribute {
  label: Bilingual
  value: string // free-text per spec clarification
}

export interface Persona {
  id: string
  name: Bilingual
  description: Bilingual
  avatarKey: string // built-in icon set
  status: PersonaStatus
  attributes: PersonaAttribute[] // both core + specific carry the same shape
  boundJourneyCount: number
}

// ─── Scoring & Detection Config (tenant-scoped) ────────────

export interface ScoringConfig {
  alpha: number // 0.0–1.0
  motMultiplier: number // 1.0–2.0
  nFloor: number // >= 1
  flagPercentile: number // 1–49
  rollingWindowDays: number // >= 7
}

export const DEFAULT_SCORING_CONFIG: ScoringConfig = {
  alpha: 0.5,
  motMultiplier: 1.5,
  nFloor: 5,
  flagPercentile: 25,
  rollingWindowDays: 30,
}

export interface DetectionConfig {
  painThreshold: number // 1–99
  happyThreshold: number // 1–99, must be > painThreshold
  volumePercentageThreshold: number // 1–100
  trendDelta: number // 1–50 points
  trendWindowDays: number // 7–365
  verbatimMinVolume: number // 1–1000
  minResponseThreshold: number // 1–500 (n_floor)
}

export const DEFAULT_DETECTION_CONFIG: DetectionConfig = {
  painThreshold: 50,
  happyThreshold: 80,
  volumePercentageThreshold: 20,
  trendDelta: 10,
  trendWindowDays: 30,
  verbatimMinVolume: 5,
  minResponseThreshold: 5,
}

// ─── Mock data ────────────────────────────────────────────

export const MOCK_PERSONAS: Persona[] = [
  {
    id: "p1",
    name: { en: "Young Professional", ar: "الموظف الشاب" },
    description: {
      en: "25–35, digital-first, time-sensitive, mobile-app primary channel",
      ar: "25-35 سنة، يفضّل الرقمي، يقدّر السرعة، يستخدم تطبيق الجوال بشكل رئيسي",
    },
    avatarKey: "briefcase",
    status: "active",
    attributes: [
      { label: { en: "Age Range", ar: "الفئة العمرية" }, value: "25–35" },
      { label: { en: "Primary Channel", ar: "القناة الرئيسية" }, value: "Mobile App" },
      { label: { en: "Tech Savvy", ar: "الإلمام التقني" }, value: "High" },
    ],
    boundJourneyCount: 3,
  },
  {
    id: "p2",
    name: { en: "Senior Client", ar: "العميل المخضرم" },
    description: {
      en: "55+, branch-preferred, value-driven, long-tenure",
      ar: "55+ سنة، يفضّل الفرع، يقدّر القيمة، عميل طويل الأمد",
    },
    avatarKey: "shield",
    status: "active",
    attributes: [
      { label: { en: "Age Range", ar: "الفئة العمرية" }, value: "55+" },
      { label: { en: "Primary Channel", ar: "القناة الرئيسية" }, value: "Branch" },
      { label: { en: "Tenure", ar: "مدة التعامل" }, value: "10+ years" },
    ],
    boundJourneyCount: 2,
  },
  {
    id: "p3",
    name: { en: "Small Business Owner", ar: "صاحب منشأة صغيرة" },
    description: {
      en: "Self-employed, multi-channel, decision-maker for SME accounts",
      ar: "صاحب عمل حر، يستخدم قنوات متعددة، صانع القرار لحسابات المنشآت الصغيرة",
    },
    avatarKey: "building",
    status: "active",
    attributes: [
      { label: { en: "Segment", ar: "الفئة" }, value: "SME" },
      { label: { en: "Primary Channel", ar: "القناة الرئيسية" }, value: "Multi-channel" },
    ],
    boundJourneyCount: 1,
  },
  {
    id: "p4",
    name: { en: "First-Time Borrower", ar: "المقترض لأول مرة" },
    description: {
      en: "New to credit products, needs guidance, high educational needs",
      ar: "جديد على المنتجات الائتمانية، يحتاج إلى توجيه، تعليمي العالي",
    },
    avatarKey: "graduation",
    status: "draft",
    attributes: [
      { label: { en: "Credit History", ar: "السجل الائتماني" }, value: "None" },
    ],
    boundJourneyCount: 0,
  },
]

// Apply for Personal Loan — Transactional · v2.1 · Active · 5 stages · 9 TPs
// Matches the BA's screenshots exactly.
const APPLY_LOAN: Journey = {
  id: "j1",
  name: { en: "Apply for Personal Loan", ar: "التقديم على قرض شخصي" },
  description: {
    en: "End-to-end journey from application submission to fund disbursement and post-loan follow-up.",
    ar: "الرحلة الكاملة من تقديم الطلب إلى صرف المبلغ ومتابعة ما بعد القرض.",
  },
  type: "transactional",
  status: "active",
  version: "2.1",
  boundPersonaIds: ["p1", "p3"],
  expectedDurationDays: 14,
  createdAt: "2026-03-15",
  publishedAt: "2026-05-28",
  updatedAt: "2026-05-28",
  totalResponses: 627,
  overallScore: 80,
  stages: [
    {
      id: "s1",
      name: { en: "Application Submission", ar: "تقديم الطلب" },
      customerGoal: {
        en: "Submit loan application quickly and receive confirmation.",
        ar: "تقديم طلب القرض بسرعة وتلقي تأكيد.",
      },
      expectedEmotion: "excited",
      sequenceFlag: "sequential",
      stageWeight: 20,
      score: 93,
      scoreDelta: -7,
      touchpoints: [
        {
          id: "t1",
          name: { en: "Submit via Mobile App", ar: "التقديم عبر تطبيق الجوال" },
          description: {
            en: "Customer fills the loan application form on the mobile app and submits supporting documents.",
            ar: "يقوم العميل بتعبئة نموذج طلب القرض على تطبيق الجوال وتقديم المستندات الداعمة.",
          },
          channels: ["mobile_app"],
          importanceCustomer: 5,
          importanceBusiness: 5,
          isMot: true,
          isMandatory: true,
          tpWeight: 3,
          kpis: [
            { type: "csat_5", weight: 60 },
            { type: "ces_5", weight: 40 },
          ],
          score: 55,
          responses: 142,
          confidence: "reliable",
        },
        {
          id: "t2",
          name: { en: "Upload Supporting Documents", ar: "رفع المستندات الداعمة" },
          description: {
            en: "Customer uploads salary certificate, ID, and other required documents.",
            ar: "يرفع العميل شهادة الراتب والهوية والمستندات المطلوبة.",
          },
          channels: ["mobile_app", "web"],
          importanceCustomer: 3,
          importanceBusiness: 4,
          isMot: false,
          isMandatory: true,
          tpWeight: 2,
          kpis: [{ type: "ces_5", weight: 100 }],
          score: 85,
          responses: 130,
          confidence: "reliable",
        },
        {
          id: "t3",
          name: { en: "Receive SMS Confirmation", ar: "استلام تأكيد عبر رسالة" },
          description: {
            en: "Customer receives an SMS confirmation that the application was successfully submitted.",
            ar: "يستلم العميل رسالة تأكيد بأن الطلب قد تم تقديمه بنجاح.",
          },
          channels: ["sms"],
          importanceCustomer: 4,
          importanceBusiness: 2,
          isMot: false,
          isMandatory: false,
          tpWeight: 1,
          kpis: [],
          score: 66,
          responses: 138,
          confidence: "reliable",
        },
      ],
    },
    {
      id: "s2",
      name: { en: "Credit Assessment", ar: "تقييم الائتمان" },
      customerGoal: {
        en: "Understand the credit review process and timeline.",
        ar: "فهم عملية مراجعة الائتمان والجدول الزمني.",
      },
      expectedEmotion: "anxious",
      sequenceFlag: "sequential",
      stageWeight: 25,
      score: 65,
      scoreDelta: 11,
      touchpoints: [
        {
          id: "t4",
          name: { en: "Background Credit Check", ar: "فحص الائتمان" },
          description: {
            en: "Internal credit review against SIMAH database and internal risk models.",
            ar: "مراجعة ائتمانية داخلية مقابل قاعدة سمة ونماذج المخاطر الداخلية.",
          },
          channels: ["phone_inbound"],
          importanceCustomer: 5,
          importanceBusiness: 5,
          isMot: true,
          isMandatory: true,
          tpWeight: 3,
          kpis: [
            { type: "csat_5", weight: 50 },
            { type: "ces_5", weight: 50 },
          ],
          score: 96,
          responses: 98,
          confidence: "reliable",
        },
        {
          id: "t5",
          name: { en: "Document Verification Call", ar: "مكالمة التحقق من المستندات" },
          description: {
            en: "Phone call from credit officer to verify the submitted documents.",
            ar: "اتصال هاتفي من موظف الائتمان للتحقق من المستندات المقدمة.",
          },
          channels: ["phone_outbound"],
          importanceCustomer: 3,
          importanceBusiness: 4,
          isMot: false,
          isMandatory: false,
          tpWeight: 2,
          kpis: [{ type: "csat_5", weight: 100 }],
          score: 77,
          responses: 76,
          confidence: "reliable",
        },
      ],
    },
    {
      id: "s3",
      name: { en: "Approval Decision", ar: "قرار الموافقة" },
      customerGoal: {
        en: "Receive a clear and timely approval or rejection decision.",
        ar: "تلقي قرار موافقة أو رفض واضح وفي الوقت المناسب.",
      },
      expectedEmotion: "confident",
      sequenceFlag: "sequential",
      stageWeight: 25,
      score: 80,
      scoreDelta: 6,
      touchpoints: [
        {
          id: "t6",
          name: { en: "Receive Approval Notification", ar: "استلام إشعار الموافقة" },
          description: {
            en: "Customer receives the formal approval or rejection notification.",
            ar: "يستلم العميل إشعار الموافقة أو الرفض الرسمي.",
          },
          channels: ["sms", "email"],
          importanceCustomer: 5,
          importanceBusiness: 4,
          isMot: true,
          isMandatory: true,
          tpWeight: 3,
          kpis: [{ type: "csat_5", weight: 100 }],
          score: 58,
          responses: 88,
          confidence: "reliable",
        },
      ],
    },
    {
      id: "s4",
      name: { en: "Loan Disbursement", ar: "صرف القرض" },
      customerGoal: {
        en: "Receive loan funds quickly and get disbursement confirmation.",
        ar: "استلام مبلغ القرض بسرعة والحصول على تأكيد الصرف.",
      },
      expectedEmotion: "relieved",
      sequenceFlag: "sequential",
      stageWeight: 20,
      score: 95,
      scoreDelta: 1,
      touchpoints: [
        {
          id: "t7",
          name: { en: "Funds Transfer to Account", ar: "تحويل المبلغ للحساب" },
          description: {
            en: "Loan amount is transferred to the customer's account.",
            ar: "يتم تحويل مبلغ القرض إلى حساب العميل.",
          },
          channels: ["mobile_app"],
          importanceCustomer: 5,
          importanceBusiness: 5,
          isMot: true,
          isMandatory: true,
          tpWeight: 3,
          kpis: [
            { type: "csat_5", weight: 60 },
            { type: "ces_5", weight: 40 },
          ],
          score: 88,
          responses: 84,
          confidence: "reliable",
        },
        {
          id: "t8",
          name: { en: "Receive Disbursement Receipt", ar: "استلام إيصال الصرف" },
          description: {
            en: "Receipt and disbursement summary sent to customer.",
            ar: "يتم إرسال إيصال الصرف وملخصه للعميل.",
          },
          channels: ["email"],
          importanceCustomer: 3,
          importanceBusiness: 3,
          isMot: false,
          isMandatory: false,
          tpWeight: 1,
          kpis: [{ type: "csat_5", weight: 100 }],
          score: 69,
          responses: 71,
          confidence: "reliable",
        },
      ],
    },
    {
      id: "s5",
      name: { en: "Post-Loan Follow-up", ar: "متابعة ما بعد القرض" },
      customerGoal: {
        en: "Feel supported during the early repayment period.",
        ar: "الشعور بالدعم خلال فترة السداد المبكرة.",
      },
      expectedEmotion: "neutral",
      sequenceFlag: "sequential",
      stageWeight: 10,
      score: 67,
      scoreDelta: -4,
      touchpoints: [
        {
          id: "t9",
          name: { en: "Welcome Call from RM", ar: "مكالمة ترحيب من مدير الحساب" },
          description: {
            en: "Relationship manager calls to welcome the customer and explain repayment terms.",
            ar: "يتصل مدير الحساب للترحيب بالعميل وشرح شروط السداد.",
          },
          channels: ["phone_outbound"],
          importanceCustomer: 3,
          importanceBusiness: 4,
          isMot: false,
          isMandatory: false,
          tpWeight: 1,
          kpis: [{ type: "csat_5", weight: 100 }],
          score: 50,
          responses: 65,
          confidence: "low_sample",
        },
      ],
    },
  ],
}

const ONBOARD_BUSINESS: Journey = {
  id: "j2",
  name: { en: "Onboard New Business Account", ar: "تأهيل حساب منشأة جديد" },
  description: {
    en: "Onboarding journey for new SME and corporate accounts.",
    ar: "رحلة تأهيل حسابات المنشآت الصغيرة والشركات الجديدة.",
  },
  type: "onboarding",
  status: "active",
  version: "1.3",
  boundPersonaIds: ["p3"],
  createdAt: "2026-02-10",
  publishedAt: "2026-05-20",
  updatedAt: "2026-05-20",
  totalResponses: 412,
  overallScore: 76,
  stages: [],
}

const RESOLVE_COMPLAINT: Journey = {
  id: "j3",
  name: { en: "Resolve Service Complaint", ar: "حل شكوى الخدمة" },
  description: {
    en: "Customer-initiated complaint resolution from intake to closure.",
    ar: "حل شكاوى العملاء من الاستلام إلى الإغلاق.",
  },
  type: "issue_resolution",
  status: "draft",
  version: "—",
  boundPersonaIds: [],
  createdAt: "2026-05-15",
  updatedAt: "2026-05-15",
  stages: [],
}

const ANNUAL_RENEWAL: Journey = {
  id: "j4",
  name: { en: "Annual Plan Renewal", ar: "تجديد الخطة السنوية" },
  description: {
    en: "Annual renewal journey for premium subscribers.",
    ar: "رحلة التجديد السنوي للمشتركين المميزين.",
  },
  type: "lifecycle",
  status: "archived",
  version: "1.0",
  boundPersonaIds: ["p2"],
  createdAt: "2025-08-01",
  publishedAt: "2025-12-10",
  updatedAt: "2025-12-10",
  totalResponses: 1248,
  overallScore: 72,
  stages: [],
}

export const MOCK_JOURNEYS: Journey[] = [
  APPLY_LOAN,
  ONBOARD_BUSINESS,
  RESOLVE_COMPLAINT,
  ANNUAL_RENEWAL,
]

// ─── Performance color helper (D1–D5) ──────────────────────
// Universal — defaults to 0–100 percentage; kpi-aware where the scale matters.

export const D1 = "#1A7A3C"
export const D2 = "#2EB85C"
export const D3 = "#E8A020"
export const D4 = "#E05C1A"
export const D5 = "#C01B2A"

export type PerfLevel = "d1" | "d2" | "d3" | "d4" | "d5"

// Maps a score to its D1–D5 band (KPI-aware). This is the single source of
// truth for performance banding — perfColor and any background/text token
// lookups derive from it, so the % color and its lighter background always
// agree on the same D-level.
export function perfLevel(value: number, kpiId?: string): PerfLevel {
  if (kpiId === "ces" || kpiId === "ces_5" || kpiId === "ces_7") {
    if (value <= 30) return "d1"
    if (value <= 40) return "d2"
    if (value <= 50) return "d3"
    if (value <= 60) return "d4"
    return "d5"
  }
  if (kpiId === "nps") {
    if (value >= 50) return "d1"
    if (value >= 40) return "d2"
    if (value >= 30) return "d3"
    if (value >= 20) return "d4"
    return "d5"
  }
  if (value >= 85) return "d1"
  if (value >= 75) return "d2"
  if (value >= 60) return "d3"
  if (value >= 45) return "d4"
  return "d5"
}

const D_HEX: Record<PerfLevel, string> = { d1: D1, d2: D2, d3: D3, d4: D4, d5: D5 }

export function perfColor(value: number, kpiId?: string): string {
  return D_HEX[perfLevel(value, kpiId)]
}

// Lighter-shade tint derived from the same D-scale status color — used to fill
// stage/touchpoint containers so their background reflects performance state
// (per the journey-report design rule). Returns inline-style hex+alpha values
// that sit gently over both light and dark card surfaces.
export function perfTint(
  value: number,
  kpiId?: string,
): { bg: string; border: string } {
  const c = perfColor(value, kpiId)
  return { bg: `${c}14`, border: `${c}3D` } // ~8% fill, ~24% border
}

// ─── Stage emotion → emoji + style (display-only) ──────────

export const EMOTION_META: Record<
  StageEmotion,
  { emoji: string; bg: string; ring: string }
> = {
  excited: { emoji: "😀", bg: "bg-d2-light", ring: "ring-d2/40" },
  neutral: { emoji: "😐", bg: "bg-d3-light", ring: "ring-d3/40" },
  anxious: { emoji: "😰", bg: "bg-d4-light", ring: "ring-d4/40" },
  frustrated: { emoji: "😤", bg: "bg-d5-light", ring: "ring-d5/40" },
  confident: { emoji: "😎", bg: "bg-nb-mint-100", ring: "ring-nb-mint/40" },
  confused: { emoji: "😕", bg: "bg-d3-light", ring: "ring-d3/40" },
  relieved: { emoji: "😌", bg: "bg-nb-cyan-100", ring: "ring-nb-cyan/40" },
}
