import createMiddleware from 'next-intl/middleware';
import { routing } from './src/i18n/routing';

export default createMiddleware(routing);

export const config = {
  // Statik dosyaları ve api yollarını hariç tutup tüm rotaları yakala
  matcher: ['/((?!api|_next|_vercel|.*\\..*).*)']
};