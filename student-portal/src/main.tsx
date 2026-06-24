import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import './styles.css'

console.log('[main] Script loaded, looking for root element...');
const rootElement = document.getElementById('root');
console.log('[main] Root element:', rootElement);

if (rootElement) {
  try {
    console.log('[main] Creating React root...');
    const root = ReactDOM.createRoot(rootElement);
    console.log('[main] React root created, rendering...');
    root.render(
      <React.StrictMode>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </React.StrictMode>,
    );
    console.log('[main] Render call completed');
  } catch (err) {
    console.error('[main] Render error:', err);
  }
} else {
  console.error('[main] Root element not found!');
}
