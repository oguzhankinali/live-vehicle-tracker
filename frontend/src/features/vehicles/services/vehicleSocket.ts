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
  private queue: Promise<void> = Promise.resolve();
  private stopped = false;
  private reconnectTimer: NodeJS.Timeout | null = null;

  public connect(): Promise<void> {
    this.queue = this.queue.then(() => this.doConnect()).catch(() => {});
    return this.queue;
  }

  public disconnect(): Promise<void> {
    this.queue = this.queue.then(() => this.doDisconnect()).catch(() => {});
    return this.queue;
  }

  private async doConnect(): Promise<void> {
    this.stopped = false;

    // Bağlantı zaten varsa ve Disconnected değilse (örn. Reconnecting) ikinci bağlantı açma
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    useVehicleStore.getState().setConnectionStatus("Connecting");
    this.fetchLatestSnapshot();

    if (!this.connection) {
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

      this.connection.onreconnected(() => {
        useVehicleStore.getState().setConnectionStatus("Connected");
        this.fetchLatestSnapshot();
      });

      this.connection.onclose(() => {
        useVehicleStore.getState().setConnectionStatus("Disconnected");
        this.scheduleReconnect();
      });

      this.connection.on("ReceiveSnapshot", (snapshot: VehiclePositionRecord[]) => {
        if (Array.isArray(snapshot)) {
          useVehicleStore.getState().batchUpdateVehicles(snapshot);
        }
      });

      this.connection.on("ReceiveUpdates", (updates: VehiclePositionRecord[]) => {
        if (Array.isArray(updates)) {
          useVehicleStore.getState().batchUpdateVehicles(updates);
        }
      });
    }

    try {
      await this.connection.start();
      if (this.stopped) {
        // start() tamamlanana kadar unmount/disconnect çağrıldıysa hemen durdur
        await this.connection.stop();
        useVehicleStore.getState().setConnectionStatus("Disconnected");
      } else {
        useVehicleStore.getState().setConnectionStatus("Connected");
      }
    } catch {
      useVehicleStore.getState().setConnectionStatus("Disconnected");
      this.scheduleReconnect();
    }
  }

  private async doDisconnect(): Promise<void> {
    this.stopped = true;
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }

    if (this.connection) {
      try {
        await this.connection.stop();
      } finally {
        this.connection = null;
        useVehicleStore.getState().setConnectionStatus("Disconnected");
      }
    }
  }

  private scheduleReconnect(): void {
    if (this.stopped || this.reconnectTimer) return;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      if (!this.stopped) {
        this.connect();
      }
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
      // Hata durumunda WebSocket akışı yeniden bağlanmayı dener
    }
  }
}

export const vehicleSocketService = new VehicleSocketService();