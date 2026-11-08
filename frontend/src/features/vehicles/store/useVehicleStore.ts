import { create } from "zustand";
import { VehiclePositionRecord } from "../types/vehicle";

export type ConnectionStatus = "Connected" | "Connecting" | "Reconnecting" | "Disconnected";

interface VehicleState {
  vehicles: Record<string, VehiclePositionRecord>;
  connectionStatus: ConnectionStatus;
  selectedVehicleId: string | null;
  lastMessageReceivedAt: number | null;
  totalVehicleCount: number;

  // Arama ve Filtre Durumları
  searchQuery: string;
  routeQuery: string;

  // Haritada çizilen geçmiş rota koordinatları [lon, lat]
  historyTrailPoints: [number, number][];

  // Aksiyonlar
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

// Gelen yeni verileri mevcut harita verisiyle akıllıca birleştiren saf yardımcı fonksiyon
function mergeRecords(
  current: Record<string, VehiclePositionRecord>,
  incoming: VehiclePositionRecord[]
): Record<string, VehiclePositionRecord> {
  if (!incoming || incoming.length === 0) return current;

  const next = { ...current };
  let hasChanges = false;

  for (let i = 0; i < incoming.length; i++) {
    const item = incoming[i];
    if (!item.vehicleId) continue;

    const existing = next[item.vehicleId];
    if (!existing) {
      next[item.vehicleId] = item;
      hasChanges = true;
      continue;
    }

    // Zaman damgası kontrolü: Yalnızca mevcut veriden daha yeni ölçüm geldiyse güncelle
    const incomingTime = new Date(item.timestamp).getTime();
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

  setConnectionStatus: (status) => set({ connectionStatus: status }),

  setSnapshot: (snapshot) => {
    const map: Record<string, VehiclePositionRecord> = {};
    for (let i = 0; i < snapshot.length; i++) {
      const v = snapshot[i];
      if (v.vehicleId) {
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
      return {
        vehicles: merged,
        totalVehicleCount: Object.keys(merged).length,
        lastMessageReceivedAt: Date.now(),
      };
    });
  },

  setSelectedVehicleId: (id) => set({ selectedVehicleId: id, historyTrailPoints: [] }),
  setSearchQuery: (searchQuery) => set({ searchQuery }),
  setRouteQuery: (routeQuery) => set({ routeQuery }),
  setHistoryTrailPoints: (historyTrailPoints) => set({ historyTrailPoints }),
  clearHistoryTrail: () => set({ historyTrailPoints: [] }),

  // Bellek sızıntısını ve hayalet araçları önlemek için belirlenen süreden eski araçları siler
  evictStaleVehicles: (maxAgeMs = 15 * 60 * 1000) => {
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