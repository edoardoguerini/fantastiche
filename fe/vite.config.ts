import { resolve } from 'node:path'
import { defineConfig } from 'vite'
import { tanstackStart } from '@tanstack/react-start/plugin/vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [
    tailwindcss(),
    tanstackStart({
      spa: { enabled: true },
      router: { routeFileIgnorePattern: '__tests__' },
    }),
    react(),
  ],
  resolve: { alias: { '@': resolve(import.meta.dirname, 'src') } },
  server: { host: '0.0.0.0', port: 6061, strictPort: true },
})
