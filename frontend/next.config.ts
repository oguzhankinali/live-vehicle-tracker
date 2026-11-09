import type { NextConfig } from "next";
import createNextIntlPlugin from 'next-intl/plugin';

const withNextIntl = createNextIntlPlugin('./src/i18n/request.ts');

const nextConfig: NextConfig = {
  reactStrictMode: true,
  // Docker içinde standalone server.js çıktısı üretmek için ZORUNLU satır:
  output: 'standalone',
};

export default withNextIntl(nextConfig);