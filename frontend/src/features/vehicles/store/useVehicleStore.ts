import { create } from "zustand";
import { VehiclePositionRecord } from "../types/vehicle";
import { VEHICLE_EVICTION_MS } from "../constants/timing";

export type ConnectionStatus = "Connected" | "Connecting" | "Reconnecting" | "Disconnected";

interface VehicleState {
  vehicles: Record<string, VehiclePositionRecord>;
  connectionStatus: ConnectionStatus;
  selectedVehicleId: string | null;
  lastMessageReceivedAt: number | null;
  totalVehicleCount: number;

  searchQuery: string;
  routeQuery: string;

  // Haritada çizilen geçmiş rota koordinatları [lon, lat]
  historyTrailPoints: [number, number][];
  // Kameranın sadece yeni rota yüklendiğinde odaklanmasını sağlayan versiyon sayacı
  trailFitVersion: number;

  setConnectionStatus: (status: ConnectionStatus) => void;
  setSnapshot: (snapshot: VehiclePositionRecord[]) => void;
  batchUpdateVehicles: (updates: VehiclePositionRecord[]) => void;
  setSelectedVehicleId: (id: string | null) => void;
  setSearchQuery: (query: string) => void;
  setRouteQuery: (query: string) => void;
  setHistoryTrailPoints: (points: [number, number][]) => void;
  clearHistoryTrail: () => void;
  evictStaleVehicles: (maxAgeMs?: number) => void;
}

function mergeRecords(
  current: Record<string, VehiclePositionRecord>,
  incoming: VehiclePositionRecord[]
): Record<string, VehiclePositionRecord> {
  if (!incoming || incoming.length === 0) return current;

  const cutoff = Date.now() - VEHICLE_EVICTION_MS;
  const next = { ...current };
  let hasChanges = false;

  for (let i = 0; i < incoming.length; i++) {
    const item = incoming[i];
    if (!item.vehicleId) continue;

    const incomingTime = new Date(item.timestamp).getTime();
    // Kendime not: 30 dakikadan eski bayat araçların haritaya hayalet olarak girmesini önle
    if (isNaN(incomingTime) || incomingTime < cutoff) continue;

    const existing = next[item.vehicleId];
    if (!existing) {
      next[item.vehicleId] = item;
      hasChanges = true;
      continue;
    }

    const existingTime = new Date(existing.timestamp).getTime();

    if (incomingTime >= existingTime) {
      next[item.vehicleId] = item;
      hasChanges = true;
    }
  }

  return hasChanges ? next : current;
}

export const useVehicleStore = create<VehicleState>((set) => ({
  vehicles: {},
  connectionStatus: "Disconnected",
  selectedVehicleId: null,
  lastMessageReceivedAt: null,
  totalVehicleCount: 0,
  searchQuery: "",
  routeQuery: "",
  historyTrailPoints: [],
  trailFitVersion: 0,

  setConnectionStatus: (status) => set({ connectionStatus: status }),

  setSnapshot: (snapshot) => {
    const cutoff = Date.now() - VEHICLE_EVICTION_MS;
    const map: Record<string, VehiclePositionRecord> = {};
    
    for (let i = 0; i < snapshot.length; i++) {
      const v = snapshot[i];
      if (!v.vehicleId) continue;

      const vTime = new Date(v.timestamp).getTime();
      // İlk snapshot'ta da 30 dakikadan eski bayat araçları doğrudan filtrele
      if (!isNaN(vTime) && vTime >= cutoff) {
        map[v.vehicleId] = v;
      }
    }
    set({
      vehicles: map,
      totalVehicleCount: Object.keys(map).length,
      lastMessageReceivedAt: Date.now(),
    });
  },

  batchUpdateVehicles: (updates) => {
    set((state) => {
      const merged = mergeRecords(state.vehicles, updates);

      // CANLI İZ UZATMA: updates.find yerine mergeRecords çıktısını kontrol ediyoruz.
      // Yalnızca timestamp'i eskisinden kesinlikle daha yeni olan ve koordinatı değişen noktayı ekle.
      let nextTrail = state.historyTrailPoints;
      const selId = state.selectedVehicleId;

      if (selId && state.historyTrailPoints.length > 0) {
        const prev = state.vehicles[selId];
        const next = merged[selId];

        if (next && next !== prev && next.timestamp !== prev?.timestamp) {
          const lastPoint = state.historyTrailPoints[state.historyTrailPoints.length - 1];
          if (lastPoint[0] !== next.longitude || lastPoint[1] !== next.latitude) {
            nextTrail = [...state.historyTrailPoints, [next.longitude, next.latitude]];
          }
        }
      }

      return {
        vehicles: merged,
        historyTrailPoints: nextTrail,
        totalVehicleCount: Object.keys(merged).length,
        lastMessageReceivedAt: Date.now(),
      };
    });
  },

  // Aynı araca tekrar tıklandığında mevcut rotayı gereksiz yere silmeyen koruma
  setSelectedVehicleId: (id) =>
    set((state) =>
      state.selectedVehicleId === id
        ? state
        : { selectedVehicleId: id, historyTrailPoints: [] }
    ),

  setSearchQuery: (searchQuery) => set({ searchQuery }),
  setRouteQuery: (routeQuery) => set({ routeQuery }),

  // REST'ten yeni rota geldiğinde trailFitVersion'ı artırarak haritaya odaklanma emri ver
  setHistoryTrailPoints: (historyTrailPoints) =>
    set((state) => ({
      historyTrailPoints,
      trailFitVersion: state.trailFitVersion + 1,
    })),

  clearHistoryTrail: () => set({ historyTrailPoints: [] }),

  // Bellek koruması: Merkezi sabite göre (30 dakika) eski araçları temizle
  evictStaleVehicles: (maxAgeMs = VEHICLE_EVICTION_MS) => {
    set((state) => {
      const now = Date.now();
      const next: Record<string, VehiclePositionRecord> = {};
      let changed = false;

      for (const [id, vehicle] of Object.entries(state.vehicles)) {
        const vehicleTime = new Date(vehicle.timestamp).getTime();
        if (now - vehicleTime <= maxAgeMs) {
          next[id] = vehicle;
        } else {
          changed = true;
        }
      }

      if (!changed) return state;

      return {
        vehicles: next,
        totalVehicleCount: Object.keys(next).length,
      };
    });
  },
}));