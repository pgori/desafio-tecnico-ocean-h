import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    // Em desenvolvimento o front chama /api e o Vite repassa para o backend.
    // Assim as duas pontas compartilham a mesma origem e nao esbarram em CORS local.
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true
      }
    }
  }
})
