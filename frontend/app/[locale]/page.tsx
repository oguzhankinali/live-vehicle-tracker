import { Suspense } from 'react';
import { getTranslations, setRequestLocale } from 'next-intl/server';

import { LiveMap } from '@/features/map/components/LiveMap';
import { ConnectionBadge } from '@/features/vehicles/components/ConnectionBadge';
import { VehicleFilterBar } from '@/features/vehicles/components/VehicleFilterBar';
import { VehicleFilterStats } from '@/features/vehicles/components/VehicleFilterStats';
import { VehicleDetailCard } from '@/features/vehicles/components/VehicleDetailCard';
import { SystemStatusButton } from '@/features/system/components/SystemStatusButton';
import { LanguageSwitcher } from '@/shared/components/LanguageSwitcher';

export default async function HomePage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);

  const t = await getTranslations('Index');

  return (
    <main className="relative h-dvh w-full overflow-hidden bg-background text-foreground">
      <LiveMap />

      <header className="absolute inset-x-3 top-3 z-10 flex max-w-[calc(100vw-1.5rem)] flex-col gap-2.5 rounded-xl border border-zinc-800 bg-zinc-900/90 p-3 shadow-xl backdrop-blur-md sm:inset-x-auto sm:left-3 sm:flex-row sm:flex-wrap sm:items-center sm:gap-3 sm:px-4 sm:py-2.5">
        <div className="flex min-w-0 flex-wrap items-center gap-2 sm:gap-3">
          <h1 className="text-xs font-semibold tracking-tight text-zinc-100 sm:text-sm">
            {t('title')}
          </h1>

          <div
            aria-hidden="true"
            className="hidden h-3.5 w-px bg-zinc-700 sm:block"
          />

          <ConnectionBadge />
        </div>

        <div className="w-full min-w-0 sm:w-auto">
          <Suspense
            fallback={
              <div className="h-9 w-full animate-pulse rounded-lg bg-zinc-800/50 sm:w-96" />
            }
          >
            <VehicleFilterBar />
          </Suspense>
        </div>

        <div className="flex min-w-0 items-center justify-between gap-2 sm:justify-start sm:gap-3">
          <div className="min-w-0 shrink-0">
            <SystemStatusButton />
          </div>

          <div className="min-w-0 sm:hidden">
            <VehicleFilterStats />
          </div>

          <div className="shrink-0">
            <Suspense
              fallback={
                <div className="h-7 w-8 animate-pulse rounded-lg bg-zinc-800/50" />
              }
            >
              <LanguageSwitcher />
            </Suspense>
          </div>
        </div>
      </header>

      <VehicleDetailCard />
    </main>
  );
}
