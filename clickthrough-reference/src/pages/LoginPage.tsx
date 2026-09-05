import { useState, useRef, useCallback } from "react"
import { useTranslation } from "react-i18next"
import { useDirection } from "@/hooks/use-direction"
import { useAuth } from "@/contexts/auth-context"
import { cn } from "@/lib/utils"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Button } from "@/components/ui/button"
import { Eye, EyeOff, Loader2, Globe, Lock } from "lucide-react"

// ─── Animations ────────────────────────────────────────────

const styles = `
  @keyframes login-float-1 {
    0%, 100% { transform: translate(0, 0) scale(1); }
    33% { transform: translate(30px, -40px) scale(1.08); }
    66% { transform: translate(-20px, 20px) scale(0.95); }
  }
  @keyframes login-float-2 {
    0%, 100% { transform: translate(0, 0) scale(1); }
    33% { transform: translate(-40px, 30px) scale(1.05); }
    66% { transform: translate(25px, -35px) scale(0.92); }
  }
  @keyframes login-float-3 {
    0%, 100% { transform: translate(0, 0) scale(1); }
    50% { transform: translate(20px, 25px) scale(1.12); }
  }
  @keyframes login-fade-in {
    from { opacity: 0; transform: translateY(16px); }
    to { opacity: 1; transform: translateY(0); }
  }
  @keyframes login-pulse-ring {
    0% { transform: scale(0.95); opacity: 0.6; }
    50% { transform: scale(1.05); opacity: 1; }
    100% { transform: scale(0.95); opacity: 0.6; }
  }
  .login-animate-in {
    animation: login-fade-in 0.5s ease-out both;
  }
  .login-animate-in-delay-1 {
    animation: login-fade-in 0.5s ease-out 0.1s both;
  }
  .login-animate-in-delay-2 {
    animation: login-fade-in 0.5s ease-out 0.2s both;
  }
  .login-animate-in-delay-3 {
    animation: login-fade-in 0.5s ease-out 0.3s both;
  }
`

// ─── Email validation ──────────────────────────────────────

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// ─── Component ─────────────────────────────────────────────

export default function LoginPage() {
  const { t } = useTranslation()
  const { toggleLang, isRtl } = useDirection()
  const auth = useAuth()

  // ── State ──────────────────────────────────────────────
  const [step, setStep] = useState<"credentials" | "mfa">("credentials")
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [showPassword, setShowPassword] = useState(false)
  const [emailError, setEmailError] = useState("")
  const [loading, setLoading] = useState(false)
  const [otp, setOtp] = useState<string[]>(["", "", "", "", "", ""])

  // ── OTP refs ───────────────────────────────────────────
  const otpRefs = useRef<(HTMLInputElement | null)[]>([])

  // ── Handlers ───────────────────────────────────────────

  const validateEmail = useCallback(
    (value: string) => {
      if (value && !EMAIL_REGEX.test(value)) {
        setEmailError(t("cx.loginEmailError"))
      } else {
        setEmailError("")
      }
    },
    [t],
  )

  const handleSignIn = useCallback(() => {
    if (!email || !password) return
    if (!EMAIL_REGEX.test(email)) {
      setEmailError(t("cx.loginEmailError"))
      return
    }
    setLoading(true)
    setTimeout(() => {
      setLoading(false)
      setStep("mfa")
    }, 1200)
  }, [email, password, t])

  const handleMfaVerify = useCallback(() => {
    setLoading(true)
    setTimeout(() => {
      setLoading(false)
      auth.login()
    }, 1000)
  }, [auth])

  const handleOtpChange = useCallback(
    (index: number, value: string) => {
      // Only allow single digits
      if (value && !/^\d$/.test(value)) return

      const next = [...otp]
      next[index] = value
      setOtp(next)

      // Auto-focus next input
      if (value && index < 5) {
        otpRefs.current[index + 1]?.focus()
      }
    },
    [otp],
  )

  const handleOtpKeyDown = useCallback(
    (index: number, e: React.KeyboardEvent<HTMLInputElement>) => {
      if (e.key === "Backspace" && !otp[index] && index > 0) {
        otpRefs.current[index - 1]?.focus()
      }
    },
    [otp],
  )

  const handleOtpPaste = useCallback((e: React.ClipboardEvent) => {
    e.preventDefault()
    const pasted = e.clipboardData.getData("text").replace(/\D/g, "").slice(0, 6)
    if (!pasted) return
    const next = ["", "", "", "", "", ""]
    for (let i = 0; i < pasted.length; i++) {
      next[i] = pasted[i]
    }
    setOtp(next)
    const focusIdx = Math.min(pasted.length, 5)
    otpRefs.current[focusIdx]?.focus()
  }, [])

  const isSignInDisabled = !email || !password || loading
  const isOtpComplete = otp.every((d) => d !== "")

  // ── Render ─────────────────────────────────────────────

  return (
    <>
      <style>{styles}</style>

      <div className="flex min-h-screen w-full bg-background">
        {/* ─── Left Panel — Brand ──────────────────────────── */}
        <div
          className={cn(
            "relative hidden w-[42%] overflow-hidden lg:flex lg:flex-col lg:items-center lg:justify-center",
            "bg-gradient-to-br from-nb-mint via-nb-cyan to-nb-cyan-800",
          )}
        >
          {/* Decorative floating circles */}
          <div
            className="absolute top-[12%] start-[10%] size-64 rounded-full bg-white/10"
            style={{ animation: "login-float-1 14s ease-in-out infinite" }}
          />
          <div
            className="absolute bottom-[18%] end-[8%] size-48 rounded-full bg-white/10"
            style={{ animation: "login-float-2 18s ease-in-out infinite" }}
          />
          <div
            className="absolute top-[55%] start-[55%] size-32 rounded-full bg-white/10"
            style={{ animation: "login-float-3 10s ease-in-out infinite" }}
          />
          <div
            className="absolute bottom-[8%] start-[25%] size-20 rounded-full bg-white/10"
            style={{ animation: "login-float-1 12s ease-in-out infinite 2s" }}
          />
          <div
            className="absolute top-[8%] end-[20%] size-16 rounded-full bg-white/10"
            style={{ animation: "login-float-2 16s ease-in-out infinite 1s" }}
          />

          {/* Brand content */}
          <div className="relative z-10 flex flex-col items-center gap-6 px-8 text-center text-white">
            {/* Logo icon */}
            <div
              className="flex size-24 items-center justify-center rounded-2xl bg-white/20 backdrop-blur-sm"
              style={{ animation: "login-pulse-ring 3s ease-in-out infinite" }}
            >
              <span className="text-5xl font-heading font-bold leading-none">
                ن
              </span>
            </div>

            {/* App name */}
            <div className="login-animate-in">
              <h1 className="text-4xl font-heading font-bold tracking-tight">
                {t("common.appName")}
              </h1>
              <p className="mt-3 text-lg text-white/80 leading-relaxed max-w-xs">
                {t("cx.loginTagline")}
              </p>
            </div>
          </div>
        </div>

        {/* ─── Right Panel — Form ──────────────────────────── */}
        <div className="relative flex w-full flex-col items-center justify-center px-6 py-12 lg:w-[58%]">
          {/* Language toggle */}
          <Button
            variant="outline"
            size="sm"
            onClick={toggleLang}
            className="absolute top-4 end-4 gap-1.5"
            aria-label={isRtl ? "Switch to English" : "التبديل للعربية"}
          >
            <Globe className="size-4" />
            {t("cx.switchLang")}
          </Button>

          {/* ── Step 1: Credentials ───────────────────────── */}
          {step === "credentials" && (
            <div className="w-full max-w-sm login-animate-in">
              {/* Mobile logo (hidden on desktop since left panel shows it) */}
              <div className="mb-8 flex flex-col items-center lg:hidden">
                <div className="flex size-16 items-center justify-center rounded-xl bg-primary text-primary-foreground mb-3">
                  <span className="text-3xl font-heading font-bold leading-none">
                    ن
                  </span>
                </div>
                <span className="text-xl font-heading font-bold text-foreground">
                  {t("common.appName")}
                </span>
              </div>

              {/* Heading */}
              <div className="mb-8 text-center lg:text-start">
                <h1 className="text-2xl font-heading font-bold text-foreground login-animate-in">
                  {t("cx.loginTitle")}
                </h1>
                <p className="mt-2 text-sm text-muted-foreground login-animate-in-delay-1">
                  {t("cx.loginWelcome")}
                </p>
              </div>

              {/* Form */}
              <form
                onSubmit={(e) => {
                  e.preventDefault()
                  handleSignIn()
                }}
                className="space-y-4 login-animate-in-delay-2"
              >
                {/* Email field */}
                <div className="space-y-2">
                  <Label htmlFor="login-email">{t("cx.loginEmail")}</Label>
                  <Input
                    id="login-email"
                    type="email"
                    autoComplete="email"
                    placeholder={t("auth.enterEmail")}
                    value={email}
                    onChange={(e) => {
                      setEmail(e.target.value)
                      if (emailError) setEmailError("")
                    }}
                    onBlur={() => validateEmail(email)}
                    aria-invalid={!!emailError}
                    aria-describedby={emailError ? "email-error" : undefined}
                    className="h-10"
                  />
                  {emailError && (
                    <p
                      id="email-error"
                      className="text-sm text-destructive mt-1"
                      role="alert"
                    >
                      {emailError}
                    </p>
                  )}
                </div>

                {/* Password field */}
                <div className="space-y-2">
                  <Label htmlFor="login-password">{t("cx.loginPassword")}</Label>
                  <div className="relative">
                    <Input
                      id="login-password"
                      type={showPassword ? "text" : "password"}
                      autoComplete="current-password"
                      placeholder={t("auth.enterPassword")}
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      className="h-10 pe-10"
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      onClick={() => setShowPassword((prev) => !prev)}
                      className="absolute top-1/2 end-1 -translate-y-1/2 size-8 text-muted-foreground hover:text-foreground"
                      aria-label={showPassword ? "Hide password" : "Show password"}
                    >
                      {showPassword ? (
                        <EyeOff className="size-4" />
                      ) : (
                        <Eye className="size-4" />
                      )}
                    </Button>
                  </div>
                </div>

                {/* Forgot password */}
                <div className="flex justify-end">
                  <button
                    type="button"
                    className="text-sm text-secondary hover:text-secondary/80 transition-colors font-medium"
                  >
                    {t("cx.loginForgot")}
                  </button>
                </div>

                {/* Sign in button */}
                <Button
                  type="submit"
                  disabled={isSignInDisabled}
                  className="w-full h-10 bg-primary hover:bg-nb-cyan-700 text-primary-foreground text-base font-bold"
                >
                  {loading ? (
                    <>
                      <Loader2 className="size-4 animate-spin ms-2" />
                      {t("cx.loginSigningIn")}
                    </>
                  ) : (
                    t("cx.loginSignIn")
                  )}
                </Button>
              </form>
            </div>
          )}

          {/* ── Step 2: MFA ───────────────────────────────── */}
          {step === "mfa" && (
            <div className="w-full max-w-sm login-animate-in">
              <div className="flex flex-col items-center text-center mb-8">
                <div className="flex size-16 items-center justify-center rounded-full bg-muted mb-4 login-animate-in">
                  <Lock className="size-8 text-muted-foreground" />
                </div>
                <h2 className="text-2xl font-heading font-bold text-foreground login-animate-in-delay-1">
                  {t("cx.loginMfaTitle")}
                </h2>
                <p className="mt-2 text-sm text-muted-foreground leading-relaxed login-animate-in-delay-2">
                  {t("cx.loginMfaDesc")}
                </p>
              </div>

              {/* OTP inputs — always LTR for digit entry */}
              <div
                className="flex justify-center gap-2 mb-6 login-animate-in-delay-2"
                dir="ltr"
                onPaste={handleOtpPaste}
              >
                {otp.map((digit, idx) => (
                  <Input
                    key={idx}
                    ref={(el) => {
                      otpRefs.current[idx] = el
                    }}
                    type="text"
                    inputMode="numeric"
                    maxLength={1}
                    value={digit}
                    onChange={(e) => handleOtpChange(idx, e.target.value)}
                    onKeyDown={(e) => handleOtpKeyDown(idx, e)}
                    className="w-12 h-14 text-center text-xl font-bold px-0"
                    aria-label={`Digit ${idx + 1}`}
                  />
                ))}
              </div>

              {/* Actions */}
              <div className="space-y-3 login-animate-in-delay-3">
                <Button
                  onClick={handleMfaVerify}
                  disabled={!isOtpComplete || loading}
                  className="w-full h-10 bg-primary hover:bg-nb-cyan-700 text-primary-foreground text-base font-bold"
                >
                  {loading ? (
                    <>
                      <Loader2 className="size-4 animate-spin ms-2" />
                      {t("cx.loginVerifying")}
                    </>
                  ) : (
                    t("cx.loginVerify")
                  )}
                </Button>

                <Button
                  variant="secondary"
                  onClick={() => auth.login()}
                  className="w-full h-10"
                >
                  {t("cx.loginSkipMfa")}
                </Button>
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  )
}
