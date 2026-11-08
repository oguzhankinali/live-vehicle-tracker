import { useEffect } from "react";
import { vehicleSocketService } from "../services/vehicleSocket";
import { useVehicleStore } from "../store/useVehicleStore";

export function useVehicleStream() {
  const evictStaleVehicles = useVehicleStore((state) => state.evictStaleVehicles);

  useEffect(() => {
    vehicleSocketService.connect();

    // Bellekte biriken hayalet araçları 60 saniyede bir periyodik temizle
    const staleInterval = setInterval(() => {
      evictStaleVehicles(15 * 60 * 1000);
    }, 60 * 1000);

    return () => {
      clearInterval(staleInterval);
      vehicleSocketService.disconnect();
    };
  }, [evictStaleVehicles]);
}