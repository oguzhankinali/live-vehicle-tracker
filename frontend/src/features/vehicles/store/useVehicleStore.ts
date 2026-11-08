import { create } from "zustand";
import { VehiclePositionRecord } from "../types/vehicle";

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

    const incomingTime = new Date(item.timestamp).getTime();
    const existingTime = new Date(existing.timestamp).getTime();

    if (incomingTime >= existingTime) {
      next[item.vehicleId] = item;
      hasChanges = true;
    }
  }

  return hasChanges ? next : current;
}

export const useVehicleStore = create<VehicleState>((set, get) => ({
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

      // CANLI İZ UZATMA: Eğer seçili bir araç varsa ve bu güncelleme paketinde yer alıyorsa,
      // rota çizgisinin ucuna bu yeni konumu ekle (sıfır REST maliyeti)
      let nextTrail = state.historyTrailPoints;
      if (state.selectedVehicleId && state.historyTrailPoints.length > 0) {
        const liveMatch = updates.find((u) => u.vehicleId === state.selectedVehicleId);
        if (liveMatch) {
          const lastPoint = state.historyTrailPoints[state.historyTrailPoints.length - 1];
          // Koordinat değiştiyse ekle
          if (lastPoint[0] !== liveMatch.longitude || lastPoint[1] !== liveMatch.latitude) {
            nextTrail = [...state.historyTrailPoints, [liveMatch.longitude, liveMatch.latitude]];
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

  setSelectedVehicleId: (id) => set({ selectedVehicleId: id, historyTrailPoints: [] }),
  setSearchQuery: (searchQuery) => set({ searchQuery }),
  setRouteQuery: (routeQuery) => set({ routeQuery }),
  setHistoryTrailPoints: (historyTrailPoints) => set({ historyTrailPoints }),
  clearHistoryTrail: () => set({ historyTrailPoints: [] }),

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