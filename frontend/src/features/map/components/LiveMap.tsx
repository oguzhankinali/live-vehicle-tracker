'use client';

import { useEffect, useRef, useState, useCallback } from 'react';
import Map, { NavigationControl } from 'react-map-gl/maplibre';
import * as maplibregl from 'maplibre-gl';
import { setWorkerUrl } from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import { useVehicleStore } from '../../vehicles/store/useVehicleStore';
import { VehiclePositionRecord } from '../../vehicles/types/vehicle';

if (typeof window !== 'undefined') {
  setWorkerUrl('/maplibre-gl-worker.mjs');
}

const osmStyle = {
  version: 8,
  sources: {
    osm: {
      type: 'raster',
      tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
      tileSize: 256,
      attribution: '&copy; OpenStreetMap Contributors'
    }
  },
  layers: [
    {
      id: 'osm',
      type: 'raster',
      source: 'osm',
      minzoom: 0,
      maxzoom: 19
    }
  ]
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
      if (typeof lon === 'number' && typeof lat === 'number' && !isNaN(lon) && !isNaN(lat) && lon >= -180 && lon <= 180 && lat >= -90 && lat <= 90) {
        features.push({
          type: 'Feature',
          geometry: {
            type: 'Point',
            coordinates: [lon, lat]
          },
          properties: {
            id: vId,
            routeId: rId,
            speed: v.speed || 0,
            heading: v.bearing || 0
          }
        });
      }
    }
  }

  return {
    type: 'FeatureCollection',
    features
  };
}

export function LiveMap() {
  const { connect, vehicles, searchQuery, routeQuery, selectedVehicleId, setSelectedVehicleId } = useVehicleStore();
  const mapRef = useRef<maplibregl.Map | null>(null);
  const [cursor, setCursor] = useState<string>('auto');

  // Kendime not: WebSocket bağlantısı global store'a aittir. React Strict Mode'un
  // bileşeni unmount edip bağlantıyı koparmasını engellemek için cleanup yapmıyoruz.
  useEffect(() => {
    connect();
  }, [connect]);

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

  const handleMapLoad = (e: any) => {
    const map = e.target as maplibregl.Map;
    mapRef.current = map;

    if (!map.getSource('vehicles-source')) {
      const currentVehicles = useVehicleStore.getState().vehicles;

      map.addSource('vehicles-source', {
        type: 'geojson',
        data: toGeoJson(currentVehicles, searchQuery, routeQuery)
      });

      map.addLayer({
        id: 'vehicles-layer',
        type: 'circle',
        source: 'vehicles-source',
        paint: {
          'circle-radius': 6,
          'circle-color': '#2563eb',
          'circle-stroke-width': 2,
          'circle-stroke-color': '#ffffff'
        }
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
          'circle-stroke-color': '#f97316'
        }
      });
    }
  };

  const handleMapClick = useCallback((e: any) => {
    const map = mapRef.current;
    if (!map) return;

    const features = map.queryRenderedFeatures(e.point, { layers: ['vehicles-layer'] });
    if (features && features.length > 0) {
      const clickedVehicleId = features[0].properties?.id;
      if (clickedVehicleId) {
        setSelectedVehicleId(clickedVehicleId);
      }
    }
  }, [setSelectedVehicleId]);

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
          zoom: 10
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