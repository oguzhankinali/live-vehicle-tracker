import { create } from 'zustand';
import { HubConnection, HubConnectionBuilder, LogLevel, HttpTransportType, HubConnectionState } from '@microsoft/signalr';
import { VehiclePositionRecord } from '../types/vehicle';

type ConnectionStatus = 'Connected' | 'Connecting' | 'Reconnecting' | 'Disconnected';

interface VehicleStore {
  vehicles: Record<string, VehiclePositionRecord>;
  connectionStatus: ConnectionStatus;
  selectedVehicleId: string | null;
  
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  setSelectedVehicleId: (id: string | null) => void;
}

let globalConnection: HubConnection | null = null;

export const useVehicleStore = create<VehicleStore>((set, get) => ({
  vehicles: {},
  connectionStatus: 'Disconnected',
  selectedVehicleId: null,

  setSelectedVehicleId: (id) => set({ selectedVehicleId: id }),

  connect: async () => {
    if (globalConnection && (globalConnection.state === HubConnectionState.Connected || globalConnection.state === HubConnectionState.Connecting)) {
      return;
    }

    set({ connectionStatus: 'Connecting' });

    // 1. RAM'den ilk araçları çek
    try {
      const res = await fetch('http://localhost:5086/api/vehicles/latest');
      if (res.ok) {
        const data: any[] = await res.json();
        const initialMap: Record<string, VehiclePositionRecord> = {};
        data.forEach((v) => {
          const id = v.vehicleId ?? v.VehicleId;
          if (id) initialMap[id] = v;
        });
        set({ vehicles: initialMap });
        console.log(`[REST] ${data.length} araç RAM'den anında haritaya yüklendi.`);
      }
    } catch (err) {
      console.warn('[REST] Başlangıç verisi çekilemedi, SignalR bekleniyor:', err);
    }

    // 2. Canlı SignalR bağlantısı
    const connection = new HubConnectionBuilder()
      .withUrl('http://localhost:5086/hubs/vehicles', {
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    globalConnection = connection;

    connection.onreconnecting(() => set({ connectionStatus: 'Reconnecting' }));
    connection.onreconnected(() => set({ connectionStatus: 'Connected' }));
    connection.onclose(() => {
      set({ connectionStatus: 'Disconnected' });
      globalConnection = null;
    });

    connection.on('ReceiveSnapshot', (snapshot: any[]) => {
      if (!Array.isArray(snapshot)) return;
      console.log(`[SignalR] Snapshot alındı: ${snapshot.length} araç.`);
      set(() => {
        const newVehicles: Record<string, VehiclePositionRecord> = {};
        snapshot.forEach((v) => {
          const vId = v.vehicleId ?? v.VehicleId;
          if (vId) newVehicles[vId] = v;
        });
        return { vehicles: newVehicles };
      });
    });

    // 3. CANLI GÜNCELLEMELER (RÖNTGEN LOGU EKLENDİ)
    connection.on('ReceiveUpdates', (updates: any[]) => {
      if (!Array.isArray(updates)) return;
      
      // Kendime not: Backend'den her 3 saniyede bir kaç aracın güncellendiğini konsola basıyoruz
      console.log(`[SignalR Canlı] ⚡ ${updates.length} aracın konumu güncellendi!`);

      set((state) => {
        const updated = { ...state.vehicles };
        updates.forEach((incoming) => {
          const vId = incoming.vehicleId ?? incoming.VehicleId;
          if (!vId) return;

          const existing = updated[vId];
          const inTime = new Date(incoming.timestamp ?? incoming.Timestamp).getTime();
          const exTime = existing ? new Date(existing.timestamp ?? (existing as any).Timestamp).getTime() : 0;

          if (!existing || inTime >= exTime) {
            updated[vId] = incoming;
          }
        });
        return { vehicles: updated };
      });
    });

    try {
      await connection.start();
      set({ connectionStatus: 'Connected' });
      console.log('[SignalR] WebSocket canlı bağlantısı kuruldu!');
    } catch (error) {
      console.error('[SignalR] Bağlantı kurulamadı:', error);
      set({ connectionStatus: 'Disconnected' });
      globalConnection = null;
    }
  },

  disconnect: async () => {
    if (globalConnection) {
      await globalConnection.stop();
      globalConnection = null;
      set({ connectionStatus: 'Disconnected' });
    }
  }
}));