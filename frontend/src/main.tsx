import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import './styles/index.css'
import { loadCachedSettings } from './hooks/useSettings'

// Apply cached theme/font settings immediately to avoid flash of default theme
loadCachedSettings();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
