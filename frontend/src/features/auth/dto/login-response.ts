/** Response of `POST /api/v1/auth/login` (step 1 — credential validation). */
export interface LoginResponse {
  challengeId: string
  requiresMfaEnrollment: boolean
}
