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
  
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  setSelectedVehicleId: (id: string | null) => void;
  setSearchQuery: (query: string) => void;
  setRouteQuery: (query: string) => void;
}

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5086';

// güncelleme not: Şartname gereği backend dakikalarca kapalı kalsa bile pes etmeyen sonsuz geri çekilmeli (0, 2, 5, 10, max 30 sn) özel yeniden bağlanma politikası.
class InfiniteRetryPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(retryContext: { previousRetryCount: number }): number | null {
    const delays = [0, 2000, 5000, 10000];
    if (retryContext.previousRetryCount < delays.length) {
      return delays[retryContext.previousRetryCount];
    }
    return 30000; // 30 saniyede bir sonsuza kadar dene
  }
}

// React Strict Mode çift mount tuzağını engellemek için singleton lock
let activeConnection: HubConnection | null = null;
let connectionPromise: Promise<void> | null = null;

// Gelen araçları mevcut listeye timestamp kontrolüyle birleştiren saf fonksiyon
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

    // Kendime not: Snapshot veya Delta fark etmez; sadece daha yeni veya eşit veriyi kabul et.
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

  connect: async () => {
    // Çift bağlantıyı engelle: Zaten bağlanmışsa veya şu an bağlanma vaadi sürüyorsa çık
    if (activeConnection?.state === HubConnectionState.Connected) return;
    if (connectionPromise) return connectionPromise;

    set({ connectionStatus: 'Connecting' });

    // REST'ten hızlı ilk yükleme (0 ms RAM)
    try {
      const res = await fetch(`${API_BASE_URL}/api/vehicles/latest`);
      if (res.ok) {
        const initialData: VehiclePositionRecord[] = await res.json();
        set((state) => ({
          vehicles: mergeVehicles(state.vehicles, initialData)
        }));
        console.log(`[REST] ${initialData.length} araç RAM'den yüklendi.`);
      }
    } catch (err) {
      console.warn('[REST] İlk veri alınamadı, SignalR bekleniyor:', err);
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/vehicles`, {
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets
      })
      .withAutomaticReconnect(new InfiniteRetryPolicy())
      .configureLogging(LogLevel.Warning) // Konsolu gereksiz loglardan temizle
      .build();

    activeConnection = connection;

    connection.onreconnecting(() => set({ connectionStatus: 'Reconnecting' }));
    connection.onreconnected(() => set({ connectionStatus: 'Connected' }));
    connection.onclose(() => {
      set({ connectionStatus: 'Disconnected' });
      activeConnection = null;
    });

    connection.on('ReceiveSnapshot', (snapshot: VehiclePositionRecord[]) => {
      if (!Array.isArray(snapshot)) return;
      console.log(`[SignalR] Snapshot alındı: ${snapshot.length} araç.`);
      set((state) => ({
        vehicles: mergeVehicles(state.vehicles, snapshot)
      }));
    });

    connection.on('ReceiveUpdates', (updates: VehiclePositionRecord[]) => {
      if (!Array.isArray(updates)) return;
      set((state) => ({
        vehicles: mergeVehicles(state.vehicles, updates)
      }));
    });

    connectionPromise = connection.start()
      .then(() => {
        set({ connectionStatus: 'Connected' });
        console.log('[SignalR] Tekil WebSocket bağlantısı sağlandı.');
      })
      .catch((err) => {
        console.error('[SignalR] İlk bağlantı başarısız, tekrar denenecek:', err);
        set({ connectionStatus: 'Disconnected' });
        activeConnection = null;
        // İlk bağlantı çökerse 5 saniye sonra otomatik tekrar dene
        setTimeout(() => {
          connectionPromise = null;
          get().connect();
        }, 5000);
      })
      .finally(() => {
        connectionPromise = null;
      });

    return connectionPromise;
  },

  disconnect: async () => {
    if (activeConnection) {
      const conn = activeConnection;
      activeConnection = null;
      connectionPromise = null;
      await conn.stop();
      set({ connectionStatus: 'Disconnected' });
    }
  }
}));