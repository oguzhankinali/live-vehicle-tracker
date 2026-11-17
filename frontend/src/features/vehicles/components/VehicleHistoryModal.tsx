'use client';

import { useEffect, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { X } from 'lucide-react';

import { useVehicleHistory, useVehicleTrail } from '../api/getVehicleHistory';
import { useVehicleStore } from '../store/useVehicleStore';
import { VehicleHistoryTable } from './VehicleHistoryTable';

interface VehicleHistoryModalProps {
  vehicleId: string;
  isOpen: boolean;
  onClose: () => void;
}

type RangeOption = '30m' | '1h' | '3h';

const RANGE_MS = {
  '30m': 30 * 60 * 1000,
  '1h': 60 * 60 * 1000,
  '3h': 3 * 60 * 60 * 1000,
} as const;

const RANGE_OPTIONS: RangeOption[] = ['30m', '1h', '3h'];

export function VehicleHistoryModal({
  vehicleId,
  isOpen,
  onClose,
}: VehicleHistoryModalProps) {
  const t = useTranslations('History');

  const historyTrailPoints = useVehicleStore(
    (state) => state.historyTrailPoints
  );

  const setHistoryTrailPoints = useVehicleStore(
    (state) => state.setHistoryTrailPoints
  );

  const clearHistoryTrail = useVehicleStore(
    (state) => state.clearHistoryTrail
  );

  const [selectedRange, setSelectedRange] = useState<RangeOption>('1h');
  const [currentPage, setCurrentPage] = useState(1);

  const { fromIso, toIso } = useMemo(() => {
    const nowMs = Math.floor(Date.now() / 1000) * 1000;

    return {
      fromIso: new Date(nowMs - RANGE_MS[selectedRange]).toISOString(),
      toIso: new Date(nowMs).toISOString(),
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedRange, isOpen]);

  const {
    data,
    isLoading,
    isError,
    refetch,
  } = useVehicleHistory(
    {
      vehicleId,
      from: fromIso,
      to: toIso,
      page: currentPage,
      pageSize: 50,
    },
    isOpen
  );

  const { data: trailPoints } = useVehicleTrail(
    {
      vehicleId,
      from: fromIso,
      to: toIso,
    },
    isOpen
  );

  useEffect(() => {
    if (isOpen && trailPoints && trailPoints.length > 0) {
      setHistoryTrailPoints(trailPoints);
    }
  }, [isOpen, trailPoints, setHistoryTrailPoints]);

  const handleClose = () => {
    clearHistoryTrail();
    onClose();
  };

  const handleRangeChange = (range: RangeOption) => {
    setSelectedRange(range);
    setCurrentPage(1);
  };

  if (!isOpen) return null;

  const totalPoints =
    historyTrailPoints.length > 0
      ? historyTrailPoints.length
      : (trailPoints?.length ?? 0);

  return (
    <section
      aria-label={t('title')}
      className="fixed inset-x-2 bottom-2 z-30 flex max-h-[calc(100dvh-1rem)] min-h-0 w-auto max-w-[calc(100vw-1rem)] flex-col rounded-2xl border border-zinc-800 bg-zinc-900/95 p-3 text-zinc-100 shadow-2xl backdrop-blur-md animate-in fade-in slide-in-from-bottom-4 duration-300 sm:inset-x-auto sm:bottom-6 sm:left-6 sm:h-[380px] sm:w-[520px] sm:p-5"
    >
      <div className="flex min-w-0 shrink-0 items-start justify-between gap-2 border-b border-zinc-800 pb-2">
        <div className="min-w-0 flex-1">
          <h2 className="text-sm font-bold text-zinc-100">
            {t('title')}
          </h2>

          <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className="max-w-full rounded-md bg-blue-950 px-2 py-0.5 font-mono text-[10px] text-blue-400 [overflow-wrap:anywhere]">
              {vehicleId}
            </span>

            {totalPoints > 0 && (
              <span className="font-mono text-[10px] text-zinc-400">
                {t('pointsCount', { count: totalPoints })}
              </span>
            )}
          </div>

          <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-[10px]">
            <span className="flex items-center gap-1 text-emerald-400">
              <span className="h-2 w-2 rounded-full bg-emerald-500" />
              {t('startPoint')}
            </span>

            <span aria-hidden="true" className="text-zinc-500">
              →
            </span>

            <span className="flex items-center gap-1 text-rose-400">
              <span className="h-2 w-2 rounded-full bg-rose-500" />
              {t('endPoint')}
            </span>
          </div>
        </div>

        <button
          type="button"
          onClick={handleClose}
          aria-label={t('close')}
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg text-zinc-400 transition-colors hover:bg-zinc-800 hover:text-zinc-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      <div className="my-2 shrink-0 space-y-1 sm:flex sm:items-center sm:gap-2 sm:space-y-0">
        <span className="block text-[11px] font-medium text-zinc-400">
          {t('timeRange')}:
        </span>

        <div className="grid min-w-0 grid-cols-3 gap-1.5 sm:flex sm:items-center">
          {RANGE_OPTIONS.map((range) => (
            <button
              key={range}
              type="button"
              aria-pressed={selectedRange === range}
              onClick={() => handleRangeChange(range)}
              className={`min-w-0 whitespace-nowrap rounded-lg px-1.5 py-1 text-[10px] font-medium transition-colors sm:px-2.5 sm:py-1.5 sm:text-[11px] ${selectedRange === range
                ? 'bg-orange-500 text-white shadow-md shadow-orange-500/20'
                : 'bg-zinc-800 text-zinc-400 hover:bg-zinc-700'
                }`}
            >
              {range === '30m' && t('last30m')}
              {range === '1h' && t('last1h')}
              {range === '3h' && t('last3h')}
            </button>
          ))}
        </div>
      </div>

      <div className="h-[152px] min-h-0 min-w-0 shrink-0 overflow-y-auto overscroll-contain pr-1 sm:h-auto sm:flex-1 sm:shrink">
        <VehicleHistoryTable
          items={data?.items ?? []}
          isLoading={isLoading}
          isError={isError}
          onRetry={() => void refetch()}
        />
      </div>

      {!isLoading && data && data.totalPages > 1 && (
        <div className="mt-1.5 flex shrink-0 flex-wrap items-center justify-between gap-2 border-t border-zinc-800 pt-2 text-[11px]">
          <span className="font-mono text-[10px] text-zinc-400">
            {t('page', {
              current: data.page,
              total: data.totalPages,
            })}{' '}
            {t('recordsCount', {
              count: data.totalCount,
            })}
          </span>

          <div className="flex shrink-0 items-center gap-1.5">
            <button
              type="button"
              disabled={!data.hasPreviousPage}
              onClick={() =>
                setCurrentPage((page) => Math.max(1, page - 1))
              }
              className="rounded-lg border border-zinc-800 px-2.5 py-1.5 font-medium text-zinc-300 hover:bg-zinc-800 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {t('prev')}
            </button>

            <button
              type="button"
              disabled={!data.hasNextPage}
              onClick={() =>
                setCurrentPage((page) => page + 1)
              }
              className="rounded-lg border border-zinc-800 px-2.5 py-1.5 font-medium text-zinc-300 hover:bg-zinc-800 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {t('next')}
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
