'use client';

import { useMemo } from 'react';
import { useTranslations } from 'next-intl';

import { matchesFilter } from '../lib/matchesFilter';
import { useVehicleStore } from '../store/useVehicleStore';

export function VehicleFilterStats() {
    const t = useTranslations('Filter');

    const vehicles = useVehicleStore((state) => state.vehicles);
    const totalVehicleCount = useVehicleStore(
        (state) => state.totalVehicleCount
    );
    const searchQuery = useVehicleStore((state) => state.searchQuery);
    const routeQuery = useVehicleStore((state) => state.routeQuery);

    const hasActiveFilters = Boolean(
        searchQuery.trim() || routeQuery.trim()
    );

    const filteredCount = useMemo(() => {
        if (!hasActiveFilters) {
            return totalVehicleCount;
        }

        return Object.values(vehicles).filter((vehicle) =>
            matchesFilter(vehicle, searchQuery, routeQuery)
        ).length;
    }, [
        vehicles,
        totalVehicleCount,
        searchQuery,
        routeQuery,
        hasActiveFilters,
    ]);

    return (
        <span
            className="whitespace-nowrap font-mono text-[10px] text-zinc-400 sm:text-[11px]"
            aria-live="off"
        >
            {hasActiveFilters
                ? t('filteredStats', {
                    filtered: filteredCount,
                    total: totalVehicleCount,
                })
                : t('totalStats', { total: totalVehicleCount })}
        </span>
    );
}
