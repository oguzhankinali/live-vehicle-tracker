import { create } from 'zustand';
import { HubConnection, HubConnectionBuilder, LogLevel, HttpTransportType, HubConnectionState, IRetryPolicy } from '@microsoft/signalr';
import { VehiclePositionRecord } from '../types/vehicle';

type ConnectionStatus = 'Connected' | 'Connecting' | 'Reconnecting' | 'Disconnected';

interface VehicleStore {
  vehicles: Record<string, VehiclePositionRecord>;
  connectionStatus: ConnectionStatus;
  selectedVehicleId: string | null;
  searchQuery: string;
  routeQuery: string;
  
  connect: () => void;
  disconnect: () => Promise<void>;
  setSelectedVehicleId: (id: string | null) => void;
  setSearchQuery: (query: string) => void;
  setRouteQuery: (query: string) => void;
}

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5086';

class InfiniteRetryPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(retryContext: { previousRetryCount: number }): number | null {
    const delays = [0, 2000, 5000, 10000];
    if (retryContext.previousRetryCount < delays.length) {
      return delays[retryContext.previousRetryCount];
    }
    return 30000;
  }
}

let activeConnection: HubConnection | null = null;
let startPromise: Promise<void> | null = null;
let isIntentionalDisconnect = false;

function mergeVehicles(
  current: Record<string, VehiclePositionRecord>,
  incomingList: VehiclePositionRecord[]
): Record<string, VehiclePositionRecord> {
  const updated = { ...current };

  for (let i = 0; i < incomingList.length; i++) {
    const incoming = incomingList[i];
    if (!incoming.vehicleId) continue;

    const existing = updated[incoming.vehicleId];
    const incomingTime = new Date(incoming.timestamp).getTime();
    const existingTime = existing ? new Date(existing.timestamp).getTime() : 0;

    if (!existing || incomingTime >= existingTime) {
      updated[incoming.vehicleId] = incoming;
    }
  }

  return updated;
}

export const useVehicleStore = create<VehicleStore>((set, get) => ({
  vehicles: {},
  connectionStatus: 'Disconnected',
  selectedVehicleId: null,
  searchQuery: '',
  routeQuery: '',

  setSelectedVehicleId: (id) => set({ selectedVehicleId: id }),
  setSearchQuery: (searchQuery) => set({ searchQuery }),
  setRouteQuery: (routeQuery) => set({ routeQuery }),

  connect: () => {
    // 1. Zaten bağlıysa veya bağlanma işlemi devam ediyorsa ikinci kez başlatma
    if (activeConnection && activeConnection.state !== HubConnectionState.Disconnected) {
      return;
    }
    if (startPromise) {
      return;
    }

    isIntentionalDisconnect = false;
    set({ connectionStatus: 'Connecting' });

    // REST başlangıç verisini paralel çek
    fetch(`${API_BASE_URL}/api/vehicles/latest`)
      .then(async (res) => {
        if (res.ok) {
          const data: VehiclePositionRecord[] = await res.json();
          set((state) => ({ vehicles: mergeVehicles(state.vehicles, data) }));
          console.log(`[REST] ${data.length} araç RAM'den yüklendi.`);
        }
      })
      .catch((err) => console.warn('[REST] İlk veri alınamadı:', err));

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/vehicles`, {
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets
      })
      .withAutomaticReconnect(new InfiniteRetryPolicy())
      .configureLogging(LogLevel.Warning)
      .build();

    activeConnection = connection;

    connection.onreconnecting(() => set({ connectionStatus: 'Reconnecting' }));
    connection.onreconnected(() => set({ connectionStatus: 'Connected' }));
    connection.onclose(() => {
      set({ connectionStatus: 'Disconnected' });
      activeConnection = null;
      startPromise = null;
    });

    connection.on('ReceiveSnapshot', (snapshot: VehiclePositionRecord[]) => {
      if (!Array.isArray(snapshot)) return;
      console.log(`[SignalR] Snapshot alındı: ${snapshot.length} araç.`);
      set((state) => ({ vehicles: mergeVehicles(state.vehicles, snapshot) }));
    });

    connection.on('ReceiveUpdates', (updates: VehiclePositionRecord[]) => {
      if (!Array.isArray(updates)) return;
      set((state) => ({ vehicles: mergeVehicles(state.vehicles, updates) }));
    });

    // Kendime not: start() işlemini Promise olarak saklıyoruz. React Strict Mode hemen stop()
    // çağırırsa el sıkışma bitmeden stop() yapılmasını engellemek için bu vaat beklenir.
    startPromise = connection.start()
      .then(() => {
        set({ connectionStatus: 'Connected' });
        console.log('[SignalR] Tekil WebSocket bağlantısı sağlandı.');
      })
      .catch((err) => {
        // Strict Mode unmount'u veya kasıtlı kapatma iptallerini hata sayıp konsolu kirletme
        if (err.name === 'AbortError' || isIntentionalDisconnect) {
          return;
        }
        console.error('[SignalR] Bağlantı hatası, tekrar denenecek:', err);
        activeConnection = null;
        set({ connectionStatus: 'Disconnected' });

        if (!isIntentionalDisconnect) {
          setTimeout(() => get().connect(), 5000);
        }
      })
      .finally(() => {
        startPromise = null;
      });
  },

  disconnect: async () => {
    isIntentionalDisconnect = true;

    // Eğer henüz start() işlemi devam ediyorsa, bitmesini bekle ki 'stop before start' hatası vermesin
    if (startPromise) {
      try {
        await startPromise;
      } catch {
        // İptal hatalarını yut
      }
    }

    if (activeConnection) {
      const conn = activeConnection;
      activeConnection = null;
      try {
        await conn.stop();
      } catch {
        // Kapanış hatalarını yut
      }
      set({ connectionStatus: 'Disconnected' });
    }
  }
}));