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
  private isExplicitlyStopped = false;

  public async connect(): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }
    if (this.isConnecting) return;

    this.isConnecting = true;
    this.isExplicitlyStopped = false;
    useVehicleStore.getState().setConnectionStatus("Connecting");

    // İlk açılışta REST API'den anlık snapshot çekiyoruz
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

    // Şartname 3.2: Reconnect olduğunda kayıp verileri kurtarmak için REST üzerinden güncel durumu tazele
    this.connection.onreconnected(() => {
      useVehicleStore.getState().setConnectionStatus("Connected");
      this.fetchLatestSnapshot();
    });

    this.connection.onclose(() => {
      useVehicleStore.getState().setConnectionStatus("Disconnected");
      if (!this.isExplicitlyStopped) {
        setTimeout(() => this.connect(), 5000);
      }
    });

    // Sunucudan gelen ilk tam liste
    this.connection.on("ReceiveSnapshot", (snapshot: VehiclePositionRecord[]) => {
      if (Array.isArray(snapshot)) {
        useVehicleStore.getState().setSnapshot(snapshot);
      }
    });

    // Sunucudan 3 saniyede bir gelen değişen araçlar listesi
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
      if (!this.isExplicitlyStopped) {
        setTimeout(() => this.connect(), 5000);
      }
    } finally {
      this.isConnecting = false;
    }
  }

  public async disconnect(): Promise<void> {
    this.isExplicitlyStopped = true;
    if (this.connection) {
      try {
        await this.connection.stop();
      } finally {
        this.connection = null;
        useVehicleStore.getState().setConnectionStatus("Disconnected");
      }
    }
  }

  // Kopukluk anında veya ilk yüklemede belleği güncelleyen REST çağrısı
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