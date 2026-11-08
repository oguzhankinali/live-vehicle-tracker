import { useQuery } from "@tanstack/react-query";
import { ENV } from "@/shared/config/env";
import { systemStatusSchema, SystemStatus } from "../types/systemStatus";

async function fetchSystemStatus(): Promise<SystemStatus> {
  const res = await fetch(`${ENV.API_BASE_URL}/api/system/status`);
  if (!res.ok) {
    throw new Error(`SYSTEM_STATUS_FETCH_FAILED_${res.status}`);
  }
  const raw = await res.json();
  return systemStatusSchema.parse(raw);
}

export function useSystemStatus(enabled = true) {
  return useQuery({
    queryKey: ["systemStatus"],
    queryFn: fetchSystemStatus,
    enabled,
    // Şartname 4.3: Periyodik güncelleme (5 saniyede bir otomatik tazeleyen kural)
    refetchInterval: 5000,
    retry: 1,
    staleTime: 4000,
  });
}