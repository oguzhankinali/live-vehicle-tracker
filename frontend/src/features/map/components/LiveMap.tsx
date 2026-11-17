'use client';

import { useEffect, useRef, useState, useCallback } from 'react';
import Map, { NavigationControl } from 'react-map-gl/maplibre';
import * as maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import { useTranslations } from 'next-intl';
import { AlertTriangle, Loader2, SearchX } from 'lucide-react';
import { useVehicleStore } from '@/features/vehicles/store/useVehicleStore';
import { useVehicleStream } from '@/features/vehicles/hooks/useVehicleStream';
import { VehiclePositionRecord } from '@/features/vehicles/types/vehicle';
import { matchesFilter } from '@/features/vehicles/lib/matchesFilter';

if (typeof window !== 'undefined') {
  maplibregl.setWorkerUrl(
    new URL('maplibre-gl/dist/maplibre-gl-worker.mjs', import.meta.url).toString()
  );
}

const osmStyle = {
  version: 8,
  sources: {
    osm: {
      type: 'raster',
      tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
      tileSize: 256,
      attribution: '&copy; OpenStreetMap Contributors',
    },
  },
  layers: [
    {
      id: 'osm',
      type: 'raster',
      source: 'osm',
      minzoom: 0,
      maxzoom: 19,
    },
  ],
};

function toGeoJson(
  vehiclesRecord: Record<string, VehiclePositionRecord>,
  search: string,
  route: string
): { collection: GeoJSON.FeatureCollection; count: number } {
  const raw = Object.values(vehiclesRecord);
  const features: GeoJSON.Feature[] = [];

  for (let i = 0; i < raw.length; i++) {
    const v = raw[i];
    const lon = v.longitude;
    const lat = v.latitude;

    if (matchesFilter(v, search, route)) {
      if (
        typeof lon === 'number' &&
        typeof lat === 'number' &&
        !isNaN(lon) &&
        !isNaN(lat) &&
        lon >= -180 &&
        lon <= 180 &&
        lat >= -90 &&
        lat <= 90
      ) {
        features.push({
          type: 'Feature',
          geometry: {
            type: 'Point',
            coordinates: [lon, lat],
          },
          properties: {
            id: v.vehicleId,
            routeId: v.routeId || '',
            speed: v.speed || 0,
            bearing: v.bearing || 0,
          },
        });
      }
    }
  }

  return {
    collection: {
      type: 'FeatureCollection',
      features,
    },
    count: features.length,
  };
}

export function LiveMap() {
  useVehicleStream();
  const tMap = useTranslations('Map');

  const {
    vehicles,
    searchQuery,
    routeQuery,
    selectedVehicleId,
    setSelectedVehicleId,
    historyTrailPoints,
    trailFitVersion,
    totalVehicleCount,
    connectionStatus,
  } = useVehicleStore();

  const mapRef = useRef<maplibregl.Map | null>(null);
  const [cursor, setCursor] = useState<string>('auto');
  const [renderedCount, setRenderedCount] = useState<number>(0);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    const source = map.getSource('vehicles-source') as maplibregl.GeoJSONSource;
    if (source) {
      const t0 = performance.now();
      const { collection, count } = toGeoJson(vehicles, searchQuery, routeQuery);
      source.setData(collection);
      setRenderedCount(count);
      const durationMs = (performance.now() - t0).toFixed(2);

      if (count > 0 && Math.random() < 0.1) {
        console.log(`[PERF] MapLibre GeoJSON render süresi: ${durationMs}ms (${count} araç)`);
      }
    }
  }, [vehicles, searchQuery, routeQuery]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !map.getLayer('vehicles-highlight-layer')) return;
    map.setFilter('vehicles-highlight-layer', ['==', 'id', selectedVehicleId || '']);
  }, [selectedVehicleId]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    const trailSource = map.getSource('vehicle-trail-source') as maplibregl.GeoJSONSource;
    const pointsSource = map.getSource('vehicle-endpoints-source') as maplibregl.GeoJSONSource;
    if (!trailSource || !pointsSource) return;

    if (historyTrailPoints && historyTrailPoints.length > 1) {
      trailSource.setData({
        type: 'FeatureCollection',
        features: [
          {
            type: 'Feature',
            geometry: {
              type: 'LineString',
              coordinates: historyTrailPoints,
            },
            properties: {},
          },
        ],
      });

      const startPoint = historyTrailPoints[0];
      const endPoint = historyTrailPoints[historyTrailPoints.length - 1];
      pointsSource.setData({
        type: 'FeatureCollection',
        features: [
          {
            type: 'Feature',
            geometry: { type: 'Point', coordinates: startPoint },
            properties: { type: 'start' },
          },
          {
            type: 'Feature',
            geometry: { type: 'Point', coordinates: endPoint },
            properties: { type: 'end' },
          },
        ],
      });
    } else {
      trailSource.setData({ type: 'FeatureCollection', features: [] });
      pointsSource.setData({ type: 'FeatureCollection', features: [] });
    }
  }, [historyTrailPoints]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || trailFitVersion === 0 || historyTrailPoints.length < 2) return;

    const bounds = new maplibregl.LngLatBounds(historyTrailPoints[0], historyTrailPoints[0]);
    for (let i = 1; i < historyTrailPoints.length; i++) {
      bounds.extend(historyTrailPoints[i]);
    }
    map.fitBounds(bounds, { padding: 120, maxZoom: 14, duration: 800 });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- Kamera yalnızca yeni geçmiş rotası istendiğinde odaklansın
  }, [trailFitVersion]);

  const handleMapLoad = (e: any) => {
    const map = e.target as maplibregl.Map;
    mapRef.current = map;

    if (!map.getSource('vehicles-source')) {
      const currentVehicles = useVehicleStore.getState().vehicles;
      const { collection } = toGeoJson(currentVehicles, searchQuery, routeQuery);
      map.addSource('vehicles-source', {
        type: 'geojson',
        data: collection,
      });

      map.addLayer({
        id: 'vehicles-layer',
        type: 'circle',
        source: 'vehicles-source',
        paint: {
          'circle-radius': 6,
          'circle-color': '#2563eb',
          'circle-stroke-width': 2,
          'circle-stroke-color': '#ffffff',
        },
      });

      map.addLayer({
        id: 'vehicles-highlight-layer',
        type: 'circle',
        source: 'vehicles-source',
        filter: ['==', 'id', ''],
        paint: {
          'circle-radius': 11,
          'circle-color': 'rgba(249, 115, 22, 0.2)',
          'circle-stroke-width': 3,
          'circle-stroke-color': '#f97316',
        },
      });
    }

    if (!map.getSource('vehicle-trail-source')) {
      map.addSource('vehicle-trail-source', {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
      });

      map.addLayer({
        id: 'vehicle-trail-casing',
        type: 'line',
        source: 'vehicle-trail-source',
        layout: { 'line-join': 'round', 'line-cap': 'round' },
        paint: {
          'line-color': '#ffffff',
          'line-width': 7,
          'line-opacity': 0.95,
        },
      });

      map.addLayer({
        id: 'vehicle-trail-line',
        type: 'line',
        source: 'vehicle-trail-source',
        layout: { 'line-join': 'round', 'line-cap': 'round' },
        paint: {
          'line-color': '#7c3aed',
          'line-width': 4,
          'line-opacity': 1,
        },
      });
    }

    if (!map.getSource('vehicle-endpoints-source')) {
      map.addSource('vehicle-endpoints-source', {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
      });

      map.addLayer({
        id: 'vehicle-endpoints-layer',
        type: 'circle',
        source: 'vehicle-endpoints-source',
        paint: {
          'circle-radius': 9,
          'circle-color': [
            'match',
            ['get', 'type'],
            'start',
            '#10b981',
            'end',
            '#ef4444',
            '#000000',
          ],
          'circle-stroke-width': 3.5,
          'circle-stroke-color': '#ffffff',
        },
      });
    }
  };

  const handleMapClick = useCallback(
    (e: any) => {
      const map = mapRef.current;
      if (!map) return;
      const features = map.queryRenderedFeatures(e.point, { layers: ['vehicles-layer'] });
      if (features && features.length > 0) {
        const clickedVehicleId = features[0].properties?.id;
        if (clickedVehicleId) {
          setSelectedVehicleId(clickedVehicleId);
        }
      }
    },
    [setSelectedVehicleId]
  );

  const handleMouseEnter = useCallback(() => setCursor('pointer'), []);
  const handleMouseLeave = useCallback(() => setCursor('auto'), []);

  const isInitialLoading = connectionStatus === 'Connecting' && totalVehicleCount === 0;
  const isConnectionError = connectionStatus === 'Disconnected' && totalVehicleCount === 0;
  const isFilterEmpty = totalVehicleCount > 0 && renderedCount === 0 && (searchQuery || routeQuery);

  return (
    <div className="relative w-full h-dvh">
      <Map
        mapLib={maplibregl}
        cursor={cursor}
        initialViewState={{
          longitude: 10.7522,
          latitude: 59.9139,
          zoom: 10,
        }}
        mapStyle={osmStyle as any}
        onLoad={handleMapLoad}
        onClick={handleMapClick}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
        interactiveLayerIds={['vehicles-layer']}
      >
        <NavigationControl position="bottom-right" />
      </Map>

      {isInitialLoading && (
        <div className="absolute inset-0 z-30 flex items-center justify-center bg-black/40 backdrop-blur-sm pointer-events-none">
          <div className="flex items-center gap-3 px-5 py-3 rounded-2xl bg-zinc-900/90 border border-zinc-800 text-zinc-100 shadow-2xl">
            <Loader2 className="h-5 w-5 animate-spin text-blue-500" />
            <span className="text-sm font-medium">{tMap('loading')}</span>
          </div>
        </div>
      )}

      {isConnectionError && (
        <div className="absolute top-36 sm:top-20 left-1/2 -translate-x-1/2 z-30 pointer-events-none">
          <div className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-rose-950/90 border border-rose-900 text-rose-300 text-xs font-medium shadow-2xl backdrop-blur-md">
            <AlertTriangle className="h-4 w-4" />
            <span>{tMap('connectionError')}</span>
          </div>
        </div>
      )}

      {isFilterEmpty && (
        <div className="absolute top-36 sm:top-20 left-1/2 -translate-x-1/2 z-20 pointer-events-none">
          <div className="flex items-center gap-2 px-4 py-2 rounded-xl bg-zinc-900/90 border border-zinc-800 text-amber-400 text-xs font-medium shadow-xl backdrop-blur-md">
            <SearchX className="h-4 w-4" />
            <span>{tMap('filterEmpty')}</span>
          </div>
        </div>
      )}
    </div>
  );
}
