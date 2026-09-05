/** Response of `POST /api/v1/auth/mfa/enroll` — TOTP enrollment material. */
export interface MfaEnrollResponse {
  otpauthUri: string
  base32Secret: string
  enrollmentToken: string
}
