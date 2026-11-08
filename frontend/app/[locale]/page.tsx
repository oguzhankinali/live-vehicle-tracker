import { Suspense } from 'react';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { LiveMap } from '../../src/features/map/components/LiveMap';
import { ConnectionBadge } from '../../src/features/vehicles/components/ConnectionBadge';
import { VehicleFilterBar } from '../../src/features/vehicles/components/VehicleFilterBar';

export default async function HomePage({
  params
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);

  const t = await getTranslations('Index');

  return (
    <main className="relative w-full h-screen overflow-hidden bg-white text-black">
      {/* Canlı Harita Bileşeni */}
      <LiveMap />

      {/* Sol üstte duran modern operasyon paneli: Başlık + Rozet + Arama Çubuğu */}
      <div className="absolute top-4 left-4 z-10 flex flex-wrap items-center gap-4 bg-white/95 dark:bg-zinc-900/95 backdrop-blur-md px-5 py-3 rounded-xl shadow-xl border border-zinc-200/80 dark:border-zinc-800 transition-all">
        <h1 className="text-lg font-semibold tracking-tight text-zinc-900 dark:text-zinc-100">
          {t('title')}
        </h1>
        <div className="h-4 w-[1px] bg-zinc-200 dark:bg-zinc-800" />
        <ConnectionBadge />
        <div className="h-4 w-[1px] bg-zinc-200 dark:bg-zinc-800 hidden md:block" />
        
        {/* Kendime not: Next.js 15 kuralı: useSearchParams kullanan bileşen Suspense içinde olmalıdır */}
        <Suspense fallback={<div className="h-7 w-44 bg-zinc-100 rounded-lg animate-pulse" />}>
          <VehicleFilterBar />
        </Suspense>
      </div>
    </main>
  );
}