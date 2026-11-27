import fbtee from "@nkzw/vite-plugin-fbtee";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, lazyPlugins } from "vite-plus";

// https://vite.dev/config/
export default defineConfig({
  envDir: "..",
  server: {
    proxy: {
      "/api": {
        target: "http://127.0.0.1:5080",
        changeOrigin: true,
      },
    },
  },
  fmt: {
    ignorePatterns: [
      ".enum_manifest.json",
      "source_strings.json",
      "src/generated/**",
      "public/maplibre-gl-worker.mjs",
      "public/maplibre-gl-shared.mjs",
      "src/translations/**",
      "translations/**",
      "tools/oxlint/anti-slop/**",
    ],
  },
  lint: {
    ignorePatterns: ["public/**", "src/generated/**", "tools/oxlint/anti-slop/**"],
    plugins: ["react", "typescript", "oxc"],
    rules: {
      "react/rules-of-hooks": "error",
      "react/only-export-components": [
        "warn",
        {
          allowConstantExport: true,
        },
      ],
      "vite-plus/prefer-vite-plus-imports": "error",
      "oxc/no-accumulating-spread": "error",
      "anti-slop/no-array-filter-map": "error",
      "anti-slop/no-reduce-accumulator-copy": "error",
      "anti-slop/no-chained-type-assertions": "error",
      "anti-slop/no-conditional-empty-object-spread": "error",
      "anti-slop/no-known-value-widening": "warn",
      "anti-slop/no-module-mocking": "error",
      "anti-slop/no-object-parameters": "error",
      "anti-slop/no-reflect-apply": "error",
      "anti-slop/no-reflect-get": "error",
      "anti-slop/no-runtime-typeof": ["warn", { allowInTypeGuards: true }],
      "anti-slop/no-shape-in-symbol-names": "warn",
      "anti-slop/no-unknown-parameters": "warn",
      "anti-slop/no-unknown-returns": "warn",
      "anti-slop/no-unknown-type-aliases": "error",
      "anti-slop/no-unsafe-dictionary-type": "warn",
      "anti-slop/no-widen-then-assert": "error",
      "anti-slop/require-readable-spacing": "warn",
      "anti-slop/require-safety-comment-for-type-assertion": "warn",
    },
    options: {
      typeAware: true,
      typeCheck: true,
    },
    jsPlugins: [
      {
        name: "vite-plus",
        specifier: "vite-plus/oxlint-plugin",
      },
      {
        name: "anti-slop",
        specifier: "./tools/oxlint/anti-slop/index.ts",
      },
    ],
  },
  plugins: lazyPlugins(() => [fbtee(), react({ compiler: true }), tailwindcss()]),
});
