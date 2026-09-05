import { useEffect } from "react"
import { useTranslation } from "react-i18next"

/**
 * Syncs document `dir` and `lang` attributes with the current i18n language.
 * Call once in a top-level component (e.g., App or layout).
 */
export function useDirection() {
  const { i18n } = useTranslation()

  useEffect(() => {
    const dir = i18n.language === "ar" ? "rtl" : "ltr"
    document.documentElement.dir = dir
    document.documentElement.lang = i18n.language
  }, [i18n.language])

  return {
    dir: i18n.language === "ar" ? "rtl" as const : "ltr" as const,
    lang: i18n.language,
    isRtl: i18n.language === "ar",
    toggleLang: () => {
      i18n.changeLanguage(i18n.language === "ar" ? "en" : "ar")
    },
  }
}
