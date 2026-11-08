'use client';

import { useEffect, useRef } from 'react';
import Map, { NavigationControl } from 'react-map-gl/maplibre';
import * as maplibregl from 'maplibre-gl';
import { setWorkerUrl } from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import { useVehicleStore } from '../../vehicles/store/useVehicleStore';

// Kendime not: MapLibre v6'da worker ayarı doğrudan named export fonksiyonu ile yapılır.
// Az önce public/ içine kopyaladığımız öz be öz v6 worker'ımızı veriyoruz.
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

function toGeoJson(vehiclesRecord: Record<string, any>) {
  const raw = Object.values(vehiclesRecord);
  const features: any[] = [];

  for (let i = 0; i < raw.length; i++) {
    const v = raw[i];
    const lon = Number(v.longitude ?? v.Longitude);
    const lat = Number(v.latitude ?? v.Latitude);

    if (!isNaN(lon) && lon >= -180 && lon <= 180 && !isNaN(lat) && lat >= -90 && lat <= 90) {
      features.push({
        type: 'Feature',
        geometry: {
          type: 'Point',
          coordinates: [lon, lat]
        },
        properties: {
          id: String(v.vehicleId ?? v.VehicleId ?? ''),
          routeId: String(v.routeId ?? v.RouteId ?? ''),
          speed: Number(v.speed ?? v.Speed ?? 0),
          heading: Number(v.bearing ?? v.Bearing ?? 0)
        }
      });
    }
  }

  return {
    type: 'FeatureCollection',
    features
  };
}

export function LiveMap() {
  const { connect, disconnect, vehicles } = useVehicleStore();
  const mapRef = useRef<maplibregl.Map | null>(null);

  useEffect(() => {
    connect();
    return () => {
      disconnect();
    };
  }, [connect, disconnect]);

  // Canlı SignalR güncellemeleri geldikçe GPU'ya aktar
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    const source = map.getSource('vehicles-source') as maplibregl.GeoJSONSource;
    if (source) {
      source.setData(toGeoJson(vehicles) as any);
    }
  }, [vehicles]);

  const handleMapLoad = (e: any) => {
    const map = e.target as maplibregl.Map;
    mapRef.current = map;
    console.log('[Map] Harita motoru yüklendi, 928 araç GPU katmanına basılıyor...');

    if (!map.getSource('vehicles-source')) {
      const currentVehicles = useVehicleStore.getState().vehicles;

      map.addSource('vehicles-source', {
        type: 'geojson',
        data: toGeoJson(currentVehicles) as any
      });

      map.addLayer({
        id: 'vehicles-layer',
        type: 'circle',
        source: 'vehicles-source',
        paint: {
          'circle-radius': 6,
          'circle-color': '#2563eb', // Canlı Mavi
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