'use client';

import { useEffect, useRef, useMemo } from 'react';
import { useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useVehicleStore } from '../store/useVehicleStore';

export function VehicleFilterBar() {
  const t = useTranslations('Filter');
  const searchParams = useSearchParams();
  const debounceTimer = useRef<NodeJS.Timeout | null>(null);
  const isInitialized = useRef(false);

  const { searchQuery, routeQuery, setSearchQuery, setRouteQuery, vehicles, totalVehicleCount } =
    useVehicleStore();

  useEffect(() => {
    if (!isInitialized.current) {
      const urlSearch = searchParams.get('search') || '';
      const urlRoute = searchParams.get('routeId') || '';
      if (urlSearch) setSearchQuery(urlSearch);
      if (urlRoute) setRouteQuery(urlRoute);
      isInitialized.current = true;
    }
  }, [searchParams, setSearchQuery, setRouteQuery]);

  const syncUrlWithDebounce = (search: string, route: string) => {
    if (debounceTimer.current) clearTimeout(debounceTimer.current);
    debounceTimer.current = setTimeout(() => {
      const params = new URLSearchParams(window.location.search);
      if (search.trim()) params.set('search', search.trim());
      else params.delete('search');

      if (route.trim()) params.set('routeId', route.trim());
      else params.delete('routeId');

      const newQuery = params.toString();
      const newUrl = newQuery ? `${window.location.pathname}?${newQuery}` : window.location.pathname;
      window.history.replaceState(null, '', newUrl);
    }, 250);
  };

  const handleSearchChange = (val: string) => {
    setSearchQuery(val);
    syncUrlWithDebounce(val, routeQuery);
  };

  const handleRouteChange = (val: string) => {
    setRouteQuery(val);
    syncUrlWithDebounce(searchQuery, val);
  };

  const clearFilters = () => {
    setSearchQuery('');
    setRouteQuery('');
    syncUrlWithDebounce('', '');
  };

  // Şartname 4.1: Canlı filtrelenen araç sayısını hesapla (vehicleId veya plaka/etiket eşleşmesi)
  const filteredCount = useMemo(() => {
    const s = searchQuery.trim().toLowerCase();
    const r = routeQuery.trim().toLowerCase();
    if (!s && !r) return totalVehicleCount;

    return Object.values(vehicles).filter((v) => {
      const matchSearch =
        !s ||
        v.vehicleId.toLowerCase().includes(s) ||
        (v.vehicleLabel && v.vehicleLabel.toLowerCase().includes(s));
      const matchRoute = !r || (v.routeId && v.routeId.toLowerCase().includes(r));
      return matchSearch && matchRoute;
    }).length;
  }, [vehicles, searchQuery, routeQuery, totalVehicleCount]);

  return (
    <div className="flex flex-wrap items-center gap-2">
      <input
        type="text"
        aria-label={t('searchPlaceholder')}
        placeholder={t('searchPlaceholder')}
        value={searchQuery}
        onChange={(e) => handleSearchChange(e.target.value)}
        className="px-3 py-1.5 text-xs bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100 placeholder:text-zinc-500 focus:outline-none focus:ring-1 focus:ring-blue-500 w-40 transition-all"
      />
      <input
        type="text"
        aria-label={t('routePlaceholder')}
        placeholder={t('routePlaceholder')}
        value={routeQuery}
        onChange={(e) => handleRouteChange(e.target.value)}
        className="px-3 py-1.5 text-xs bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100 placeholder:text-zinc-500 focus:outline-none focus:ring-1 focus:ring-blue-500 w-32 transition-all"
      />

      {/* Şartname 4.1: Canlı araç istatistik rozeti */}
      <span className="text-[11px] font-mono text-zinc-400 px-1.5 select-none">
        {t('stats', { filtered: filteredCount, total: totalVehicleCount })}
      </span>

      {(searchQuery || routeQuery) && (
        <button
          onClick={clearFilters}
          className="px-2 py-1 text-xs bg-zinc-800 hover:bg-zinc-700 rounded-lg text-zinc-300 font-medium transition-colors"
        >
          {t('clear')}
        </button>
      )}
    </div>
  );
}