import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// API do CRM em desenvolvimento (dotnet run --project src/Crm.Api)
const api = 'http://localhost:5080'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
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
    // Um quarto das CPUs: 2 workers na máquina de desenvolvimento (4 núcleos e 8 threads) e 1 no runner de 2 vCPUs do CI.
    // Com o padrão do Vitest (todas menos uma), os 7 workers disputavam os 4 núcleos, tudo ficava de 4 a 6 vezes mais
    // lento, e a suíte falhava por tempo em toda rodada (de 3 a 13 testes em 19/10/2026). Por que 2 e não 4: com a máquina
    // mais ocupada, 4 workers falharam em 2 das 10 rodadas de 20/10/2026, e as falhas eram de lentidão geral, não de um
    // teste específico.
    maxWorkers: '25%',
  },
})
