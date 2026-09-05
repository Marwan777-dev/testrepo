import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './i18n'
import './index.css'
import App from './App.tsx'
import { loadCurrentTheme } from './lib/theme/tenant-runtime'

// Apply the tenant theme BEFORE first paint (no flash). The inline script in
// index.html already applied the cached theme synchronously; this revalidates it
// against the backend. Resilient: if GET /api/theme/current isn't implemented yet
// (or the backend is down) it's a no-op and the default index.css theme renders.
loadCurrentTheme().finally(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
})
