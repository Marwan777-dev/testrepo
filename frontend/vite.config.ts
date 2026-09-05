import path from "path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    // Tenant is identified by the subdomain (e.g. e2e.localhost, gac.localhost). Vite's
    // host check would otherwise reject these, so allow any *.localhost subdomain. The
    // leading "." matches the bare host and all of its subdomains.
    allowedHosts: [".localhost"],
    proxy: {
      // .NET backend 307-redirects HTTP→HTTPS; target HTTPS and accept the
      // self-signed dev cert (CLAUDE.md › Backend Integration §5).
      // xfwd adds X-Forwarded-Host so the backend can recover the real subdomain
      // (e.g. e2e.localhost) for tenant resolution — changeOrigin rewrites Host to
      // the target, which would otherwise strip the subdomain (AD-07 / API-02).
      "/api": {
        target: "https://localhost:7286",
        changeOrigin: true,
        secure: false,
        xfwd: true,
      },
    },
  },
});
