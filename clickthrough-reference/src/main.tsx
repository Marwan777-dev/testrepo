import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './i18n'
import './index.css'
import App from './App.tsx'
import { KpiProvider } from './contexts/kpi-context'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <KpiProvider>
      <App />
    </KpiProvider>
  </StrictMode>,
)
  