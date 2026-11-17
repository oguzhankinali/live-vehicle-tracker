'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { AlertCircle, MapPin, X } from 'lucide-react';
import { useVehicleStore } from '../store/useVehicleStore';
import { VehicleStopStatus, OccupancyStatus } from '../types/vehicle';
import { useVehicleReference } from '../api/getVehicleReference';
import { VEHICLE_STALE_WARN_MS } from '../constants/timing';
import { VehicleHistoryModal } from './VehicleHistoryModal';

export function VehicleDetailCard() {
  const t = useTranslations('VehicleDetail');

  const [historyVehicleId, setHistoryVehicleId] =
    useState<string | null>(null);
  const [now, setNow] = useState<number>(Date.now());

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
    Boolean(selectedVehicleId && vehicle)
  );

  useEffect(() => {
    setHistoryVehicleId(null);
  }, [selectedVehicleId]);

  useEffect(() => {
    if (!selectedVehicleId) return;

    setNow(Date.now());

    const timer = setInterval(
      () => setNow(Date.now()),
      1000
    );

    return () => clearInterval(timer);
  }, [selectedVehicleId]);

  if (!selectedVehicleId || !vehicle) return null;

  const vehicleTimestamp = new Date(vehicle.timestamp).getTime();
  const diffMs = Math.max(0, now - vehicleTimestamp);
  const diffSec = Math.floor(diffMs / 1000);
  const diffMin = Math.floor(diffSec / 60);
  const isStale = diffMs >= VEHICLE_STALE_WARN_MS;

  const speedDisplay =
    vehicle.speed != null
      ? `${Math.round(vehicle.speed * 3.6)} ${t('unitKmh')}`
      : '-';

  const bearingDisplay =
    vehicle.bearing != null
      ? `${Math.round(vehicle.bearing)}${t('unitDegree')}`
      : '-';

  // Entur isimleri gelmezse mevcut teknik kimlikleri koru.
  const routeDisplay =
    reference?.line?.publicCode ||
    vehicle.routeId ||
    '-';

  const routeName = reference?.line?.name;
  const operatorName = reference?.line?.operatorName;

  const stopDisplay =
    reference?.stop?.name ||
    reference?.stop?.stopPlaceName ||
    vehicle.stopId ||
    '-';

  const getStatusText = (
    status: VehicleStopStatus | null | undefined
  ) => {
    switch (status) {
      case VehicleStopStatus.IncomingAt:
        return t('statusIncoming');
      case VehicleStopStatus.StoppedAt:
        return t('statusStopped');
      case VehicleStopStatus.InTransitTo:
        return t('statusInTransit');
      default:
        return t('statusUnknown');
    }
  };

  const getOccupancyText = (
    occ: OccupancyStatus | null | undefined
  ) => {
    switch (occ) {
      case OccupancyStatus.Empty:
        return t('occEmpty');
      case OccupancyStatus.ManySeatsAvailable:
        return t('occManySeats');
      case OccupancyStatus.FewSeatsAvailable:
        return t('occFewSeats');
      case OccupancyStatus.StandingRoomOnly:
        return t('occStanding');
      case OccupancyStatus.CrushedStandingRoomOnly:
        return t('occCrushedStanding');
      case OccupancyStatus.Full:
        return t('occFull');
      case OccupancyStatus.NotAcceptingPassengers:
        return t('occNotAccepting');
      case OccupancyStatus.NotBoardable:
        return t('occNotBoardable');
      case OccupancyStatus.NoDataAvailable:
      default:
        return t('occNoData');
    }
  };

  const getRelativeTimeString = () => {
    if (diffSec < 60) {
      return t('secondsAgo', { sec: diffSec });
    }

    return t('minutesAgo', { min: diffMin });
  };

  const handleCloseDetail = () => {
    clearHistoryTrail();
    setHistoryVehicleId(null);
    setSelectedVehicleId(null);
  };

  return (
    <>
      <div className="fixed inset-x-2 bottom-2 sm:inset-auto sm:top-20 sm:right-4 z-20 w-auto sm:w-80 md:w-96 max-h-[80dvh] overflow-y-auto bg-zinc-900/95 backdrop-blur-md rounded-2xl shadow-2xl border border-zinc-800 p-4 sm:p-5 transition-all animate-in fade-in slide-in-from-bottom-4 sm:slide-in-from-right-4 duration-300">
        <div className="flex items-center justify-between pb-3 border-b border-zinc-800">
          <div className="flex items-center gap-2">
            <div
              className={`h-3 w-3 rounded-full ${isStale
                  ? 'bg-amber-500'
                  : 'bg-blue-500 animate-pulse'
                }`}
            />
            <h2 className="text-sm font-semibold tracking-tight text-zinc-100">
              {t('title')}
            </h2>
          </div>

          <button
            onClick={handleCloseDetail}
            className="text-zinc-400 hover:text-zinc-200 p-1.5 rounded-lg hover:bg-zinc-800 transition-colors"
            aria-label={t('close')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {isStale && (
          <div className="mt-3 flex items-center gap-2 p-2.5 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-400 text-xs">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>{t('staleWarning', { min: diffMin })}</span>
          </div>
        )}

        <div className="mt-4 bg-zinc-800/50 rounded-xl p-3 border border-zinc-800 flex items-center justify-between">
          <div>
            <span className="text-[10px] uppercase font-bold text-zinc-400 block tracking-wider">
              {t('vehicleId')}
            </span>
            <span className="text-base font-bold text-zinc-100 font-mono">
              {vehicle.vehicleId}
            </span>
          </div>

          {vehicle.vehicleLabel && (
            <span className="px-2.5 py-1 text-xs font-semibold bg-blue-950 text-blue-300 rounded-md">
              {vehicle.vehicleLabel}
            </span>
          )}
        </div>

        <div className="mt-4 grid grid-cols-2 gap-2 text-xs">
          <div className="p-2.5 bg-zinc-800/30 rounded-lg border border-zinc-800">
            <span className="text-zinc-400 block text-[11px]">
              {t('route')}
            </span>
            <span
              className="font-semibold text-zinc-200 block mt-0.5 truncate"
              title={vehicle.routeId || undefined}
            >
              {routeDisplay}
            </span>
          </div>

          <div className="p-2.5 bg-zinc-800/30 rounded-lg border border-zinc-800">
            <span className="text-zinc-400 block text-[11px]">
              {t('trip')}
            </span>
            <span
              className="font-semibold text-zinc-200 truncate block mt-0.5"
              title={vehicle.tripId || undefined}
            >
              {vehicle.tripId || '-'}
            </span>
          </div>

          <div className="p-2.5 bg-zinc-800/30 rounded-lg border border-zinc-800">
            <span className="text-zinc-400 block text-[11px]">
              {t('speed')}
            </span>
            <span className="font-semibold text-zinc-200 block mt-0.5">
              {speedDisplay}
            </span>
          </div>

          <div className="p-2.5 bg-zinc-800/30 rounded-lg border border-zinc-800">
            <span className="text-zinc-400 block text-[11px]">
              {t('bearing')}
            </span>
            <span className="font-semibold text-zinc-200 block mt-0.5">
              {bearingDisplay}
            </span>
          </div>
        </div>

        {/* Hat adi, operator ve durak bilgileri */}
        <div className="mt-3 space-y-2 text-xs">
          {routeName && (
            <div className="p-2.5 bg-blue-950/30 rounded-lg border border-blue-900/40">
              <span className="text-blue-300 block text-[11px]">
                {t('lineName')}
              </span>
              <span className="font-semibold text-zinc-100 block mt-1 break-words">
                {routeName}
              </span>
            </div>
          )}

          {operatorName && (
            <div className="flex items-start justify-between gap-3 p-2.5 bg-zinc-800/30 rounded-lg border border-zinc-800">
              <span className="text-zinc-400 shrink-0">
                {t('operator')}
              </span>
              <span className="font-medium text-zinc-200 text-right break-words">
                {operatorName}
              </span>
            </div>
          )}

          {(vehicle.stopId || reference?.stop) && (
            <div className="flex items-start justify-between gap-3 p-2.5 bg-zinc-800/30 rounded-lg border border-zinc-800">
              <span className="text-zinc-400 shrink-0">
                {t('stop')}
              </span>
              <span
                className="font-medium text-zinc-200 text-right break-words"
                title={vehicle.stopId || undefined}
              >
                {stopDisplay}
              </span>
            </div>
          )}

          {isReferencePending &&
            Boolean(vehicle.routeId || vehicle.stopId) && (
              <span className="text-[11px] text-zinc-500 block px-1">
                {t('referenceLoading')}
              </span>
            )}
        </div>

        <div className="mt-3 space-y-2 text-xs">
          <div className="flex items-center justify-between p-2.5 bg-zinc-800/30 rounded-lg border border-zinc-800">
            <span className="text-zinc-400">
              {t('status')}
            </span>
            <span className="font-medium text-zinc-200">
              {getStatusText(vehicle.currentStatus)}
            </span>
          </div>

          <div className="flex items-center justify-between p-2.5 bg-zinc-800/30 rounded-lg border border-zinc-800">
            <span className="text-zinc-400">
              {t('occupancy')}
            </span>
            <span className="font-medium text-zinc-200">
              {getOccupancyText(vehicle.occupancyStatus)}
            </span>
          </div>

          <div className="flex items-center justify-between p-2.5 bg-zinc-800/30 rounded-lg border border-zinc-800">
            <span className="text-zinc-400">
              {t('lastUpdate')}
            </span>
            <span className="font-mono text-zinc-300">
              {getRelativeTimeString()}
            </span>
          </div>
        </div>

        <button
          onClick={() => setHistoryVehicleId(vehicle.vehicleId)}
          className="mt-4 w-full py-2.5 px-4 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-700 hover:to-indigo-700 text-white text-xs font-semibold rounded-xl shadow-lg shadow-blue-500/20 transition-all flex items-center justify-center gap-2"
        >
          <MapPin className="h-4 w-4" />
          <span>{t('historyButton')}</span>
        </button>
      </div>

      <VehicleHistoryModal
        key={vehicle.vehicleId}
        vehicleId={vehicle.vehicleId}
        isOpen={historyVehicleId === vehicle.vehicleId}
        onClose={() => setHistoryVehicleId(null)}
      />
    </>
  );
}
