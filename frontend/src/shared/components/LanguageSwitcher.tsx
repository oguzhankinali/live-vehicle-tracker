'use client';

import { useLocale } from 'next-intl';
import { useRouter, usePathname } from '@/i18n/routing';
import { useSearchParams } from 'next/navigation';

export function LanguageSwitcher() {
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const toggleLocale = () => {
    const nextLocale = locale === 'tr' ? 'en' : 'tr';
    const query = searchParams.toString();
    const target = query ? `${pathname}?${query}` : pathname;
    router.replace(target, { locale: nextLocale });
  };

  return (
    <button
      onClick={toggleLocale}
      className="px-2.5 py-1 text-xs font-semibold rounded-lg bg-zinc-800/80 hover:bg-zinc-700 text-zinc-200 border border-zinc-700 transition-colors uppercase font-mono tracking-wider"
      aria-label="Dil değiştir / Switch language"
    >
      {locale === 'tr' ? 'EN' : 'TR'}
    </button>
  );
}