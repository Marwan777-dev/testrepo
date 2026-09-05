// Effective edit-lock state for a survey, per BR-15.1 + the Q8 team-owned rules:
//
// - Draft            → editable by P-01 and by ANY P-03 in the tenant (Q8: Draft
//                      ownership is team-scoped — not just the author). P-02/P-06 are
//                      read-only everywhere.
// - Pending review   → the reviewer (P-01) may edit before publishing (BR-15.1);
//                      every P-03 — submitter included — is locked.
// - Active / Paused  → not editable in place for anyone (BR-1.5); requires the
//                      destructive Return-to-Draft (BR-1.6).
// - Archived         → locked; only Unarchive → Draft re-enables editing (FR-1.14).
//
// Concurrency between two editing P-03s is NOT handled here — that's the ETag flow
// (useSurveyEtag + ETagConflictError). This hook only answers "may this user edit at
// all", mirroring the server-side EditLockFilter; the API remains authoritative.

import { useSession } from "@/features/auth/hooks/useSession"

export type SurveyStatus = "Draft" | "PendingReview" | "Active" | "Paused" | "Archived"

export type EditLockReason =
  /** Locked because the survey awaits review and the caller submitted it (BR-15.1). */
  | "pending_review_submitted_by_you"
  /** Locked because the survey awaits review (caller is a non-reviewer teammate). */
  | "pending_review"
  /** Locked because Active surveys require destructive Return-to-Draft (BR-1.5/1.6). */
  | "active_requires_return_to_draft"
  /** Locked because Paused surveys require destructive Return-to-Draft (BR-1.5/1.6). */
  | "paused_requires_return_to_draft"
  /** Locked because Archived surveys must be Unarchived to Draft first (FR-1.14). */
  | "archived_requires_unarchive"
  /** Locked because the caller's persona is read-only for surveys (P-02 / P-06). */
  | "role_read_only"

export interface EditLockState {
  canEdit: boolean
  /** Why editing is locked; null when `canEdit` is true. */
  reason: EditLockReason | null
}

export interface EditLockInput {
  status: SurveyStatus
  /** User id that submitted the survey for review; null/undefined outside PendingReview. */
  submittedBy?: string | null
}

const AUTHOR_ROLES = new Set(["P-01", "P-03"])

/**
 * Pure edit-lock computation — exported separately from the hook so it is directly
 * unit-testable and reusable outside React (e.g. table row action menus).
 */
export function computeEditLock(
  status: SurveyStatus,
  callerRole: string,
  callerUserId: string,
  submittedBy?: string | null
): EditLockState {
  if (!AUTHOR_ROLES.has(callerRole)) {
    return { canEdit: false, reason: "role_read_only" }
  }
  switch (status) {
    case "Draft":
      // Q8: team-owned — any P-03 (and P-01) edits any Draft in the tenant.
      return { canEdit: true, reason: null }
    case "PendingReview":
      if (callerRole === "P-01") return { canEdit: true, reason: null } // BR-15.1 reviewer
      return {
        canEdit: false,
        reason:
          submittedBy != null && submittedBy === callerUserId
            ? "pending_review_submitted_by_you"
            : "pending_review",
      }
    case "Active":
      return { canEdit: false, reason: "active_requires_return_to_draft" }
    case "Paused":
      return { canEdit: false, reason: "paused_requires_return_to_draft" }
    case "Archived":
      return { canEdit: false, reason: "archived_requires_unarchive" }
  }
}

/**
 * React binding: derives the edit-lock state for the given survey from the current
 * session's persona + user id. While the session (or survey) is still loading, returns
 * locked-with-null-reason so forms render disabled rather than flashing editable.
 */
export function useSurveyEditLock(survey: EditLockInput | null | undefined): EditLockState {
  const { session } = useSession()
  if (!survey || !session) return { canEdit: false, reason: null }
  return computeEditLock(survey.status, session.persona, session.userId, survey.submittedBy)
}
