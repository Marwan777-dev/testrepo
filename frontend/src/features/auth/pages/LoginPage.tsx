// M-10 login (step 1 of the MFA-gated flow). Validates email + password, calls
// `loginStep1`, and on success routes to the MFA challenge or enrollment page —
// carrying the short-lived `challengeId` forward via router state. No session
// exists yet at this point; that is created only after the MFA step.

import { type FormEvent, useRef, useState } from "react"
import { useNavigate } from "react-router"
import { useTranslation } from "react-i18next"
import { Eye, EyeOff, Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { AuthApiError, loginStep1 } from "@/features/auth/api"
import { AuthLayout } from "@/features/auth/components/AuthLayout"

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** Maps an API-05 error code to a localized message key. */
function errorKeyForCode(code: string): string {
  switch (code) {
    case "auth.invalid_credentials":
      return "auth.errorInvalidCredentials"
    case "auth.account_locked":
      return "auth.errorAccountLocked"
    default:
      return "auth.errorGeneric"
  }
}

export default function LoginPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [showPassword, setShowPassword] = useState(false)
  const [emailError, setEmailError] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const emailRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)

  function validate(): boolean {
    const trimmedEmail = email.trim()
    let emailMsg: string | null = null
    let passwordMsg: string | null = null

    if (!trimmedEmail) emailMsg = t("auth.emailRequired")
    else if (!EMAIL_RE.test(trimmedEmail)) emailMsg = t("auth.invalidEmail")
    if (!password) passwordMsg = t("auth.passwordRequired")

    setEmailError(emailMsg)
    setPasswordError(passwordMsg)

    // Auto-focus the first invalid field.
    if (emailMsg) emailRef.current?.focus()
    else if (passwordMsg) passwordRef.current?.focus()

    return !emailMsg && !passwordMsg
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)
    if (!validate()) return

    setSubmitting(true)
    try {
      const result = await loginStep1(email.trim(), password)
      const next = result.requiresMfaEnrollment ? "/auth/mfa/enroll" : "/auth/mfa"
      navigate(next, { state: { challengeId: result.challengeId }, replace: true })
    } catch (err) {
      const code = err instanceof AuthApiError ? err.code : ""
      setFormError(t(errorKeyForCode(code)))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout title={t("auth.signInTitle")} subtitle={t("auth.signInSubtitle")}>
      <form onSubmit={handleSubmit} noValidate className="space-y-4">
        {formError && (
          <div
            role="alert"
            className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            {formError}
          </div>
        )}

        {/* Email */}
        <div className="space-y-1.5">
          <Label htmlFor="email">{t("auth.email")}</Label>
          <Input
            id="email"
            ref={emailRef}
            type="email"
            inputMode="email"
            autoComplete="email"
            dir="ltr"
            className="text-start"
            placeholder={t("auth.enterEmail")}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            aria-invalid={!!emailError}
            aria-describedby={emailError ? "email-error" : undefined}
            disabled={submitting}
          />
          {emailError && (
            <p id="email-error" role="alert" className="text-sm text-destructive">
              {emailError}
            </p>
          )}
        </div>

        {/* Password */}
        <div className="space-y-1.5">
          <div className="flex items-center justify-between gap-2">
            <Label htmlFor="password">{t("auth.password")}</Label>
            <Button
              type="button"
              variant="link"
              size="sm"
              className="h-auto px-0 text-sm font-normal"
              onClick={() => navigate("/auth/password-reset")}
            >
              {t("auth.forgotPassword")}
            </Button>
          </div>
          <div className="relative">
            <Input
              id="password"
              ref={passwordRef}
              type={showPassword ? "text" : "password"}
              autoComplete="current-password"
              dir="ltr"
              className="text-start pe-10"
              placeholder={t("auth.enterPassword")}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              aria-invalid={!!passwordError}
              aria-describedby={passwordError ? "password-error" : undefined}
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
          {passwordError && (
            <p id="password-error" role="alert" className="text-sm text-destructive">
              {passwordError}
            </p>
          )}
        </div>

        <Button
          type="submit"
          className="w-full bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
          disabled={submitting}
        >
          {submitting && <Loader2 className="size-4 animate-spin ms-2" />}
          {submitting ? t("auth.signingIn") : t("auth.signIn")}
        </Button>
      </form>
    </AuthLayout>
  )
}
