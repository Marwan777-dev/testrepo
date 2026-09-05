// M-10 password reset. Two modes selected by the URL:
//   • no `?token=` → request mode: enter email, we call the reset endpoint
//     (always 202, to avoid user enumeration) and show a neutral confirmation.
//   • `?token=…`   → redeem mode: set a new password (with a live complexity
//     checklist mirroring the backend PasswordValidator) and submit it.
// Rate-limited requests (429) show a cooldown; an invalid/expired token (400)
// routes the user back to requesting a fresh link.

import { type FormEvent, useEffect, useRef, useState } from "react"
import { useNavigate, useSearchParams } from "react-router"
import { useTranslation } from "react-i18next"
import { Check, Eye, EyeOff, Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { AuthApiError, redeemPasswordReset, requestPasswordReset } from "@/features/auth/api"
import { AuthLayout } from "@/features/auth/components/AuthLayout"

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const DEFAULT_RATE_LIMIT_SECONDS = 1800

// Mirrors src/Nabadat.Platform.M10/Application/Auth/PasswordValidator.cs.
const PASSWORD_RULES = [
  { key: "pwRuleLength", test: (p: string) => p.length >= 10 },
  { key: "pwRuleUpper", test: (p: string) => /[A-Z]/.test(p) },
  { key: "pwRuleLower", test: (p: string) => /[a-z]/.test(p) },
  { key: "pwRuleDigit", test: (p: string) => /[0-9]/.test(p) },
  { key: "pwRuleSpecial", test: (p: string) => /[^A-Za-z0-9]/.test(p) },
] as const

function formatCountdown(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${seconds.toString().padStart(2, "0")}`
}

function ErrorAlert({ message }: { message: string }) {
  return (
    <div
      role="alert"
      className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
    >
      {message}
    </div>
  )
}

function BackToLogin() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  return (
    <div className="text-center">
      <Button
        type="button"
        variant="link"
        size="sm"
        className="font-normal"
        onClick={() => navigate("/login")}
      >
        {t("auth.backToLogin")}
      </Button>
    </div>
  )
}

// ── Request mode ──────────────────────────────────────────────────────────────

function RequestForm() {
  const { t } = useTranslation()

  const [email, setEmail] = useState("")
  const [emailError, setEmailError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [sent, setSent] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [lockSeconds, setLockSeconds] = useState(0)
  const locked = lockSeconds > 0
  const emailRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (lockSeconds <= 0) return
    const id = setInterval(() => setLockSeconds((s) => (s <= 1 ? 0 : s - 1)), 1000)
    return () => clearInterval(id)
  }, [lockSeconds])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    const trimmed = email.trim()
    if (!trimmed) {
      setEmailError(t("auth.emailRequired"))
      emailRef.current?.focus()
      return
    }
    if (!EMAIL_RE.test(trimmed)) {
      setEmailError(t("auth.invalidEmail"))
      emailRef.current?.focus()
      return
    }
    setEmailError(null)
    setSubmitting(true)
    try {
      await requestPasswordReset(trimmed)
      setSent(true)
    } catch (err) {
      if (err instanceof AuthApiError && err.status === 429) {
        setLockSeconds(err.retryAfter ?? DEFAULT_RATE_LIMIT_SECONDS)
      } else if (err instanceof AuthApiError && err.status === 503) {
        setError(t("auth.resetUnavailable"))
      } else {
        setError(t("auth.errorGeneric"))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout title={t("auth.resetTitle")} subtitle={t("auth.resetSubtitle")}>
      {sent ? (
        <div className="space-y-4">
          <div
            role="status"
            className="rounded-md border border-border bg-muted/40 px-3 py-3 text-sm text-foreground"
          >
            {t("auth.resetSent")}
          </div>
          <BackToLogin />
        </div>
      ) : (
        <form onSubmit={handleSubmit} noValidate className="space-y-4">
          {error && <ErrorAlert message={error} />}
          {locked && <ErrorAlert message={t("auth.resetRateLimited", { time: formatCountdown(lockSeconds) })} />}

          <div className="space-y-1.5">
            <Label htmlFor="reset-email">
              {t("auth.email")} <span className="text-destructive">*</span>
            </Label>
            <Input
              ref={emailRef}
              id="reset-email"
              type="email"
              inputMode="email"
              autoComplete="email"
              dir="ltr"
              className="text-start"
              placeholder={t("auth.enterEmail")}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              aria-invalid={!!emailError}
              aria-describedby={emailError ? "reset-email-error" : undefined}
              disabled={submitting}
            />
            {emailError && (
              <p id="reset-email-error" role="alert" className="text-sm text-destructive">
                {emailError}
              </p>
            )}
          </div>

          <Button
            type="submit"
            className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            disabled={submitting || locked}
          >
            {submitting && <Loader2 className="size-4 animate-spin ms-2" />}
            {submitting ? t("auth.resetSending") : t("auth.resetSend")}
          </Button>

          <BackToLogin />
        </form>
      )}
    </AuthLayout>
  )
}

// ── Redeem mode ───────────────────────────────────────────────────────────────

function RedeemForm({ token }: { token: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const [password, setPassword] = useState("")
  const [confirm, setConfirm] = useState("")
  const [showPassword, setShowPassword] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [invalidToken, setInvalidToken] = useState(false)
  const [touched, setTouched] = useState(false)
  const passwordRef = useRef<HTMLInputElement>(null)
  const confirmRef = useRef<HTMLInputElement>(null)

  const ruleResults = PASSWORD_RULES.map((r) => ({ key: r.key, ok: r.test(password) }))
  const allPass = ruleResults.every((r) => r.ok)
  const matches = password.length > 0 && password === confirm
  const showMismatch = touched && confirm.length > 0 && !matches

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setTouched(true)
    if (!allPass) {
      passwordRef.current?.focus()
      return
    }
    if (!matches) {
      confirmRef.current?.focus()
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await redeemPasswordReset(token, password)
      navigate("/login", { replace: true })
    } catch (err) {
      if (err instanceof AuthApiError && err.status === 400) {
        setInvalidToken(true)
      } else if (err instanceof AuthApiError && err.status === 422) {
        setError(t("auth.resetWeak"))
      } else {
        setError(t("auth.errorGeneric"))
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (invalidToken) {
    return (
      <AuthLayout title={t("auth.resetNewTitle")}>
        <div className="space-y-4">
          <ErrorAlert message={t("auth.resetInvalidToken")} />
          <Button
            type="button"
            className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
            onClick={() => navigate("/auth/password-reset")}
          >
            {t("auth.resetRequestNew")}
          </Button>
          <BackToLogin />
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title={t("auth.resetNewTitle")} subtitle={t("auth.resetNewSubtitle")}>
      <form onSubmit={handleSubmit} noValidate className="space-y-4">
        {error && <ErrorAlert message={error} />}

        {/* New password */}
        <div className="space-y-1.5">
          <Label htmlFor="new-password">
            {t("auth.resetNewPassword")} <span className="text-destructive">*</span>
          </Label>
          <div className="relative">
            <Input
              ref={passwordRef}
              id="new-password"
              type={showPassword ? "text" : "password"}
              autoComplete="new-password"
              dir="ltr"
              className="text-start pe-10"
              placeholder={t("auth.resetEnterNew")}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={submitting}
            />
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="absolute inset-y-0 end-0 my-auto me-1 size-8 text-muted-foreground"
              onClick={() => setShowPassword((v) => !v)}
              aria-label={showPassword ? t("auth.hidePassword") : t("auth.showPassword")}
              disabled={submitting}
            >
              {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            </Button>
          </div>
        </div>

        {/* Complexity checklist */}
        <div className="space-y-2 rounded-md border border-border bg-muted/30 px-3 py-3">
          <p className="text-xs font-medium text-muted-foreground">{t("auth.pwRequirementsTitle")}</p>
          <ul className="space-y-1.5">
            {ruleResults.map(({ key, ok }) => (
              <li key={key} className="flex items-center gap-2 text-sm">
                {ok ? (
                  <Check className="size-4 shrink-0 text-primary" />
                ) : (
                  <span className="flex size-4 shrink-0 items-center justify-center">
                    <span className="size-1.5 rounded-full bg-muted-foreground/50" />
                  </span>
                )}
                <span className={ok ? "text-foreground" : "text-muted-foreground"}>
                  {t(`auth.${key}`)}
                </span>
              </li>
            ))}
          </ul>
        </div>

        {/* Confirm password */}
        <div className="space-y-1.5">
          <Label htmlFor="confirm-password">
            {t("auth.resetConfirmPassword")} <span className="text-destructive">*</span>
          </Label>
          <Input
            ref={confirmRef}
            id="confirm-password"
            type={showPassword ? "text" : "password"}
            autoComplete="new-password"
            dir="ltr"
            className="text-start"
            placeholder={t("auth.resetReenter")}
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            aria-invalid={showMismatch}
            aria-describedby={showMismatch ? "confirm-error" : undefined}
            disabled={submitting}
          />
          {showMismatch && (
            <p id="confirm-error" role="alert" className="text-sm text-destructive">
              {t("auth.resetMismatch")}
            </p>
          )}
        </div>

        <Button
          type="submit"
          className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
          disabled={submitting || !allPass || !matches}
        >
          {submitting && <Loader2 className="size-4 animate-spin ms-2" />}
          {submitting ? t("auth.resetSubmitting") : t("auth.resetSubmit")}
        </Button>

        <BackToLogin />
      </form>
    </AuthLayout>
  )
}

export default function PasswordResetPage() {
  const [params] = useSearchParams()
  const token = params.get("token")
  return token ? <RedeemForm token={token} /> : <RequestForm />
}
