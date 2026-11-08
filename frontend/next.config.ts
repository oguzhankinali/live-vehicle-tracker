import type { NextConfig } from "next";
import createNextIntlPlugin from 'next-intl/plugin';

// next-intl eklentisini i18n ayar dosyamızla başlatıyoruz
const withNextIntl = createNextIntlPlugin('./src/i18n/request.ts');

const nextConfig: NextConfig = {
  experimental: {
    agentFeedback: true,
  },
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

// Config'i next-intl ile sarmalayarak dışarı aktarıyoruz
export default withNextIntl(nextConfig);