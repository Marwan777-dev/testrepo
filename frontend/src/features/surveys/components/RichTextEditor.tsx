// Rich-text editor for the F3 welcome / thank-you messages (T088): a framed
// contentEditable surface with an in-frame toolbar (B / I / U / Heading / List /
// Link) and a `</> HTML` source toggle — clickthrough-parity design. The server
// sanitises on every save (Q3 allowlist, policy v1) — this editor never trusts its
// own output. Toolbar buttons preventDefault on mousedown so the text selection
// survives the click (without it, exec commands silently no-op).

import { useEffect, useRef, useState } from "react"
import { useTranslation } from "react-i18next"
import { Bold, Code, Heading, Italic, Link2, List, Underline } from "lucide-react"

import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { cn } from "@/lib/utils"

export function RichTextEditor({
  id,
  label,
  hint,
  value,
  onChange,
  disabled,
}: {
  id: string
  label: string
  /** Muted helper line rendered between the label and the editor frame. */
  hint?: string
  value: string
  onChange: (html: string) => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const [sourceMode, setSourceMode] = useState(false)
  const surfaceRef = useRef<HTMLDivElement>(null)

  // Push external value into the editable surface only when it actually differs —
  // rewriting innerHTML on every keystroke would reset the caret.
  useEffect(() => {
    if (sourceMode) return
    const el = surfaceRef.current
    if (el && el.innerHTML !== value) el.innerHTML = value
  }, [value, sourceMode])

  const exec = (command: string, arg?: string) => {
    surfaceRef.current?.focus()
    // execCommand is deprecated but remains the simplest dependency-free rich text
    // primitive; output passes through the server-side sanitiser regardless.
    document.execCommand(command, false, arg)
    onChange(surfaceRef.current?.innerHTML ?? "")
  }

  const insertLink = () => {
    const url = window.prompt(t("surveysModule.editor.linkPrompt"), "https://")
    if (url) exec("createLink", url)
  }

  const TOOLS = [
    { icon: Bold, labelKey: "surveysModule.editor.bold", run: () => exec("bold") },
    { icon: Italic, labelKey: "surveysModule.editor.italic", run: () => exec("italic") },
    { icon: Underline, labelKey: "surveysModule.editor.underline", run: () => exec("underline") },
    { icon: Heading, labelKey: "surveysModule.editor.heading", run: () => exec("formatBlock", "<h3>") },
    { icon: List, labelKey: "surveysModule.editor.bulletList", run: () => exec("insertUnorderedList") },
    { icon: Link2, labelKey: "surveysModule.editor.link", run: insertLink },
  ]

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}

      <div
        className={cn(
          "overflow-hidden rounded-md border border-input bg-card",
          "focus-within:ring-2 focus-within:ring-primary focus-within:ring-offset-2 focus-within:ring-offset-background",
          disabled && "opacity-50"
        )}
      >
        {/* Toolbar */}
        <div className="flex items-center gap-0.5 border-b border-border bg-muted/40 px-2 py-1.5">
          {TOOLS.map(({ icon: Icon, labelKey, run }) => (
            <button
              key={labelKey}
              type="button"
              aria-label={t(labelKey)}
              title={t(labelKey)}
              disabled={disabled || sourceMode}
              onMouseDown={(e) => e.preventDefault()}
              onClick={run}
              className={cn(
                "inline-flex size-7 items-center justify-center rounded-sm text-muted-foreground transition-colors",
                "hover:bg-accent hover:text-foreground disabled:pointer-events-none disabled:opacity-40"
              )}
            >
              <Icon className="size-3.5" aria-hidden />
            </button>
          ))}
          <span className="flex-1" />
          <button
            type="button"
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => setSourceMode((m) => !m)}
            aria-pressed={sourceMode}
            disabled={disabled}
            className={cn(
              "inline-flex items-center gap-1 rounded-sm px-2 py-1 text-xs font-medium transition-colors",
              sourceMode
                ? "bg-primary/10 text-primary"
                : "text-muted-foreground hover:bg-accent hover:text-foreground"
            )}
          >
            <Code className="size-3.5" aria-hidden />
            {t("surveysModule.editor.htmlToggle")}
          </button>
        </div>

        {/* Body */}
        {sourceMode ? (
          <Textarea
            id={id}
            dir="ltr"
            aria-label={`${label} (HTML)`}
            className="min-h-[120px] rounded-none border-0 font-mono text-xs focus-visible:ring-0 focus-visible:ring-offset-0"
            value={value}
            onChange={(e) => onChange(e.target.value)}
            disabled={disabled}
          />
        ) : (
          <div
            ref={surfaceRef}
            id={id}
            role="textbox"
            aria-multiline="true"
            aria-label={label}
            contentEditable={!disabled}
            suppressContentEditableWarning
            onInput={() => onChange(surfaceRef.current?.innerHTML ?? "")}
            className={cn(
              "min-h-[120px] px-3 py-2.5 text-sm leading-relaxed text-foreground outline-none",
              // Formatted content must LOOK formatted inside the surface —
              // headings, lists and links render unstyled without these.
              "[&_h1]:mb-1 [&_h1]:text-xl [&_h1]:font-bold [&_h2]:mb-1 [&_h2]:text-lg [&_h2]:font-bold",
              "[&_h3]:mb-1 [&_h3]:text-base [&_h3]:font-bold [&_p]:mb-1",
              "[&_b]:font-bold [&_strong]:font-bold [&_em]:italic [&_i]:italic [&_u]:underline",
              "[&_ol]:list-decimal [&_ol]:ps-5 [&_ul]:list-disc [&_ul]:ps-5 [&_a]:text-primary [&_a]:underline",
              disabled && "pointer-events-none"
            )}
          />
        )}
      </div>
    </div>
  )
}
