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
});
