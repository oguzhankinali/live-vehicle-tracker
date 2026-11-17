'use client';

import { useFormatter, useTranslations } from 'next-intl';

import type { VehiclePositionRecord } from '../types/vehicle';

interface VehicleHistoryTableProps {
    items: VehiclePositionRecord[];
    isLoading: boolean;
    isError: boolean;
    onRetry: () => void;
}

export function VehicleHistoryTable({
    items,
    isLoading,
    isError,
    onRetry,
}: VehicleHistoryTableProps) {
    const t = useTranslations('History');
    const format = useFormatter();

    if (isLoading) {
        return (
            <div className="flex h-32 items-center justify-center gap-2">
                <div className="h-5 w-5 animate-spin rounded-full border-2 border-orange-500 border-t-transparent" />
                <span className="text-xs text-zinc-400">{t('loading')}</span>
            </div>
        );
    }

    if (isError) {
        return (
            <div className="flex h-32 flex-col items-center justify-center gap-3 text-center">
                <p className="text-xs text-rose-400">{t('error')}</p>
                <button
                    type="button"
                    onClick={onRetry}
                    className="rounded-lg border border-rose-900 bg-rose-950/40 px-3 py-1.5 text-xs text-rose-400 hover:bg-rose-900/40"
                >
                    {t('retry')}
                </button>
            </div>
        );
    }

    if (items.length === 0) {
        return (
            <div className="flex h-32 items-center justify-center px-3 text-center">
                <p className="text-xs text-zinc-400">{t('empty')}</p>
            </div>
        );
    }

    return (
        <table className="w-full table-fixed text-left text-[11px]">
            <thead className="sticky top-0 z-10 bg-zinc-800 text-zinc-400">
                <tr className="h-8 border-b border-zinc-700">
                    <th scope="col" className="w-[36%] px-1.5 font-semibold">
                        {t('time')}
                    </th>

                    <th
                        scope="col"
                        className="w-[18%] px-1 font-semibold"
                        title={t('unitKmh')}
                    >
                        {t('speed')}
                        <span className="sr-only"> ({t('unitKmh')})</span>
                    </th>

                    <th scope="col" className="w-[46%] px-1.5 font-semibold">
                        {t('coords')}
                    </th>
                </tr>
            </thead>

            <tbody className="divide-y divide-zinc-800/70">
                {items.map((item, index) => (
                    <tr
                        key={`${item.feedEntityId}-${item.timestamp}-${index}`}
                        className="h-10 hover:bg-zinc-800/30"
                    >
                        <td className="px-1.5 py-0.5 font-mono text-zinc-300">
                            <span className="block whitespace-nowrap">
                                {format.dateTime(new Date(item.timestamp), {
                                    day: '2-digit',
                                    month: '2-digit',
                                })}
                            </span>

                            <span className="block whitespace-nowrap text-[10px] text-zinc-400">
                                {format.dateTime(new Date(item.timestamp), {
                                    hour: '2-digit',
                                    minute: '2-digit',
                                    second: '2-digit',
                                    hour12: false,
                                })}
                            </span>
                        </td>

                        <td className="px-1 py-0.5 font-semibold text-zinc-200">
                            {item.speed != null
                                ? Math.round(item.speed * 3.6)
                                : '-'}
                        </td>

                        <td className="px-1.5 py-0.5 font-mono text-[10px] text-zinc-400">
                            <span className="block whitespace-nowrap">
                                {format.number(item.latitude, {
                                    minimumFractionDigits: 4,
                                    maximumFractionDigits: 4,
                                })}
                            </span>

                            <span className="block whitespace-nowrap">
                                {format.number(item.longitude, {
                                    minimumFractionDigits: 4,
                                    maximumFractionDigits: 4,
                                })}
                            </span>
                        </td>
                    </tr>
                ))}
            </tbody>
        </table>
    );
}
