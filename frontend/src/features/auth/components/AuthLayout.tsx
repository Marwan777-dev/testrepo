// Shared chrome for the unauthenticated auth pages (login, MFA, password reset):
// the centered Nabadat brand lockup + a single card with a title/subtitle. Each
// page supplies only its form as `children`, so the brand presentation stays
// identical across the whole flow.

import { type ReactNode, useEffect, useState } from "react"
import { useTranslation } from "react-i18next"
import { useTheme } from "next-themes"
import { Activity, Languages, Moon, Sun } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useDirection } from "@/hooks/use-direction"

interface AuthLayoutProps {
  /** Card heading (already localized). */
  title: string
  /** Optional sub-heading under the title (already localized). */
  subtitle?: string
  children: ReactNode
}

export function AuthLayout({ title, subtitle, children }: AuthLayoutProps) {
  const { t } = useTranslation()
  const { lang, toggleLang } = useDirection()
  const { resolvedTheme, setTheme } = useTheme()

  // next-themes resolves the theme only after mount; guard so we don't flash the
  // wrong icon on first client render.
  const [mounted, setMounted] = useState(false)
  useEffect(() => setMounted(true), [])
  const isDark = resolvedTheme === "dark"

  return (
    <div className="relative flex min-h-svh items-center justify-center bg-background px-4 py-12">
      {/* Unauthenticated pages have no topbar, so surface the theme + language
          toggles here — the platform is bilingual and dark-mode overridable. */}
      <div className="absolute top-4 end-4 flex items-center gap-1">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => setTheme(isDark ? "light" : "dark")}
          aria-label={t("common.toggleTheme")}
        >
          {mounted && isDark ? <Sun className="size-4" /> : <Moon className="size-4" />}
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={toggleLang}
          className="gap-2"
          aria-label={t("common.toggleLanguage")}
        >
          <Languages className="size-4" />
          <span>{lang === "ar" ? t("common.languageNameEn") : t("common.languageNameAr")}</span>
        </Button>
      </div>

      <div className="flex w-full max-w-md flex-col items-center gap-6">
        <div className="flex flex-col items-center gap-3 text-center">
          <span className="flex size-12 items-center justify-center rounded-md bg-gradient-to-br from-nb-mint to-nb-cyan text-white shadow-sm">
            <Activity className="size-6" aria-hidden="true" />
          </span>
          <div className="space-y-1">
            <p className="font-heading text-2xl font-bold text-foreground">{t("common.appName")}</p>
            <p className="text-sm text-muted-foreground">{t("auth.brandTagline")}</p>
          </div>
        </div>

        <Card className="w-full">
          <CardHeader className="text-center">
            <CardTitle className="font-heading text-xl">{title}</CardTitle>
            {subtitle && <p className="text-sm text-muted-foreground">{subtitle}</p>}
          </CardHeader>
          <CardContent>{children}</CardContent>
        </Card>
      </div>
    </div>
  )
}
