'use client';

import { useState, useMemo, useEffect } from 'react';
import { useTranslations, useFormatter } from 'next-intl';
import { X } from 'lucide-react';
import { useVehicleHistory, useVehicleTrail } from '../api/getVehicleHistory';
import { useVehicleStore } from '../store/useVehicleStore';

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

export function VehicleHistoryModal({ vehicleId, isOpen, onClose }: VehicleHistoryModalProps) {
  const t = useTranslations('History');
  const format = useFormatter();

  const historyTrailPoints = useVehicleStore((state) => state.historyTrailPoints);
  const setHistoryTrailPoints = useVehicleStore((state) => state.setHistoryTrailPoints);
  const clearHistoryTrail = useVehicleStore((state) => state.clearHistoryTrail);

  const [selectedRange, setSelectedRange] = useState<RangeOption>('1h');
  const [currentPage, setCurrentPage] = useState<number>(1);

  // isOpen bilinçli olarak eklendi: Modal her açıldığında zaman penceresini "şimdi"ye göre tazeler
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const { fromIso, toIso } = useMemo(() => {
    const nowMs = Math.floor(Date.now() / 1000) * 1000;
    return {
      fromIso: new Date(nowMs - RANGE_MS[selectedRange]).toISOString(),
      toIso: new Date(nowMs).toISOString(),
    };
  }, [selectedRange, isOpen]);

  const { data, isLoading, isError, refetch } = useVehicleHistory(
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

  if (!isOpen) return null;

  const liveTotalPoints = historyTrailPoints.length > 0 ? historyTrailPoints.length : (trailPoints?.length || 0);

  return (
    <div className="absolute bottom-6 left-6 z-20 w-[520px] max-w-[calc(100vw-3rem)] bg-zinc-900/95 backdrop-blur-md rounded-2xl shadow-2xl border border-zinc-800 p-5 flex flex-col max-h-[380px] animate-in fade-in slide-in-from-bottom-4 duration-300">
      <div className="flex items-center justify-between pb-3 border-b border-zinc-800">
        <div>
          <h2 className="text-sm font-bold text-zinc-100 flex items-center gap-2">
            <span>{t('title')}</span>
            <span className="text-[11px] font-mono font-normal px-2 py-0.5 bg-blue-950 text-blue-400 rounded-md">
              {vehicleId}
            </span>
          </h2>
          <div className="flex items-center gap-3 mt-1.5 text-[11px]">
            <span className="flex items-center gap-1.5 font-medium text-emerald-400">
              <span className="h-2.5 w-2.5 rounded-full bg-emerald-500 ring-2 ring-emerald-900" />
              {t('startPoint')}
            </span>
            <span className="text-zinc-600 font-bold">→</span>
            <span className="flex items-center gap-1.5 font-medium text-rose-400">
              <span className="h-2.5 w-2.5 rounded-full bg-rose-500 ring-2 ring-rose-900" />
              {t('endPoint')}
            </span>
            {liveTotalPoints > 0 && (
              <span className="text-zinc-400 font-mono text-[10px] ml-auto">
                {t('pointsCount', { count: liveTotalPoints })}
              </span>
            )}
          </div>
        </div>
        <button
          onClick={handleClose}
          className="text-zinc-400 hover:text-zinc-200 p-1.5 rounded-lg hover:bg-zinc-800 transition-colors"
          aria-label={t('close')}
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      <div className="flex items-center gap-2 my-3">
        <span className="text-[11px] text-zinc-400 font-medium">{t('timeRange')}:</span>
        {(['30m', '1h', '3h'] as RangeOption[]).map((range) => (
          <button
            key={range}
            onClick={() => {
              setSelectedRange(range);
              setCurrentPage(1);
            }}
            className={`px-2.5 py-1 text-[11px] font-medium rounded-lg transition-colors ${
              selectedRange === range
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

      <div className="flex-1 overflow-y-auto min-h-[140px] pr-1">
        {isLoading && (
          <div className="flex flex-col items-center justify-center h-32 gap-2">
            <div className="h-5 w-5 border-2 border-orange-500 border-t-transparent rounded-full animate-spin" />
            <span className="text-xs text-zinc-400">{t('loading')}</span>
          </div>
        )}

        {isError && (
          <div className="flex flex-col items-center justify-center h-32 gap-2 text-center">
            <span className="text-rose-500 text-xs font-medium">{t('error')}</span>
            <button
              onClick={() => refetch()}
              className="px-2.5 py-1 text-xs bg-rose-950/40 text-rose-400 border border-rose-900 rounded-lg hover:bg-rose-900/40"
            >
              {t('retry')}
            </button>
          </div>
        )}

        {!isLoading && !isError && data && data.items.length === 0 && (
          <div className="flex flex-col items-center justify-center h-32 text-center text-xs text-zinc-400">
            <p>{t('empty')}</p>
          </div>
        )}

        {!isLoading && !isError && data && data.items.length > 0 && (
          <table className="w-full text-left text-[11px]">
            <thead className="sticky top-0 bg-zinc-800/90 backdrop-blur border-b border-zinc-800 text-zinc-400">
              <tr>
                <th scope="col" className="py-1.5 px-2 font-semibold">{t('time')}</th>
                <th scope="col" className="py-1.5 px-2 font-semibold">{t('speed')}</th>
                <th scope="col" className="py-1.5 px-2 font-semibold">{t('coords')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-800/50">
              {data.items.map((item, idx) => (
                <tr key={`${item.feedEntityId}-${item.timestamp}-${idx}`} className="hover:bg-zinc-800/30">
                  <td className="py-1 px-2 font-mono text-zinc-300">
                    {format.dateTime(new Date(item.timestamp), {
                      hour: '2-digit',
                      minute: '2-digit',
                      second: '2-digit',
                    })}
                  </td>
                  <td className="py-1 px-2 font-semibold text-zinc-200">
                    {item.speed != null ? `${Math.round(item.speed * 3.6)} ${t('unitKmh')}` : '-'}
                  </td>
                  <td className="py-1 px-2 font-mono text-zinc-400 text-[10px]">
                    {item.latitude.toFixed(4)}, {item.longitude.toFixed(4)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {!isLoading && data && data.totalPages > 1 && (
        <div className="flex items-center justify-between pt-3 mt-2 border-t border-zinc-800 text-[11px]">
          <span className="text-zinc-400 font-mono">
            {t('page', { current: data.page, total: data.totalPages })} {t('recordsCount', { count: data.totalCount })}
          </span>
          <div className="flex items-center gap-1.5">
            <button
              disabled={!data.hasPreviousPage}
              onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
              className="px-2.5 py-1 rounded-lg border border-zinc-800 text-zinc-300 disabled:opacity-40 disabled:cursor-not-allowed hover:bg-zinc-800 font-medium"
            >
              {t('prev')}
            </button>
            <button
              disabled={!data.hasNextPage}
              onClick={() => setCurrentPage((p) => p + 1)}
              className="px-2.5 py-1 rounded-lg border border-zinc-800 text-zinc-300 disabled:opacity-40 disabled:cursor-not-allowed hover:bg-zinc-800 font-medium"
            >
              {t('next')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}