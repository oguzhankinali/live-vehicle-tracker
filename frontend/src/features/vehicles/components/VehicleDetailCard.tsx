'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import { AlertCircle, MapPin, X } from 'lucide-react';

import { useVehicleStore } from '../store/useVehicleStore';
import { useVehicleReference } from '../api/getVehicleReference';
import { VEHICLE_STALE_WARN_MS } from '../constants/timing';

import { VehicleDetailContent } from './VehicleDetailContent';
import { VehicleHistoryModal } from './VehicleHistoryModal';

export function VehicleDetailCard() {
  const t = useTranslations('VehicleDetail');

  const [historyVehicleId, setHistoryVehicleId] =
    useState<string | null>(null);

  const [now, setNow] = useState(Date.now());

  const selectedVehicleId = useVehicleStore(
    (state) => state.selectedVehicleId
  );

  const setSelectedVehicleId = useVehicleStore(
    (state) => state.setSelectedVehicleId
  );

  const clearHistoryTrail = useVehicleStore(
    (state) => state.clearHistoryTrail
  );

  const vehicle = useVehicleStore((state) =>
    selectedVehicleId
      ? state.vehicles[selectedVehicleId]
      : null
  );

  const {
    data: reference,
    isPending: isReferencePending,
  } = useVehicleReference(
    vehicle?.routeId,
    vehicle?.stopId,
    vehicle?.tripId,
    Boolean(selectedVehicleId && vehicle)
  );

  useEffect(() => {
    setHistoryVehicleId(null);
  }, [selectedVehicleId]);

  useEffect(() => {
    if (!selectedVehicleId) return;

    setNow(Date.now());

    const timer = setInterval(() => {
      setNow(Date.now());
    }, 1000);

    return () => clearInterval(timer);
  }, [selectedVehicleId]);

  if (!selectedVehicleId || !vehicle) return null;

  const timestampMs = new Date(vehicle.timestamp).getTime();

  const diffMs = Math.max(0, now - timestampMs);
  const diffSec = Math.floor(diffMs / 1000);
  const diffMin = Math.floor(diffSec / 60);

  const isStale = diffMs >= VEHICLE_STALE_WARN_MS;
  const isHistoryOpen = historyVehicleId === vehicle.vehicleId;

  const handleCloseDetail = () => {
    clearHistoryTrail();
    setHistoryVehicleId(null);
    setSelectedVehicleId(null);
  };

  return (
    <>
      <section
        aria-label={t('title')}
        className={`
          fixed inset-x-2 bottom-2 z-20
          flex h-[300px] max-h-[45dvh] min-h-0 w-auto flex-col
          overflow-hidden rounded-2xl border border-zinc-800
          bg-zinc-900/95 p-3 text-zinc-100
          shadow-2xl backdrop-blur-md
          sm:inset-auto sm:right-4 sm:top-20
          sm:h-auto sm:w-80 sm:max-h-[80dvh] sm:p-5
          md:w-96
          ${isHistoryOpen ? 'hidden sm:flex' : ''}
        `}
      >
        <div className="flex shrink-0 items-center justify-between gap-2 border-b border-zinc-800 pb-3">
          <div className="flex min-w-0 items-center gap-2">
            <span
              className={`h-3 w-3 shrink-0 rounded-full ${isStale
                  ? 'bg-amber-500'
                  : 'animate-pulse bg-blue-500'
                }`}
            />

            <h2 className="truncate text-sm font-semibold">
              {t('title')}
            </h2>
          </div>

          <button
            type="button"
            onClick={handleCloseDetail}
            aria-label={t('close')}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-zinc-400 transition-colors hover:bg-zinc-800 hover:text-zinc-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain pr-1">
          {isStale && (
            <div className="mt-3 flex items-center gap-2 rounded-lg border border-amber-500/20 bg-amber-500/10 p-2.5 text-xs text-amber-400">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>
                {t('staleWarning', { min: diffMin })}
              </span>
            </div>
          )}

          <VehicleDetailContent
            vehicle={vehicle}
            reference={reference}
            isReferencePending={isReferencePending}
            diffSec={diffSec}
            diffMin={diffMin}
          />
        </div>

        <div className="shrink-0 border-t border-zinc-800 pt-3">
          <button
            type="button"
            onClick={() => setHistoryVehicleId(vehicle.vehicleId)}
            className="flex min-h-10 w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-blue-600 to-indigo-600 px-4 py-2.5 text-xs font-semibold text-white shadow-lg shadow-blue-500/20 transition-colors hover:from-blue-700 hover:to-indigo-700"
          >
            <MapPin className="h-4 w-4 shrink-0" />
            <span>{t('historyButton')}</span>
          </button>
        </div>
      </section>

      <VehicleHistoryModal
        key={vehicle.vehicleId}
        vehicleId={vehicle.vehicleId}
        isOpen={isHistoryOpen}
        onClose={() => setHistoryVehicleId(null)}
      />
    </>
  );
}
