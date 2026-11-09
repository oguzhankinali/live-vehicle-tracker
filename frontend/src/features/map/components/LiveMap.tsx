'use client';

import { useEffect, useRef, useState, useCallback } from 'react';
import Map, { NavigationControl } from 'react-map-gl/maplibre';
import * as maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import { useVehicleStore } from '@/features/vehicles/store/useVehicleStore';
import { useVehicleStream } from '@/features/vehicles/hooks/useVehicleStream';
import { VehiclePositionRecord } from '@/features/vehicles/types/vehicle';

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
): GeoJSON.FeatureCollection {
  const raw = Object.values(vehiclesRecord);
  const features: GeoJSON.Feature[] = [];
  const sQuery = search.trim().toLowerCase();
  const rQuery = route.trim().toLowerCase();

  for (let i = 0; i < raw.length; i++) {
    const v = raw[i];
    const lon = v.longitude;
    const lat = v.latitude;
    const vId = v.vehicleId;
    const rId = v.routeId || '';

    const matchesSearch = !sQuery || vId.toLowerCase().includes(sQuery);
    const matchesRoute = !rQuery || rId.toLowerCase().includes(rQuery);

    if (matchesSearch && matchesRoute) {
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
            id: vId,
            routeId: rId,
            speed: v.speed || 0,
            bearing: v.bearing || 0,
          },
        });
      }
    }
  }

  return {
    type: 'FeatureCollection',
    features,
  };
}

export function LiveMap() {
  useVehicleStream();

  const {
    vehicles,
    searchQuery,
    routeQuery,
    selectedVehicleId,
    setSelectedVehicleId,
    historyTrailPoints,
    trailFitVersion,
  } = useVehicleStore();

  const mapRef = useRef<maplibregl.Map | null>(null);
  const [cursor, setCursor] = useState<string>('auto');

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    const source = map.getSource('vehicles-source') as maplibregl.GeoJSONSource;
    if (source) {
      source.setData(toGeoJson(vehicles, searchQuery, routeQuery));
    }
  }, [vehicles, searchQuery, routeQuery]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !map.getLayer('vehicles-highlight-layer')) return;
    map.setFilter('vehicles-highlight-layer', ['==', 'id', selectedVehicleId || '']);
  }, [selectedVehicleId]);

  // Rota çizgisini ve uç noktaları güncelleme efekti
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

  // Kamera Odaklanma Efekti: SADECE trailFitVersion değiştiğinde (yeni rota yüklendiğinde) çalışır.
  // Canlı noktalar geldikçe bu sayaç artmaz, dolayısıyla kullanıcının zoom'u bozulmaz.
  useEffect(() => {
    const map = mapRef.current;
    if (!map || trailFitVersion === 0 || historyTrailPoints.length < 2) return;

    const bounds = new maplibregl.LngLatBounds(historyTrailPoints[0], historyTrailPoints[0]);
    for (let i = 1; i < historyTrailPoints.length; i++) {
      bounds.extend(historyTrailPoints[i]);
    }
    map.fitBounds(bounds, { padding: 120, maxZoom: 14, duration: 800 });
    // Bilinçli tercih: historyTrailPoints bilerek bağımlılığa eklenmemiştir;
    // aksi halde her canlı WebSocket noktasında kamera geriye zıplar.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trailFitVersion]);

  const handleMapLoad = (e: any) => {
    const map = e.target as maplibregl.Map;
    mapRef.current = map;

    if (!map.getSource('vehicles-source')) {
      const currentVehicles = useVehicleStore.getState().vehicles;
      map.addSource('vehicles-source', {
        type: 'geojson',
        data: toGeoJson(currentVehicles, searchQuery, routeQuery),
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

  return (
    <div className="w-full h-screen">
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
    </div>
  );
}