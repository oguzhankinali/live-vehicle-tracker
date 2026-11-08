'use client';

import Map, { NavigationControl } from 'react-map-gl/maplibre';
import * as maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';

// not: Turbopack yerel worker'ı engellediği için jsdelivr üzerinden çekiyoruz.
maplibregl.setWorkerUrl('https://cdn.jsdelivr.net/npm/maplibre-gl@4.7.1/dist/maplibre-gl-csp-worker.js');

// Kendime not: Harici stil URL'leri (Carto vb.) CORS veya versiyon uyumsuzluğu yapabildiği için, en garantili yol olan standart OSM raster stilini doğrudan JSON nesnesi olarak veriyoruz.
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

export function LiveMap() {
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
      >
        <NavigationControl position="bottom-right" />
      </Map>
    </div>
  );
}