// T141 [US6] — Industry dropdown. Options come straight from the `GET /api/v1/tenant/organization`
// response's `industry_options` (the canonical list, single source of truth) — never a hard-coded
// frontend enum (contracts/settings-api.md). Labels are localised when a translation exists, falling
// back to the canonical member name.

import { useTranslation } from "react-i18next"

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"

interface IndustryDropdownProps {
  id?: string
  value: string
  options: string[]
  onChange: (value: string) => void
  disabled?: boolean
}

export function IndustryDropdown({ id, value, options, onChange, disabled }: IndustryDropdownProps) {
  const { t } = useTranslation()
  const label = (option: string) => t(`settings.organization.industries.${option}`, option)

  return (
    <Select value={value} onValueChange={(v) => onChange(v ?? "")} disabled={disabled}>
      <SelectTrigger id={id} data-testid="organization-industry" className="w-full">
        <SelectValue placeholder={t("settings.organization.industryPlaceholder")}>
          {(v) => (v ? label(v as string) : t("settings.organization.industryPlaceholder"))}
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option} value={option} data-testid={`organization-industry-option-${option}`}>
            {label(option)}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
