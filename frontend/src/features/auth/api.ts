// M-10 Authentication API client — thin endpoint functions over `callJson`.
//
// Endpoints follow specs/001-user-role-management/contracts/auth-api.md. Wire
// types live in ./dto, the transport helper in ./http, the error type in
// ./auth-api-error, and token storage in ./session-token. Those are re-exported
// here so callers can import everything auth-API from "@/features/auth/api".

import { callJson } from "./http"
import type {
  LoginResponse,
  MfaEnrollResponse,
  PasswordResetRedeemResponse,
  SessionResponse,
  SessionTokenResponse,
} from "./dto"

export type * from "./dto"
export { AuthApiError } from "./auth-api-error"
export {
  SESSION_TOKEN_KEY,
  getSessionToken,
  setSessionToken,
  clearSessionToken,
} from "./session-token"

/** Step 1 of login: validates credentials, returns an MFA challenge id. */
export function loginStep1(email: string, password: string): Promise<LoginResponse> {
  return callJson<LoginResponse>("/login", {
    body: { username: email, password },
  })
}

/** Completes the MFA challenge with a TOTP code; creates a session. */
export function mfaVerify(challengeId: string, code: string): Promise<SessionTokenResponse> {
  return callJson<SessionTokenResponse>("/mfa/verify", {
    body: { challengeId, totpCode: code },
  })
}

/** Initiates TOTP enrollment for a first-time / reset-MFA user. */
export function mfaEnroll(challengeId: string): Promise<MfaEnrollResponse> {
  return callJson<MfaEnrollResponse>("/mfa/enroll", {
    body: { challengeId },
  })
}

/** Confirms TOTP enrollment with the first code; creates a session. */
export function mfaEnrollConfirm(
  enrollmentToken: string,
  code: string,
): Promise<SessionTokenResponse> {
  return callJson<SessionTokenResponse>("/mfa/enroll/confirm", {
    body: { enrollmentToken, totpCode: code },
  })
}

/** Skips the MFA step for a valid challenge; creates a session without TOTP. */
export function mfaSkip(challengeId: string): Promise<SessionTokenResponse> {
  return callJson<SessionTokenResponse>("/mfa/skip", {
    body: { challengeId },
  })
}

/** Revokes the current session (204 No Content). */
export function logout(): Promise<void> {
  return callJson<void>("/logout", { method: "POST" })
}

/** Requests a self-service password reset for the given email (202 Accepted). */
export function requestPasswordReset(email: string): Promise<void> {
  return callJson<void>("/password-reset/request", {
    body: { email },
  })
}

/** Redeems a password-reset token and sets a new password. */
export function redeemPasswordReset(
  token: string,
  newPassword: string,
): Promise<PasswordResetRedeemResponse> {
  return callJson<PasswordResetRedeemResponse>("/password-reset/redeem", {
    body: { token, newPassword },
  })
}

/** Returns the current session's permission snapshot (used to refresh state). */
export function getSession(): Promise<SessionResponse> {
  return callJson<SessionResponse>("/session")
}
