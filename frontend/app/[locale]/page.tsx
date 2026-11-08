import { Suspense } from 'react';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { LiveMap } from '@/features/map/components/LiveMap';
import { ConnectionBadge } from '@/features/vehicles/components/ConnectionBadge';
import { VehicleFilterBar } from '@/features/vehicles/components/VehicleFilterBar';
import { VehicleDetailCard } from '@/features/vehicles/components/VehicleDetailCard';
import { SystemStatusButton } from '@/features/system/components/SystemStatusButton';

export default async function HomePage({
  params
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('Index');

  return (
    <main className="relative w-full h-screen overflow-hidden bg-background text-foreground">
      {/* Canlı Harita */}
      <LiveMap />

      {/* Sol üstte operasyon paneli */}
      <header className="absolute top-4 left-4 z-10 flex flex-wrap items-center gap-3 bg-zinc-900/90 backdrop-blur-md px-4 py-2.5 rounded-xl shadow-xl border border-zinc-800 transition-all">
        <h1 className="text-sm font-semibold tracking-tight text-zinc-100">
          {t('title')}
        </h1>

        <div className="h-4 w-[1px] bg-zinc-800" />
        <ConnectionBadge />

        <div className="h-4 w-[1px] bg-zinc-800 hidden md:block" />
        <Suspense fallback={<div className="h-7 w-44 bg-zinc-800/50 rounded-lg animate-pulse" />}>
          <VehicleFilterBar />
        </Suspense>

        <div className="h-4 w-[1px] bg-zinc-800" />
        {/* Şartname 4.3: Sistem Durumu Butonu ve Modalı */}
        <SystemStatusButton />
      </header>

      {/* Sağ tarafta açılan canlı araç detay kartı */}
      <VehicleDetailCard />
    </main>
  );
}