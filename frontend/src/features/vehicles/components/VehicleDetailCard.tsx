'use client';

import { useTranslations } from 'next-intl';
import { useVehicleStore } from '../store/useVehicleStore';
import { VehicleStopStatus, OccupancyStatus } from '../types/vehicle';

export function VehicleDetailCard() {
  const t = useTranslations('VehicleDetail');
  const selectedVehicleId = useVehicleStore((state) => state.selectedVehicleId);
  const setSelectedVehicleId = useVehicleStore((state) => state.setSelectedVehicleId);
  
  const vehicle = useVehicleStore((state) => 
    selectedVehicleId ? state.vehicles[selectedVehicleId] : null
  );

  if (!selectedVehicleId || !vehicle) return null;

  // Hız yoksa '0 km/s' değil '-' gösterilmeli (durmak != hız verisi olmaması)
  const speedDisplay = vehicle.speed != null 
    ? `${Math.round(vehicle.speed * 3.6)} ${t('unitKmh')}` 
    : '-';

  const bearingDisplay = vehicle.bearing != null 
    ? `${Math.round(vehicle.bearing)}${t('unitDegree')}` 
    : '-';

  const getStatusText = (status: VehicleStopStatus | null) => {
    switch (status) {
      case VehicleStopStatus.IncomingAt: return t('statusIncoming');
      case VehicleStopStatus.StoppedAt: return t('statusStopped');
      case VehicleStopStatus.InTransitTo: return t('statusInTransit');
      default: return t('statusUnknown');
    }
  };

  // Tüm GTFS Occupancy enumlarını eksiksiz eşle
  const getOccupancyText = (occ: OccupancyStatus | null) => {
    switch (occ) {
      case OccupancyStatus.Empty: return t('occEmpty');
      case OccupancyStatus.ManySeatsAvailable: return t('occManySeats');
      case OccupancyStatus.FewSeatsAvailable: return t('occFewSeats');
      case OccupancyStatus.StandingRoomOnly: return t('occStanding');
      case OccupancyStatus.CrushedStandingRoomOnly: return t('occCrushedStanding');
      case OccupancyStatus.Full: return t('occFull');
      case OccupancyStatus.NotAcceptingPassengers: return t('occNotAccepting');
      case OccupancyStatus.NotBoardable: return t('occNotBoardable');
      case OccupancyStatus.NoDataAvailable:
      default: return t('occNoData');
    }
  };

  // Göreli zaman hesabı (X sn önce / X dk önce)
  const getRelativeTime = (timestampIso: string) => {
    const diffMs = Date.now() - new Date(timestampIso).getTime();
    const diffSec = Math.max(0, Math.floor(diffMs / 1000));
    if (diffSec < 60) return t('secondsAgo', { sec: diffSec });
    const diffMin = Math.floor(diffSec / 60);
    return t('minutesAgo', { min: diffMin });
  };

  return (
    <div className="absolute top-20 right-4 z-20 w-80 md:w-96 bg-white/95 dark:bg-zinc-900/95 backdrop-blur-md rounded-2xl shadow-2xl border border-zinc-200/80 dark:border-zinc-800 p-5 transition-all animate-in fade-in slide-in-from-right-4 duration-300">
      <div className="flex items-center justify-between pb-3 border-b border-zinc-200 dark:border-zinc-800">
        <div className="flex items-center gap-2">
          <div className="h-3 w-3 rounded-full bg-blue-600 animate-pulse" />
          <h2 className="text-sm font-semibold tracking-tight text-zinc-900 dark:text-zinc-100">
            {t('title')}
          </h2>
        </div>
        <button
          onClick={() => setSelectedVehicleId(null)}
          className="text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 p-1 rounded-lg hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
          aria-label={t('close')}
        >
          ✕
        </button>
      </div>

      <div className="mt-4 bg-zinc-50 dark:bg-zinc-800/50 rounded-xl p-3 border border-zinc-100 dark:border-zinc-800 flex items-center justify-between">
        <div>
          <span className="text-[10px] uppercase font-bold text-zinc-400 block tracking-wider">
            {t('vehicleId')}
          </span>
          <span className="text-base font-bold text-zinc-900 dark:text-zinc-100 font-mono">
            {vehicle.vehicleId}
          </span>
        </div>
        {vehicle.vehicleLabel && (
          <span className="px-2.5 py-1 text-xs font-semibold bg-blue-100 dark:bg-blue-950 text-blue-700 dark:text-blue-300 rounded-md">
            {vehicle.vehicleLabel}
          </span>
        )}
      </div>

      <div className="mt-4 grid grid-cols-2 gap-2 text-xs">
        <div className="p-2.5 bg-zinc-50 dark:bg-zinc-800/30 rounded-lg border border-zinc-100 dark:border-zinc-800">
          <span className="text-zinc-400 block text-[10px]">{t('route')}</span>
          <span className="font-semibold text-zinc-800 dark:text-zinc-200 truncate block mt-0.5">
            {vehicle.routeId || '-'}
          </span>
        </div>

        <div className="p-2.5 bg-zinc-50 dark:bg-zinc-800/30 rounded-lg border border-zinc-100 dark:border-zinc-800">
          <span className="text-zinc-400 block text-[10px]">{t('trip')}</span>
          <span className="font-semibold text-zinc-800 dark:text-zinc-200 truncate block mt-0.5">
            {vehicle.tripId || '-'}
          </span>
        </div>

        <div className="p-2.5 bg-zinc-50 dark:bg-zinc-800/30 rounded-lg border border-zinc-100 dark:border-zinc-800">
          <span className="text-zinc-400 block text-[10px]">{t('speed')}</span>
          <span className="font-semibold text-zinc-800 dark:text-zinc-200 block mt-0.5">
            {speedDisplay}
          </span>
        </div>

        <div className="p-2.5 bg-zinc-50 dark:bg-zinc-800/30 rounded-lg border border-zinc-100 dark:border-zinc-800">
          <span className="text-zinc-400 block text-[10px]">{t('bearing')}</span>
          <span className="font-semibold text-zinc-800 dark:text-zinc-200 block mt-0.5">
            {bearingDisplay}
          </span>
        </div>
      </div>

      <div className="mt-3 space-y-2 text-xs">
        <div className="flex items-center justify-between p-2.5 bg-zinc-50 dark:bg-zinc-800/30 rounded-lg border border-zinc-100 dark:border-zinc-800">
          <span className="text-zinc-400">{t('status')}</span>
          <span className="font-medium text-zinc-800 dark:text-zinc-200">
            {getStatusText(vehicle.currentStatus)}
          </span>
        </div>

        <div className="flex items-center justify-between p-2.5 bg-zinc-50 dark:bg-zinc-800/30 rounded-lg border border-zinc-100 dark:border-zinc-800">
          <span className="text-zinc-400">{t('occupancy')}</span>
          <span className="font-medium text-zinc-800 dark:text-zinc-200">
            {getOccupancyText(vehicle.occupancyStatus)}
          </span>
        </div>

        <div className="flex items-center justify-between p-2.5 bg-zinc-50 dark:bg-zinc-800/30 rounded-lg border border-zinc-100 dark:border-zinc-800">
          <span className="text-zinc-400">{t('lastUpdate')}</span>
          <span className="font-mono text-zinc-600 dark:text-zinc-300">
            {getRelativeTime(vehicle.timestamp)}
          </span>
        </div>
      </div>
    </div>
  );
}