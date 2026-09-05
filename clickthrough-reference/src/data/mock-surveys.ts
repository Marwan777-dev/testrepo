import type { Survey, SurveyTemplate } from "@/types/survey"

export const MOCK_SURVEYS: Survey[] = [
  {
    id: "srv-001",
    nameAr: "استبيان رضا العملاء — الربع الثاني",
    nameEn: "Customer Satisfaction Survey — Q2",
    type: "Relational",
    status: "Active",
    journeyId: "j-001",
    journeyNameAr: "رحلة الاشتراك الرقمي",
    journeyNameEn: "Digital Onboarding Journey",
    version: "v2.1",
    updatedAt: "2026-06-18",
    responseCount: 1842,
    questionCount: 8,
    settings: {
      type: "Relational",
      journeyId: "j-001",
      stageIds: ["s-001", "s-002"],
      shuffleEnabled: false,
      shuffleMode: "Random",
      postExpiryEnabled: false,
      postExpiryMessage: "",
    },
    surveyJson: {
      pages: [
        {
          name: "page1",
          elements: [
            {
              type: "kpiQuestion",
              name: "q_csat",
              title: { ar: "كيف تقيّم تجربتك العامة مع خدمتنا؟", en: "How would you rate your overall experience?" },
            },
            {
              type: "rating",
              name: "q_nps",
              title: { ar: "ما مدى احتمال توصيتك بخدماتنا لأصدقائك وزملائك؟", en: "How likely are you to recommend us?" },
              rateMin: 0,
              rateMax: 10,
            },
            {
              type: "comment",
              name: "q_open",
              title: { ar: "ما الذي يمكننا تحسينه في تجربتك؟", en: "What could we improve about your experience?" },
            },
          ],
        },
      ],
    },
  },
  {
    id: "srv-002",
    nameAr: "استبيان ما بعد المعاملة — الفرع",
    nameEn: "Post-Transaction Survey — Branch",
    type: "Transactional",
    status: "Active",
    journeyId: "j-002",
    journeyNameAr: "رحلة الخدمة في الفرع",
    journeyNameEn: "Branch Service Journey",
    version: "v1.3",
    updatedAt: "2026-06-15",
    responseCount: 634,
    questionCount: 5,
    settings: {
      type: "Transactional",
      journeyId: "j-002",
      shuffleEnabled: true,
      shuffleMode: "Random",
      postExpiryEnabled: true,
      postExpiryMessage:
        "على الرغم من انتهاء فترة الاستبيان، نقدر رأيك حول تجربتك الأخيرة في الفرع.",
    },
  },
  {
    id: "srv-003",
    nameAr: "مسح صوت العميل — المنتجات الرقمية",
    nameEn: "VOC Pulse — Digital Products",
    type: "Relational",
    status: "Draft",
    version: "v1.0",
    updatedAt: "2026-06-20",
    responseCount: 0,
    questionCount: 12,
    settings: {
      type: "Relational",
      shuffleEnabled: false,
      shuffleMode: "Random",
      postExpiryEnabled: false,
      postExpiryMessage: "",
    },
  },
  {
    id: "srv-004",
    nameAr: "تقييم جودة خدمة العملاء",
    nameEn: "Customer Service Quality Assessment",
    type: "Transactional",
    status: "Paused",
    journeyId: "j-003",
    journeyNameAr: "رحلة دعم العملاء",
    journeyNameEn: "Customer Support Journey",
    version: "v3.0",
    updatedAt: "2026-05-30",
    responseCount: 2291,
    questionCount: 7,
    settings: {
      type: "Transactional",
      journeyId: "j-003",
      shuffleEnabled: false,
      shuffleMode: "Random",
      postExpiryEnabled: false,
      postExpiryMessage: "",
    },
  },
  {
    id: "srv-005",
    nameAr: "استبيان إعادة تصميم التطبيق",
    nameEn: "App Redesign Feedback Survey",
    type: "Transactional",
    status: "Archived",
    journeyId: "j-001",
    journeyNameAr: "رحلة الاشتراك الرقمي",
    journeyNameEn: "Digital Onboarding Journey",
    version: "v1.0",
    updatedAt: "2026-03-10",
    responseCount: 5103,
    questionCount: 6,
  },
]

export const MOCK_TEMPLATES: SurveyTemplate[] = [
  {
    id: "tpl-001",
    nameAr: "قالب NPS المصرفي القياسي",
    nameEn: "Standard Banking NPS Template",
    templateClass: "Platform",
    sectors: ["Banking"],
    usedBySurveys: 14,
    updatedAt: "2026-05-01",
    questionCount: 4,
  },
  {
    id: "tpl-002",
    nameAr: "قالب رضا الخدمة الحكومية",
    nameEn: "Government Service Satisfaction",
    templateClass: "Platform",
    sectors: ["Government"],
    usedBySurveys: 8,
    updatedAt: "2026-04-15",
    questionCount: 6,
  },
  {
    id: "tpl-003",
    nameAr: "نبض التجربة الرقمية",
    nameEn: "Digital Experience Pulse",
    templateClass: "Platform",
    sectors: ["Banking", "Telecommunications", "Services"],
    usedBySurveys: 22,
    updatedAt: "2026-04-20",
    questionCount: 5,
  },
  {
    id: "tpl-004",
    nameAr: "قالب تقييم رحلة الاشتراك",
    nameEn: "Onboarding Journey Assessment",
    templateClass: "Tenant",
    sectors: ["Banking"],
    usedBySurveys: 3,
    updatedAt: "2026-06-01",
    questionCount: 7,
  },
  {
    id: "tpl-005",
    nameAr: "استبيان جودة خدمة الاتصالات",
    nameEn: "Telecom Service Quality Survey",
    templateClass: "Platform",
    sectors: ["Telecommunications"],
    usedBySurveys: 11,
    updatedAt: "2026-03-28",
    questionCount: 8,
  },
  {
    id: "tpl-006",
    nameAr: "قالب ما بعد المعاملة — مخصص",
    nameEn: "Post-Transaction — Custom",
    templateClass: "Tenant",
    sectors: ["Banking", "Services"],
    usedBySurveys: 1,
    updatedAt: "2026-06-10",
    questionCount: 5,
  },
]

export const MOCK_JOURNEYS_FOR_BINDING = [
  { id: "j-001", nameAr: "رحلة الاشتراك الرقمي", nameEn: "Digital Onboarding Journey" },
  { id: "j-002", nameAr: "رحلة الخدمة في الفرع", nameEn: "Branch Service Journey" },
  { id: "j-003", nameAr: "رحلة دعم العملاء", nameEn: "Customer Support Journey" },
]

export const MOCK_STAGES_BY_JOURNEY: Record<string, { id: string; nameAr: string; nameEn: string }[]> = {
  "j-001": [
    { id: "s-001", nameAr: "التسجيل والتحقق", nameEn: "Registration & Verification" },
    { id: "s-002", nameAr: "إعداد الحساب", nameEn: "Account Setup" },
    { id: "s-003", nameAr: "الاستخدام الأول", nameEn: "First Use" },
  ],
  "j-002": [
    { id: "s-011", nameAr: "الوصول إلى الفرع", nameEn: "Branch Arrival" },
    { id: "s-012", nameAr: "الانتظار في الطابور", nameEn: "Queue Wait" },
    { id: "s-013", nameAr: "تنفيذ المعاملة", nameEn: "Transaction Execution" },
  ],
  "j-003": [
    { id: "s-021", nameAr: "إنشاء الطلب", nameEn: "Ticket Creation" },
    { id: "s-022", nameAr: "المعالجة", nameEn: "Resolution" },
    { id: "s-023", nameAr: "الإغلاق وتقييم الرضا", nameEn: "Closure & CSAT" },
  ],
}

export const MOCK_TOUCHPOINTS_BY_STAGE: Record<string, { id: string; nameAr: string; nameEn: string }[]> = {
  "s-001": [
    { id: "tp-001", nameAr: "نموذج التسجيل عبر الإنترنت", nameEn: "Online Registration Form" },
    { id: "tp-002", nameAr: "رسالة التحقق عبر SMS", nameEn: "SMS Verification" },
  ],
  "s-002": [
    { id: "tp-003", nameAr: "شاشة إنشاء ملف المستخدم", nameEn: "Profile Setup Screen" },
    { id: "tp-004", nameAr: "رفع المستندات", nameEn: "Document Upload" },
  ],
  "s-013": [
    { id: "tp-011", nameAr: "نافذة الصراف", nameEn: "Teller Window" },
    { id: "tp-012", nameAr: "نقطة البيع", nameEn: "POS Terminal" },
  ],
}

export const MOCK_ACTIVE_KPIS = [
  { id: "nps", shortName: "NPS", fullName: "Net Promoter Score", scale: "0–10" },
  { id: "csat", shortName: "CSAT", fullName: "Customer Satisfaction Score", scale: "1–5" },
  { id: "ces", shortName: "CES", fullName: "Customer Effort Score", scale: "1–7" },
  { id: "fcr", shortName: "FCR", fullName: "First Contact Resolution", scale: "1–5" },
  { id: "vfm", shortName: "VFM", fullName: "Value for Money", scale: "1–5" },
  { id: "agent", shortName: "Agent Score", fullName: "Agent Performance Score", scale: "1–5" },
  { id: "cxi", shortName: "CXI", fullName: "Customer Experience Index", scale: "1–100" },
  { id: "chs", shortName: "CHS", fullName: "Customer Health Score", scale: "1–100" },
]
