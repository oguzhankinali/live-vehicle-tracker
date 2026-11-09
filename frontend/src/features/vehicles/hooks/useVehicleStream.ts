import { useEffect } from "react";
import { vehicleSocketService } from "../services/vehicleSocket";
import { useVehicleStore } from "../store/useVehicleStore";
import { VEHICLE_EVICTION_MS, VEHICLE_EVICTION_INTERVAL_MS } from "../constants/timing";

export function useVehicleStream() {
  const evictStaleVehicles = useVehicleStore((state) => state.evictStaleVehicles);

  useEffect(() => {
    vehicleSocketService.connect();

    // Bellekte biriken hayalet araçları merkezi sabit süresine göre periyodik temizle
    const staleInterval = setInterval(() => {
      evictStaleVehicles(VEHICLE_EVICTION_MS);
    }, VEHICLE_EVICTION_INTERVAL_MS);

    return () => {
      clearInterval(staleInterval);
      vehicleSocketService.disconnect();
    };
  }, [evictStaleVehicles]);
}