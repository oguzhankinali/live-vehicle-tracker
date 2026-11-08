'use client';

import { useEffect, useRef } from 'react';
import { useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useVehicleStore } from '../store/useVehicleStore';

export function VehicleFilterBar() {
  const t = useTranslations('Filter');
  const searchParams = useSearchParams();
  const debounceTimer = useRef<NodeJS.Timeout | null>(null);
  const isInitialized = useRef(false);

  const { searchQuery, routeQuery, setSearchQuery, setRouteQuery } = useVehicleStore();

  // Kendime not: URL'i YALNIZCA açılışta oku. Kullanıcı yazarken store ezilmesin.
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

  return (
    <div className="flex flex-wrap items-center gap-2">
      <input
        type="text"
        aria-label={t('searchPlaceholder')}
        placeholder={t('searchPlaceholder')}
        value={searchQuery}
        onChange={(e) => handleSearchChange(e.target.value)}
        className="px-3 py-1.5 text-xs bg-zinc-100 dark:bg-zinc-800 border border-zinc-300 dark:border-zinc-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 w-44 transition-all"
      />

      <input
        type="text"
        aria-label={t('routePlaceholder')}
        placeholder={t('routePlaceholder')}
        value={routeQuery}
        onChange={(e) => handleRouteChange(e.target.value)}
        className="px-3 py-1.5 text-xs bg-zinc-100 dark:bg-zinc-800 border border-zinc-300 dark:border-zinc-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 w-36 transition-all"
      />

      {(searchQuery || routeQuery) && (
        <button
          onClick={clearFilters}
          className="px-2.5 py-1.5 text-xs bg-zinc-200 dark:bg-zinc-700 hover:bg-zinc-300 dark:hover:bg-zinc-600 rounded-lg text-zinc-700 dark:text-zinc-200 font-medium transition-colors"
        >
          {t('clear')}
        </button>
      )}
    </div>
  );
}