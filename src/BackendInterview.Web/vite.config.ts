import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Keep the D:\PV junction path instead of resolving it back to D:\C#\PV.
    preserveSymlinks: true,
  },
  server: {
    proxy: { '/api': 'http://localhost:5080' },
  },
});
