// Invite-user dialog: email + persona, posts to `createUser`. On success it calls
// `onCreated` so the list refreshes. A 409 surfaces as an inline "already exists"
// error; other failures show a generic message. RTL-first, design-system styled.

import { type FormEvent, useRef, useState } from "react"
import { useTranslation } from "react-i18next"
import { Check, Eye, EyeOff, Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { createUser, UsersApiError, type Persona } from "@/features/users/api"

const PERSONAS: Persona[] = ["P-01", "P-02", "P-03", "P-04", "P-05", "P-06", "P-07", "P-08"]
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// Mirrors src/Nabadat.Platform.M10/Application/Auth/PasswordValidator.cs (FR-027).
const PASSWORD_RULES = [
  { key: "pwRuleLength", test: (p: string) => p.length >= 10 },
  { key: "pwRuleUpper", test: (p: string) => /[A-Z]/.test(p) },
  { key: "pwRuleLower", test: (p: string) => /[a-z]/.test(p) },
  { key: "pwRuleDigit", test: (p: string) => /[0-9]/.test(p) },
  { key: "pwRuleSpecial", test: (p: string) => /[^A-Za-z0-9]/.test(p) },
] as const

interface InviteUserDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Called after a user is successfully created (so the caller can refresh). */
  onCreated: () => void
}

export function InviteUserDialog({ open, onOpenChange, onCreated }: InviteUserDialogProps) {
  const { t } = useTranslation()
  const [email, setEmail] = useState("")
  const [persona, setPersona] = useState<Persona>("P-03")
  const [password, setPassword] = useState("")
  const [showPassword, setShowPassword] = useState(false)
  const [emailError, setEmailError] = useState<string | null>(null)
  const [passwordTouched, setPasswordTouched] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const emailRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)

  const ruleResults = PASSWORD_RULES.map((r) => ({ key: r.key, ok: r.test(password) }))
  const passwordValid = ruleResults.every((r) => r.ok)

  function reset() {
    setEmail("")
    setPersona("P-03")
    setPassword("")
    setShowPassword(false)
    setEmailError(null)
    setPasswordTouched(false)
    setFormError(null)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const trimmed = email.trim()
    if (!trimmed) {
      setEmailError(t("users.inviteEmailRequired"))
      emailRef.current?.focus()
      return
    }
    if (!EMAIL_RE.test(trimmed)) {
      setEmailError(t("users.inviteEmailInvalid"))
      emailRef.current?.focus()
      return
    }
    setEmailError(null)

    if (!passwordValid) {
      setPasswordTouched(true)
      setFormError(t("users.inviteWeakPassword"))
      passwordRef.current?.focus()
      return
    }

    setSubmitting(true)
    try {
      await createUser({ username: trimmed, persona, password })
      reset()
      onOpenChange(false)
      onCreated()
    } catch (err) {
      if (err instanceof UsersApiError && err.status === 409) {
        setFormError(t("users.inviteConflict"))
      } else if (err instanceof UsersApiError && err.status === 422) {
        setPasswordTouched(true)
        setFormError(t("users.inviteWeakPassword"))
      } else {
        setFormError(t("users.inviteFailed"))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        if (!o) reset()
        onOpenChange(o)
      }}
    >
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("users.inviteTitle")}</DialogTitle>
          <DialogDescription>{t("users.inviteDesc")}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} noValidate className="space-y-4">
          {formError && (
            <div
              role="alert"
              className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {formError}
            </div>
          )}

          <div className="space-y-1.5">
            <Label htmlFor="invite-email">
              {t("users.inviteEmail")} <span className="text-destructive">*</span>
            </Label>
            <Input
              ref={emailRef}
              id="invite-email"
              type="email"
              inputMode="email"
              dir="ltr"
              className="text-start"
              placeholder={t("users.inviteEmailPlaceholder")}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              aria-invalid={!!emailError}
              aria-describedby={emailError ? "invite-email-error" : undefined}
              disabled={submitting}
            />
            {emailError && (
              <p id="invite-email-error" role="alert" className="text-sm text-destructive">
                {emailError}
              </p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="invite-password">
              {t("users.invitePassword")} <span className="text-destructive">*</span>
            </Label>
            <div className="relative">
              <Input
                ref={passwordRef}
                id="invite-password"
                type={showPassword ? "text" : "password"}
                autoComplete="new-password"
                dir="ltr"
                className="text-start pe-10"
                placeholder={t("users.invitePasswordPlaceholder")}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                onBlur={() => setPasswordTouched(true)}
                aria-invalid={passwordTouched && !passwordValid}
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
                    <span className={ok ? "text-foreground" : "text-muted-foreground"}>{t(`auth.${key}`)}</span>
                  </li>
                ))}
              </ul>
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="invite-persona">{t("users.invitePersona")}</Label>
            <Select value={persona} onValueChange={(v) => setPersona(v as Persona)}>
              <SelectTrigger id="invite-persona" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {PERSONAS.map((p) => (
                  <SelectItem key={p} value={p}>
                    {p}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={submitting}
            >
              {t("users.cancel")}
            </Button>
            <Button
              type="submit"
              className="bg-primary hover:bg-nb-cyan-700 text-primary-foreground"
              disabled={submitting}
            >
              {submitting && <Loader2 className="size-4 animate-spin ms-2" />}
              {submitting ? t("users.inviteSubmitting") : t("users.inviteSubmit")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
