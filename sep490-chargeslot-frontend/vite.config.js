import { defineConfig } from "vite";
import react from "@vitejs/plugin-react-swc";
import path from "path";
import tailwindcss from "@tailwindcss/vite";
// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    proxy: {
      "/api": {
        target: "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net",
        changeOrigin: true,
        secure: true,
      },
      "/hubs": {
        target: "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net",
        changeOrigin: true,
        secure: true,
        ws: true, // proxy WebSocket cho SignalR
      },
    },
  },
  build: {
    // Tăng giới hạn cảnh báo chunk size lên 800 KB (mặc định là 500 KB)
    chunkSizeWarningLimit: 800,
    rollupOptions: {
      output: {
        // Tách các thư viện lớn thành chunk riêng (Sử dụng Function Format để fix lỗi với Rolldown/Vite 6)
        manualChunks(id) {
          if (id.includes('node_modules')) {
            if (id.includes('react/') || id.includes('react-dom/') || id.includes('react-router-dom/')) {
              return 'vendor-react';
            }
            if (id.includes('leaflet/') || id.includes('react-leaflet/')) {
              return 'vendor-map';
            }
            if (id.includes('recharts/')) {
              return 'vendor-chart';
            }
            if (id.includes('zustand/') || id.includes('qrcode.react/')) {
              return 'vendor-ui';
            }
            return 'vendor';
          }
        },
      },
    },
  },
});
