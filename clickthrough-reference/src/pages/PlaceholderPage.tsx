import { useTranslation } from "react-i18next"
import { Lock } from "lucide-react"

export default function PlaceholderPage({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-col items-center justify-center py-32 text-center px-4">
      <div className="flex size-16 items-center justify-center rounded-2xl bg-muted mb-6">
        <Lock className="size-7 text-muted-foreground" />
      </div>
      <h2 className="text-lg font-heading font-bold mb-2">{t(titleKey)}</h2>
      <p className="text-sm text-muted-foreground max-w-sm leading-relaxed">
        {t("cx.comingSoonDesc")}
      </p>
    </div>
  )
}
