// T064 [US2] — two optional bilingual (EN + AR) anchor-label fields: Min Scale Description and Max
// Scale Description, ≤ 60 chars each (SRS FR-2.23a). An endpoint with both languages blank maps to
// null on save. RTL-aware; the Arabic inputs carry lang="ar".

import { useTranslation } from "react-i18next"

import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import type { BilingualText } from "@/features/kpi-management/api"

const MAX_LEN = 60

export interface ScaleEndpointDescriptionFieldsProps {
  min: BilingualText | null
  max: BilingualText | null
  onChange: (next: { min: BilingualText | null; max: BilingualText | null }) => void
  readOnly?: boolean
}

function normalize(value: BilingualText): BilingualText | null {
  return value.en.trim() === "" && value.ar.trim() === "" ? null : value
}

export function ScaleEndpointDescriptionFields({
  min,
  max,
  onChange,
  readOnly,
}: ScaleEndpointDescriptionFieldsProps) {
  const { t } = useTranslation()
  const minVal = min ?? { en: "", ar: "" }
  const maxVal = max ?? { en: "", ar: "" }

  const setMin = (patch: Partial<BilingualText>) =>
    onChange({ min: normalize({ ...minVal, ...patch }), max })
  const setMax = (patch: Partial<BilingualText>) =>
    onChange({ min, max: normalize({ ...maxVal, ...patch }) })

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <EndpointGroup
        title={t("kpi.minScaleDescription")}
        value={minVal}
        readOnly={readOnly}
        enLabel={t("kpi.english")}
        arLabel={t("kpi.arabic")}
        onChange={setMin}
        idPrefix="kpi-min-desc"
      />
      <EndpointGroup
        title={t("kpi.maxScaleDescription")}
        value={maxVal}
        readOnly={readOnly}
        enLabel={t("kpi.english")}
        arLabel={t("kpi.arabic")}
        onChange={setMax}
        idPrefix="kpi-max-desc"
      />
    </div>
  )
}

function EndpointGroup({
  title,
  value,
  enLabel,
  arLabel,
  onChange,
  readOnly,
  idPrefix,
}: {
  title: string
  value: BilingualText
  enLabel: string
  arLabel: string
  onChange: (patch: Partial<BilingualText>) => void
  readOnly?: boolean
  idPrefix: string
}) {
  return (
    <fieldset className="space-y-2">
      <legend className="text-sm font-medium text-foreground">{title}</legend>
      <div className="space-y-1.5">
        <Label htmlFor={`${idPrefix}-en`} className="text-xs text-muted-foreground">
          {enLabel}
        </Label>
        <Input
          id={`${idPrefix}-en`}
          lang="en"
          dir="ltr"
          maxLength={MAX_LEN}
          value={value.en}
          disabled={readOnly}
          onChange={(e) => onChange({ en: e.target.value })}
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor={`${idPrefix}-ar`} className="text-xs text-muted-foreground">
          {arLabel}
        </Label>
        <Input
          id={`${idPrefix}-ar`}
          lang="ar"
          dir="rtl"
          maxLength={MAX_LEN}
          value={value.ar}
          disabled={readOnly}
          onChange={(e) => onChange({ ar: e.target.value })}
        />
      </div>
    </fieldset>
  )
}
