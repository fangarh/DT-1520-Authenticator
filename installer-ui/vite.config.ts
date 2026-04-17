import { configDefaults, defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts",
    exclude: [...configDefaults.exclude, "e2e/**"],
  },
  server: {
    host: "127.0.0.1",
    port: 4181,
    proxy: {
      "/api": {
        target: "http://127.0.0.1:4180",
        changeOrigin: true,
      },
    },
  },
});
