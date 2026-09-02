import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// Configuracao de teste separada da de build para o vite.config.ts continuar
// tratando so do que interessa a aplicacao.
export default defineConfig({
  plugins: [vue()],
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.ts']
  }
})
