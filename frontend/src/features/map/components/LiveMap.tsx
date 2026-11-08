'use client';

import { useEffect, useRef } from 'react';
import Map, { NavigationControl } from 'react-map-gl/maplibre';
import * as maplibregl from 'maplibre-gl';
import { setWorkerUrl } from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import { useVehicleStore } from '../../vehicles/store/useVehicleStore';

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

// Kendime not: Filtreleme parametrelerini doğrudan GeoJSON dönüşümünde uyguluyoruz (0 ms)
function toGeoJson(vehiclesRecord: Record<string, any>, search: string, route: string) {
  const raw = Object.values(vehiclesRecord);
  const features: any[] = [];

  const sQuery = search.trim().toLowerCase();
  const rQuery = route.trim().toLowerCase();

  for (let i = 0; i < raw.length; i++) {
    const v = raw[i];
    const lon = Number(v.longitude ?? v.Longitude);
    const lat = Number(v.latitude ?? v.Latitude);
    const vId = String(v.vehicleId ?? v.VehicleId ?? '');
    const rId = String(v.routeId ?? v.RouteId ?? '');

    // Filtreleme Kontrolü
    const matchesSearch = !sQuery || vId.toLowerCase().includes(sQuery);
    const matchesRoute = !rQuery || rId.toLowerCase().includes(rQuery);

    if (matchesSearch && matchesRoute) {
      if (!isNaN(lon) && lon >= -180 && lon <= 180 && !isNaN(lat) && lat >= -90 && lat <= 90) {
        features.push({
          type: 'Feature',
          geometry: {
            type: 'Point',
            coordinates: [lon, lat]
          },
          properties: {
            id: vId,
            routeId: rId,
            speed: Number(v.speed ?? v.Speed ?? 0),
            heading: Number(v.bearing ?? v.Bearing ?? 0)
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
  const { connect, disconnect, vehicles, searchQuery, routeQuery } = useVehicleStore();
  const mapRef = useRef<maplibregl.Map | null>(null);

  useEffect(() => {
    connect();
    return () => {
      disconnect();
    };
  }, [connect, disconnect]);

  // Canlı SignalR verisi veya filtre değiştiğinde doğrudan GPU'ya bas
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    const source = map.getSource('vehicles-source') as maplibregl.GeoJSONSource;
    if (source) {
      source.setData(toGeoJson(vehicles, searchQuery, routeQuery) as any);
    }
  }, [vehicles, searchQuery, routeQuery]);

  const handleMapLoad = (e: any) => {
    const map = e.target as maplibregl.Map;
    mapRef.current = map;

    if (!map.getSource('vehicles-source')) {
      const currentVehicles = useVehicleStore.getState().vehicles;

      map.addSource('vehicles-source', {
        type: 'geojson',
        data: toGeoJson(currentVehicles, searchQuery, routeQuery) as any
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
    }
  };

  return (
    <div className="w-full h-screen">
      <Map
        mapLib={maplibregl}
        initialViewState={{
          longitude: 10.7522,
          latitude: 59.9139,
          zoom: 10
        }}
        mapStyle={osmStyle as any}
        onLoad={handleMapLoad}
      >
        <NavigationControl position="bottom-right" />
      </Map>
    </div>
  );
}