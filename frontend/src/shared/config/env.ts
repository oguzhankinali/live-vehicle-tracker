// Backend API ve WebSocket adreslerinin tek merkezden yönetildiği konfigürasyon
export const ENV = {
  API_BASE_URL: process.env.NEXT_PUBLIC_API_URL || "http://localhost:5086",
  WS_URL: (process.env.NEXT_PUBLIC_API_URL || "http://localhost:5086") + "/hubs/vehicles",
} as const;