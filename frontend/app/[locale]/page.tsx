import { Suspense } from 'react';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { LiveMap } from '@/features/map/components/LiveMap';
import { ConnectionBadge } from '@/features/vehicles/components/ConnectionBadge';
import { VehicleFilterBar } from '@/features/vehicles/components/VehicleFilterBar';
import { VehicleDetailCard } from '@/features/vehicles/components/VehicleDetailCard';
import { SystemStatusButton } from '@/features/system/components/SystemStatusButton';
import { LanguageSwitcher } from '@/shared/components/LanguageSwitcher';

export default async function HomePage({
  params
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('Index');

  return (
    // Şartname 4.4: Mobil tarayıcı taşmalarını önlemek için h-dvh standardı
    <main className="relative w-full h-dvh overflow-hidden bg-background text-foreground">
      {/* Canlı Harita */}
      <LiveMap />

      {/* Sol üstte operasyon paneli (375px mobil ekranlara tam sığacak responsive yapı) */}
      <header className="absolute top-3 left-3 right-3 sm:right-auto z-10 flex flex-wrap items-center gap-2 sm:gap-3 bg-zinc-900/90 backdrop-blur-md p-2.5 sm:px-4 sm:py-2.5 rounded-xl shadow-xl border border-zinc-800 transition-all max-w-[calc(100vw-1.5rem)]">
        <h1 className="text-xs sm:text-sm font-semibold tracking-tight text-zinc-100">
          {t('title')}
        </h1>

        <div className="h-3.5 w-[1px] bg-zinc-800" />
        <ConnectionBadge />

        <div className="h-3.5 w-[1px] bg-zinc-800 hidden md:block" />
        <Suspense fallback={<div className="h-7 w-28 sm:w-44 bg-zinc-800/50 rounded-lg animate-pulse" />}>
          <VehicleFilterBar />
        </Suspense>

        <div className="h-3.5 w-[1px] bg-zinc-800" />
        <SystemStatusButton />

        <div className="h-3.5 w-[1px] bg-zinc-800" />
        <Suspense fallback={<div className="h-7 w-8 bg-zinc-800/50 rounded-lg animate-pulse" />}>
          <LanguageSwitcher />
        </Suspense>
      </header>

      {/* Canlı araç detay kartı (Mobilde bottom sheet, masaüstünde sağ kart) */}
      <VehicleDetailCard />
    </main>
  );
}