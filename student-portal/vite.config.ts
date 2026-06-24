import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist',
  },
  server: {
    port: 3001,
  },
  preview: {
    allowedHosts: true,
    port: parseInt(process.env.PORT || '4173'),
  },
})
