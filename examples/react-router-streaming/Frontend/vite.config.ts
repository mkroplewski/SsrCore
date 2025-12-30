import { reactRouter } from "@react-router/dev/vite";
import tailwindcss from "@tailwindcss/vite";
import tsconfigPaths from "vite-tsconfig-paths";
import { defineConfig } from 'vite'

export default defineConfig(({ isSsrBuild }) => ({
  plugins: [tailwindcss(), reactRouter(), tsconfigPaths()],
  build:{
    emptyOutDir: true,
    outDir: "../wwwroot",
    rollupOptions: isSsrBuild
      ? {
          input: "./server/app.ts",
          output: {
            entryFileNames: '[name].mjs',
            chunkFileNames: 'assets/[name]-[hash].mjs',
            assetFileNames: 'assets/[name]-[hash][extname]',
          },
        }
      : undefined,
  },
  ssr: {
    noExternal: true
  }
}));
