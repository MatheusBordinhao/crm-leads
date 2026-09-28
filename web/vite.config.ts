import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// API do CRM em desenvolvimento (dotnet run --project src/Crm.Api)
const api = 'http://localhost:5080'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': api,
      '/hubs': { target: api, ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
