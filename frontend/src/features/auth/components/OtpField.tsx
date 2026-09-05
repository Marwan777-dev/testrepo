// Shared 6-digit TOTP entry used by the MFA challenge and MFA enrollment pages.
// Forces LTR so the digit boxes read left-to-right inside the RTL app, and
// renders each slot as a separate rounded box.

import { InputOTP, InputOTPGroup, InputOTPSlot } from "@/components/ui/input-otp"

interface OtpFieldProps {
  value: string
  onChange: (value: string) => void
  onComplete: (value: string) => void
  disabled?: boolean
  invalid?: boolean
  autoFocus?: boolean
  length?: number
}

export function OtpField({
  value,
  onChange,
  onComplete,
  disabled,
  invalid,
  autoFocus,
  length = 6,
}: OtpFieldProps) {
  return (
    <div className="flex justify-center" dir="ltr">
      <InputOTP
        maxLength={length}
        value={value}
        onChange={onChange}
        onComplete={onComplete}
        disabled={disabled}
        autoFocus={autoFocus}
        containerClassName="justify-center gap-2"
      >
        <InputOTPGroup className="gap-2">
          {Array.from({ length }).map((_, i) => (
            <InputOTPSlot
              key={i}
              index={i}
              aria-invalid={invalid}
              className="size-12 rounded-md border-l text-base first:rounded-l-md last:rounded-r-md"
            />
          ))}
        </InputOTPGroup>
      </InputOTP>
    </div>
  )
}
