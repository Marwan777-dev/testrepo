/** Response of `POST /api/v1/auth/password-reset/redeem`. */
export interface PasswordResetRedeemResponse {
  requiresMfaReenrollment: boolean
}
