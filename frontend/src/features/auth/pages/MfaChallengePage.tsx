// M-10 MFA challenge — step 2 of login. The 6-digit TOTP code completes the
// challenge created by `loginStep1`; on success the session token is stored and
// the user lands on the dashboard. The `challengeId` arrives via router state
// from the login page; reaching this page without one sends the user back.

import { useEffect, useState } from "react"
import { useLocation, useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import { AuthApiError, mfaSkip, mfaVerify, setSessionToken } from "@/features/auth/api"
import { AuthLayout } from "@/features/auth/components/AuthLayout"
import { OtpField } from "@/features/auth/components/OtpField"
import { useSession } from "@/features/auth/hooks/useSession"

const CODE_LENGTH = 6
const DEFAULT_LOCK_SECONDS = 300

function formatCountdown(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${seconds.toString().padStart(2, "0")}`
}

export default function MfaChallengePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const { refreshPermissions } = useSession()
  const challengeId = (location.state as { challengeId?: string } | null)?.challengeId ?? null

  const [code, setCode] = useState("")
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [skipping, setSkipping] = useState(false)
  const [lockSeconds, setLockSeconds] = useState(0)
  const locked = lockSeconds > 0

  // Reached directly (no challenge) → restart at login.
  useEffect(() => {
    if (!challengeId) navigate("/login", { replace: true })
  }, [challengeId, navigate])

  // Lockout cooldown ticker.
  useEffect(() => {
    if (lockSeconds <= 0) return
    const id = setInterval(() => {
      setLockSeconds((s) => (s <= 1 ? 0 : s - 1))
    }, 1000)
    return () => clearInterval(id)
  }, [lockSeconds])

  if (!challengeId) return null

  async function verify(value: string) {
    if (!challengeId || submitting || locked) return
    setSubmitting(true)
    setError(null)
    try {
      const session = await mfaVerify(challengeId, value)
      setSessionToken(session.sessionToken)
      // Hydrate the session context from the new token before entering the guarded
      // area — the SessionProvider mounted at /login (token-less) and won't re-read
      // sessionStorage on its own, so without this the AuthGuard bounces to /login.
      await refreshPermissions()
      navigate("/", { replace: true })
    } catch (err) {
      if (err instanceof AuthApiError && (err.code === "auth.account_locked" || err.status === 423)) {
        setLockSeconds(err.retryAfter ?? DEFAULT_LOCK_SECONDS)
      } else if (
        err instanceof AuthApiError &&
        (err.code === "auth.mfa.invalid_code" || err.status === 422)
      ) {
        setError(t("auth.mfaInvalidCode"))
      } else {
        setError(t("auth.errorGeneric"))
      }
      setCode("")
    } finally {
      setSubmitting(false)
    }
  }

  async function skip() {
    if (!challengeId || submitting || skipping) return
    setSkipping(true)
    setError(null)
    try {
      const session = await mfaSkip(challengeId)
      setSessionToken(session.sessionToken)
      await refreshPermissions()
      navigate("/", { replace: true })
    } catch {
      setError(t("auth.errorGeneric"))
    } finally {
      setSkipping(false)
    }
  }

  const message = locked ? t("auth.mfaLocked", { time: formatCountdown(lockSeconds) }) : error
  const disabled = submitting || skipping || locked

  return (
    <AuthLayout title={t("auth.mfaTitle")} subtitle={t("auth.mfaSubtitle")}>
      <div className="space-y-4">
        <OtpField
          value={code}
          onChange={setCode}
          onComplete={verify}
          disabled={disabled}
          invalid={!!message}
          autoFocus
          length={CODE_LENGTH}
        />

        {message && (
          <div
            role="alert"
            className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-center text-sm text-destructive"
          >
            {message}
          </div>
        )}

        <Button
          type="button"
          className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
          onClick={() => verify(code)}
          disabled={disabled || code.length < CODE_LENGTH}
        >
          {submitting && <Loader2 className="size-4 animate-spin ms-2" />}
          {submitting ? t("auth.mfaVerifying") : t("auth.mfaVerify")}
        </Button>

        <Button
          type="button"
          variant="outline"
          className="w-full"
          onClick={() => navigate("/login", { replace: true })}
        >
          {t("auth.mfaBackToLogin")}
        </Button>

        <div className="text-center">
          <Button
            type="button"
            variant="outline"
            className="w-full"
            onClick={skip}
            disabled={disabled}
          >
            {skipping && <Loader2 className="size-4 animate-spin" />}
            {skipping ? t("auth.mfaSkipping") : t("auth.mfaSkip")}
          </Button>
        </div>
      </div>
    </AuthLayout>
  )
}
