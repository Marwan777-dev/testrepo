// Barrel for the surveys feature hooks. Per-story hooks land here as later tasks add them.
export { useUnsavedChangesGuard } from "./useUnsavedChangesGuard"
export type { UnsavedChangesGuard } from "./useUnsavedChangesGuard"
export { useSurveyEtag } from "./useSurveyEtag"
export type { SurveyEtagState } from "./useSurveyEtag"
export { computeEditLock, useSurveyEditLock } from "./useSurveyEditLock"
export type {
  EditLockInput,
  EditLockReason,
  EditLockState,
  SurveyStatus,
} from "./useSurveyEditLock"
