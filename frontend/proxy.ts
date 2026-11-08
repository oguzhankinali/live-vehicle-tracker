import createMiddleware from 'next-intl/middleware';
import { routing } from './src/i18n/routing';

export default createMiddleware(routing);

export const config = {
  // Gereksiz statik dosyaları atlayıp sadece kök ve dil rotalarını yakalasın
  matcher: ['/', '/(tr|en)/:path*']
};