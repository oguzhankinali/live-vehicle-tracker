'use client';

import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Route, Search } from 'lucide-react';

import { useVehicleStore } from '../store/useVehicleStore';
import { VehicleFilterStats } from './VehicleFilterStats';

type SearchMode = 'vehicle' | 'route' | null;

const inputClassName =
  'w-full min-w-0 flex-1 rounded-lg border border-zinc-700 bg-zinc-800 px-3 py-2 text-base text-zinc-100 placeholder:text-zinc-400 focus:outline-none focus:ring-1 focus:ring-blue-500 sm:text-xs';

export function VehicleFilterBar() {
  const t = useTranslations('Filter');
  const searchParams = useSearchParams();

  const [mobileSearch, setMobileSearch] = useState<SearchMode>(null);
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isInitialized = useRef(false);

  const searchQuery = useVehicleStore((state) => state.searchQuery);
  const routeQuery = useVehicleStore((state) => state.routeQuery);
  const setSearchQuery = useVehicleStore((state) => state.setSearchQuery);
  const setRouteQuery = useVehicleStore((state) => state.setRouteQuery);

  const hasActiveFilters = Boolean(
    searchQuery.trim() || routeQuery.trim()
  );

  useEffect(() => {
    if (isInitialized.current) return;

    const urlSearch = searchParams.get('search') || '';
    const urlRoute = searchParams.get('routeId') || '';

    if (urlSearch) setSearchQuery(urlSearch);
    if (urlRoute) setRouteQuery(urlRoute);

    if (urlSearch) {
      setMobileSearch('vehicle');
    } else if (urlRoute) {
      setMobileSearch('route');
    }

    isInitialized.current = true;
  }, [searchParams, setSearchQuery, setRouteQuery]);

  useEffect(() => {
    return () => {
      if (debounceTimer.current) {
        clearTimeout(debounceTimer.current);
      }
    };
  }, []);

  const syncUrlWithDebounce = (search: string, route: string) => {
    if (debounceTimer.current) {
      clearTimeout(debounceTimer.current);
    }

    debounceTimer.current = setTimeout(() => {
      const params = new URLSearchParams(window.location.search);

      if (search.trim()) {
        params.set('search', search.trim());
      } else {
        params.delete('search');
      }

      if (route.trim()) {
        params.set('routeId', route.trim());
      } else {
        params.delete('routeId');
      }

      const query = params.toString();
      const url = query
        ? `${window.location.pathname}?${query}`
        : window.location.pathname;

      window.history.replaceState(null, '', url);
    }, 250);
  };

  const handleSearchChange = (value: string) => {
    setSearchQuery(value);
    syncUrlWithDebounce(value, routeQuery);
  };

  const handleRouteChange = (value: string) => {
    setRouteQuery(value);
    syncUrlWithDebounce(searchQuery, value);
  };

  const clearFilters = () => {
    setSearchQuery('');
    setRouteQuery('');
    syncUrlWithDebounce('', '');
  };

  const toggleMobileSearch = (mode: Exclude<SearchMode, null>) => {
    setMobileSearch((current) =>
      current === mode ? null : mode
    );
  };

  return (
    <div className="flex w-full min-w-0 flex-col gap-2 sm:w-auto sm:flex-row sm:flex-wrap sm:items-center">
      <div className="grid grid-cols-2 gap-2 sm:hidden">
        <button
          type="button"
          aria-expanded={mobileSearch === 'vehicle'}
          aria-controls="mobile-vehicle-search"
          onClick={() => toggleMobileSearch('vehicle')}
          className={`flex min-h-9 min-w-0 items-center justify-center gap-1.5 rounded-lg border px-2 py-2 text-[11px] font-medium transition-colors ${mobileSearch === 'vehicle'
              ? 'border-blue-500 bg-blue-600/20 text-blue-300'
              : 'border-zinc-700 bg-zinc-800 text-zinc-200'
            }`}
        >
          <Search className="h-3.5 w-3.5 shrink-0" />
          <span>{t('vehicleSearch')}</span>
          {searchQuery.trim() && (
            <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-emerald-400" />
          )}
        </button>

        <button
          type="button"
          aria-expanded={mobileSearch === 'route'}
          aria-controls="mobile-route-search"
          onClick={() => toggleMobileSearch('route')}
          className={`flex min-h-9 min-w-0 items-center justify-center gap-1.5 rounded-lg border px-2 py-2 text-[11px] font-medium transition-colors ${mobileSearch === 'route'
              ? 'border-blue-500 bg-blue-600/20 text-blue-300'
              : 'border-zinc-700 bg-zinc-800 text-zinc-200'
            }`}
        >
          <Route className="h-3.5 w-3.5 shrink-0" />
          <span>{t('routeSearch')}</span>
          {routeQuery.trim() && (
            <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-emerald-400" />
          )}
        </button>
      </div>

      <div
        id="mobile-vehicle-search"
        className={`${mobileSearch === 'vehicle' ? 'flex' : 'hidden'} min-w-0 items-center gap-2 sm:block sm:w-44 lg:w-52`}
      >
        <input
          type="search"
          aria-label={t('vehicleSearch')}
          placeholder={t('searchPlaceholder')}
          value={searchQuery}
          onChange={(event) => handleSearchChange(event.target.value)}
          className={inputClassName}
        />

        {hasActiveFilters && (
          <button
            type="button"
            onClick={clearFilters}
            className="shrink-0 rounded-lg bg-zinc-800 px-2 py-2 text-xs font-medium text-zinc-300 hover:bg-zinc-700 sm:hidden"
          >
            {t('clear')}
          </button>
        )}
      </div>

      <div
        id="mobile-route-search"
        className={`${mobileSearch === 'route' ? 'flex' : 'hidden'} min-w-0 items-center gap-2 sm:block sm:w-40 lg:w-48`}
      >
        <input
          type="search"
          aria-label={t('routeSearch')}
          placeholder={t('routePlaceholder')}
          value={routeQuery}
          onChange={(event) => handleRouteChange(event.target.value)}
          className={inputClassName}
        />

        {hasActiveFilters && (
          <button
            type="button"
            onClick={clearFilters}
            className="shrink-0 rounded-lg bg-zinc-800 px-2 py-2 text-xs font-medium text-zinc-300 hover:bg-zinc-700 sm:hidden"
          >
            {t('clear')}
          </button>
        )}
      </div>

      <div className="hidden sm:block">
        <VehicleFilterStats />
      </div>

      {hasActiveFilters && (
        <button
          type="button"
          onClick={clearFilters}
          className="hidden rounded-lg bg-zinc-800 px-2.5 py-1.5 text-xs font-medium text-zinc-300 hover:bg-zinc-700 sm:block"
        >
          {t('clear')}
        </button>
      )}
    </div>
  );
}
