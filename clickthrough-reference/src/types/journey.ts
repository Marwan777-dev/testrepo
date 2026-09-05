export type JourneyStatus = 'Draft' | 'Active' | 'Archived'
export type JourneyType = 'Transactional' | 'Lifecycle' | 'Issue-Resolution' | 'Onboarding'
export type EmotionType = 'Excited' | 'Neutral' | 'Anxious' | 'Frustrated' | 'Confident' | 'Confused' | 'Relieved'
export type ChannelType =
  | 'Web'
  | 'Mobile App'
  | 'Email'
  | 'SMS'
  | 'WhatsApp'
  | 'Phone (Inbound)'
  | 'Phone (Outbound)'
  | 'Branch/In-Person'
  | 'Chat'
  | 'IVR'
  | 'Social Media'
  | 'Kiosk'
  | 'Other'
export type KPIType = 'NPS' | 'CSAT' | 'CES' | 'FCR' | 'Sentiment'

export interface KPIBinding {
  id: string
  kpiType: KPIType
  weightPct: number
}

export interface Touchpoint {
  id: string
  nameEn: string
  nameAr: string
  descriptionEn?: string
  descriptionAr?: string
  channels: ChannelType[]
  importanceCustomer: number
  importanceBusiness: number
  isMoT: boolean
  isMandatory: boolean
  kpiBindings: KPIBinding[]
  sequenceOrder: number
}

export interface Stage {
  id: string
  nameEn: string
  nameAr: string
  customerGoalEn?: string
  customerGoalAr?: string
  expectedEmotion?: EmotionType
  expectedDurationHours?: number
  sequenceFlag: 'Sequential' | 'Parallel'
  sequenceOrder: number
  touchpoints: Touchpoint[]
  isExpanded: boolean
}

export interface Journey {
  id: string
  nameEn: string
  nameAr: string
  descriptionEn?: string
  descriptionAr?: string
  journeyType: JourneyType
  status: JourneyStatus
  version: string
  updatedAt: string
  stages: Stage[]
  personas: string[]
  expectedDurationDays?: number
}
