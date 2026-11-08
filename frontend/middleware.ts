import createMiddleware from 'next-intl/middleware';
import { routing } from './src/i18n/routing';

// Next.js 15 App Router için kök dizinden dil yönlendirmesini (/ -> /tr) sağlayan resmi ara yazılım
export default createMiddleware(routing);

export const config = {
  // Statik dosyalar, favicon ve dahili Next.js yolları hariç tüm istekleri yakalar
  matcher: ['/((?!api|_next|_vercel|.*\\..*).*)'],
};