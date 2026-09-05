/**
 * Client runtime for tenant theming.
 *
 * Boot path (`loadCurrentTheme`) is BACKEND-DRIVEN: the frontend calls the API
 * with a RELATIVE `/api` URL. The Vite dev proxy (and prod reverse proxy) forwards
 * it with `X-Forwarded-Host` (vite.config `xfwd:true`), so the *backend* recovers
 * the real subdomain and decides the tenant. The frontend never parses the subdomain.
 *
 * No-flash: a tiny inline script in index.html applies the cached CSS (per-origin)
 * before the bundle runs; main.tsx then `await`s loadCurrentTheme before render.
 *
 * Until the backend implements `GET /api/theme/current` this is a safe no-op
 * (a non-2xx response leaves the default index.css theme in place — never throws).
 */
import { buildThemeCss } from "./derive-theme"
import type { TenantThemeSeed } from "./tenant-theme"

export const DEFAULT_SLUG = "nabadat"

// Relative base → goes through the `/api` dev proxy / prod reverse proxy, which
// forwards the real subdomain to the backend for tenant resolution.
export const API_BASE: string =
  (import.meta.env.VITE_THEME_API as string | undefined) ?? ""

const CSS_KEY = "tenant-theme-css"
const STYLE_ID = "tenant-theme"

function injectCss(css: string | null): void {
  if (typeof document === "undefined") return
  const existing = document.getElementById(STYLE_ID) as HTMLStyleElement | null
  if (css === null) {
    existing?.remove()
    return
  }
  const tag = existing ?? document.createElement("style")
  tag.id = STYLE_ID
  tag.textContent = css
  // ALWAYS append (moves an existing tag too) so the tenant overrides sit AFTER
  // index.css in <head> and win the cascade. Updating textContent in place would
  // leave the tag before index.css → the default theme keeps overriding it.
  document.head.appendChild(tag)
}

function cacheCss(css: string | null): void {
  try {
    if (css === null) localStorage.removeItem(CSS_KEY)
    else localStorage.setItem(CSS_KEY, css)
  } catch {
    /* storage unavailable — only lose the no-flash optimization */
  }
}

/**
 * BACKEND-DRIVEN boot: ask the API which tenant this host maps to and apply it.
 * The default tenant clears the override (index.css verbatim). Caches per-origin
 * for next load's no-flash. Never throws.
 */
export async function loadCurrentTheme(): Promise<void> {
  try {
    const res = await fetch(`${API_BASE}/api/theme/current`)
    if (!res.ok) return
    const data = (await res.json()) as { slug: string } & TenantThemeSeed
    if (data.slug === DEFAULT_SLUG) {
      injectCss(null)
      cacheCss(null)
      return
    }
    const { slug: _slug, ...seed } = data
    const css = buildThemeCss(seed)
    injectCss(css)
    cacheCss(css)
  } catch {
    /* API unreachable → keep cached/default theme */
  }
}

/**
 * Dev switcher — live, session-only override by explicit slug (no caching, so a
 * reload reverts to the host-driven `loadCurrentTheme`).
 */
export async function switchTenant(slug: string): Promise<void> {
  if (slug === DEFAULT_SLUG) {
    injectCss(null)
    return
  }
  try {
    const res = await fetch(`${API_BASE}/api/tenants/${slug}/theme`)
    if (!res.ok) return
    const seed = (await res.json()) as TenantThemeSeed
    injectCss(buildThemeCss(seed))
  } catch {
    /* ignore */
  }
}
