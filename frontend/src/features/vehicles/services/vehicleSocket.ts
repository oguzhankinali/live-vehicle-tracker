import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
  HttpTransportType,
  HubConnectionState,
} from "@microsoft/signalr";
import { ENV } from "@/shared/config/env";
import { useVehicleStore } from "../store/useVehicleStore";
import { VehiclePositionRecord } from "../types/vehicle";

class VehicleSocketService {
  private connection: HubConnection | null = null;
  private isConnecting = false;
  private reconnectTimer: NodeJS.Timeout | null = null;

  public async connect(): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      return;
    }
    if (this.isConnecting) return;

    this.isConnecting = true;
    useVehicleStore.getState().setConnectionStatus("Connecting");

    // İlk açılışta REST API'den güncel durumu çek
    this.fetchLatestSnapshot();

    this.connection = new HubConnectionBuilder()
      .withUrl(ENV.WS_URL, {
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.onreconnecting(() => {
      useVehicleStore.getState().setConnectionStatus("Reconnecting");
    });

    // Şartname 3.2: Yeniden bağlanıldığında kayıp verileri kurtarmak için REST ile tazele
    this.connection.onreconnected(() => {
      useVehicleStore.getState().setConnectionStatus("Connected");
      this.fetchLatestSnapshot();
    });

    this.connection.onclose(() => {
      useVehicleStore.getState().setConnectionStatus("Disconnected");
      this.scheduleReconnect();
    });

    // Sunucudan gelen ilk tam liste (Ezmek yerine akıllıca birleştiriyoruz)
    this.connection.on("ReceiveSnapshot", (snapshot: VehiclePositionRecord[]) => {
      if (Array.isArray(snapshot)) {
        useVehicleStore.getState().batchUpdateVehicles(snapshot);
      }
    });

    // Sunucudan 3 saniyede bir gelen değişen araçlar
    this.connection.on("ReceiveUpdates", (updates: VehiclePositionRecord[]) => {
      if (Array.isArray(updates)) {
        useVehicleStore.getState().batchUpdateVehicles(updates);
      }
    });

    try {
      await this.connection.start();
      useVehicleStore.getState().setConnectionStatus("Connected");
    } catch {
      useVehicleStore.getState().setConnectionStatus("Disconnected");
      this.scheduleReconnect();
    } finally {
      this.isConnecting = false;
    }
  }

  public async disconnect(): Promise<void> {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }

    if (this.connection) {
      try {
        await this.connection.stop();
      } finally {
        this.connection = null;
        this.isConnecting = false;
        useVehicleStore.getState().setConnectionStatus("Disconnected");
      }
    }
  }

  private scheduleReconnect(): void {
    if (this.reconnectTimer) return;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.connect();
    }, 5000);
  }

  private async fetchLatestSnapshot(): Promise<void> {
    try {
      const response = await fetch(`${ENV.API_BASE_URL}/api/vehicles/latest`);
      if (response.ok) {
        const data: VehiclePositionRecord[] = await response.json();
        useVehicleStore.getState().batchUpdateVehicles(data);
      }
    } catch {
      // Ağ hatasında WebSocket yayını denemeye devam eder
    }
  }
}

export const vehicleSocketService = new VehicleSocketService();