import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiUrl = env.VITE_API_URL || 'http://192.168.49.2:30500'

  return {
    plugins: [react()],
    server: {
      port: 3000,
      proxy: {
        '/api': {
          target: apiUrl,
          changeOrigin: true
        },
        '/hubs': {
          target: apiUrl,
          changeOrigin: true,
          ws: true
        }
      }
    },
    build: {
      outDir: 'dist',
      sourcemap: true
    }
  }
})
