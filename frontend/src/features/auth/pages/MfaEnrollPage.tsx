// M-10 MFA enrollment — for first-time users (and users whose MFA was reset).
// On entry we ask the backend for TOTP material (otpauth URI + Base32 secret +
// enrollment token), render the QR code to scan, and confirm with the first
// 6-digit code. A successful confirm creates the session and lands the user on
// the dashboard. The `challengeId` arrives via router state from the login page.

import { useEffect, useRef, useState } from "react"
import { useLocation, useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { QRCodeSVG } from "qrcode.react"
import { Check, Copy, Eye, EyeOff, Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import type { MfaEnrollResponse } from "@/features/auth/api"
import { AuthApiError, mfaEnroll, mfaEnrollConfirm, mfaSkip, setSessionToken } from "@/features/auth/api"
import { AuthLayout } from "@/features/auth/components/AuthLayout"
import { OtpField } from "@/features/auth/components/OtpField"
import { useSession } from "@/features/auth/hooks/useSession"

export default function MfaEnrollPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const { refreshPermissions } = useSession()
  const challengeId = (location.state as { challengeId?: string } | null)?.challengeId ?? null

  const [enroll, setEnroll] = useState<MfaEnrollResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [code, setCode] = useState("")
  const [confirmError, setConfirmError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [skipping, setSkipping] = useState(false)

  const [showSecret, setShowSecret] = useState(false)
  const [copied, setCopied] = useState(false)

  const startedRef = useRef(false)

  // Kick off enrollment once. Reaching here without a challenge → back to login.
  useEffect(() => {
    if (!challengeId) {
      navigate("/login", { replace: true })
      return
    }
    if (startedRef.current) return
    startedRef.current = true
    mfaEnroll(challengeId)
      .then(setEnroll)
      .catch((err) => {
        const expired = err instanceof AuthApiError && err.status === 400
        setLoadError(t(expired ? "auth.mfaEnrollExpired" : "auth.mfaEnrollLoadError"))
      })
      .finally(() => setLoading(false))
  }, [challengeId, navigate, t])

  // Clear the "copied" affordance after a moment.
  useEffect(() => {
    if (!copied) return
    const id = setTimeout(() => setCopied(false), 2000)
    return () => clearTimeout(id)
  }, [copied])

  if (!challengeId) return null

  async function confirm(value: string) {
    if (!enroll || submitting) return
    setSubmitting(true)
    setConfirmError(null)
    try {
      const session = await mfaEnrollConfirm(enroll.enrollmentToken, value)
      setSessionToken(session.sessionToken)
      // Hydrate the session context from the new token before entering the guarded
      // area (see MfaChallengePage) — otherwise the AuthGuard bounces to /login.
      await refreshPermissions()
      navigate("/", { replace: true })
    } catch (err) {
      const invalid =
        err instanceof AuthApiError && (err.code === "auth.mfa.invalid_code" || err.status === 422)
      setConfirmError(t(invalid ? "auth.mfaInvalidCode" : "auth.errorGeneric"))
      setCode("")
    } finally {
      setSubmitting(false)
    }
  }

  async function copySecret() {
    if (!enroll) return
    try {
      await navigator.clipboard.writeText(enroll.base32Secret)
      setCopied(true)
    } catch {
      // Clipboard denied — the secret is still visible for manual entry.
    }
  }

  async function skip() {
    if (!challengeId || submitting || skipping) return
    setSkipping(true)
    try {
      const session = await mfaSkip(challengeId)
      setSessionToken(session.sessionToken)
      await refreshPermissions()
      navigate("/", { replace: true })
    } catch {
      setConfirmError(t("auth.errorGeneric"))
    } finally {
      setSkipping(false)
    }
  }

  return (
    <AuthLayout title={t("auth.mfaEnrollTitle")} subtitle={t("auth.mfaEnrollSubtitle")}>
      {loading ? (
        <div className="flex justify-center py-8">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      ) : loadError ? (
        <div className="space-y-4">
          <div
            role="alert"
            className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            {loadError}
          </div>
          <div className="text-center">
            <Button
              type="button"
              variant="link"
              size="sm"
              className="font-normal"
              onClick={() => navigate("/login", { replace: true })}
            >
              {t("auth.mfaBackToLogin")}
            </Button>
          </div>
        </div>
      ) : (
        enroll && (
          <div className="space-y-5">
            {/* QR — kept black-on-white for reliable scanning, not themed. */}
            <div className="flex justify-center">
              <div className="rounded-md border border-border bg-white p-3 shadow-sm">
                <QRCodeSVG
                  value={enroll.otpauthUri}
                  size={176}
                  bgColor="#ffffff"
                  title={t("auth.mfaEnrollQrAlt")}
                />
              </div>
            </div>

            {/* Manual setup key fallback */}
            <div className="space-y-2">
              <p className="text-center text-sm text-muted-foreground">{t("auth.mfaEnrollManual")}</p>
              <div className="flex items-center gap-2">
                <code
                  dir="ltr"
                  className="flex-1 truncate rounded-md border border-input bg-muted/40 px-3 py-2 text-center font-mono text-sm tracking-wider"
                >
                  {showSecret ? enroll.base32Secret : "•".repeat(enroll.base32Secret.length)}
                </code>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="size-9 shrink-0 text-muted-foreground"
                  aria-label={showSecret ? t("auth.mfaEnrollHideSecret") : t("auth.mfaEnrollShowSecret")}
                  onClick={() => setShowSecret((v) => !v)}
                >
                  {showSecret ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="size-9 shrink-0 text-muted-foreground"
                  aria-label={t("auth.mfaEnrollCopy")}
                  onClick={copySecret}
                >
                  {copied ? <Check className="size-4 text-primary" /> : <Copy className="size-4" />}
                </Button>
              </div>
              {copied && (
                <p role="status" className="text-center text-xs text-muted-foreground">
                  {t("auth.mfaEnrollCopied")}
                </p>
              )}
            </div>

            {/* Confirm with the first code */}
            <OtpField
              value={code}
              onChange={setCode}
              onComplete={confirm}
              disabled={submitting}
              invalid={!!confirmError}
            />

            {confirmError && (
              <div
                role="alert"
                className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-center text-sm text-destructive"
              >
                {confirmError}
              </div>
            )}

            <Button
              type="button"
              className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              onClick={() => confirm(code)}
              disabled={submitting || code.length < 6}
            >
              {submitting && <Loader2 className="size-4 animate-spin ms-2" />}
              {submitting ? t("auth.mfaEnrollConfirming") : t("auth.mfaEnrollConfirm")}
            </Button>

            <Button
              type="button"
              variant="outline"
              className="w-full"
              onClick={() => navigate("/login", { replace: true })}
            >
              {t("auth.mfaBackToLogin")}
            </Button>

            <Button
              type="button"
              variant="outline"
              className="w-full"
              onClick={skip}
              disabled={submitting || skipping}
            >
              {skipping && <Loader2 className="size-4 animate-spin" />}
              {skipping ? t("auth.mfaSkipping") : t("auth.mfaSkip")}
            </Button>
          </div>
        )
      )}
    </AuthLayout>
  )
}
