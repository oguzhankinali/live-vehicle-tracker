import { VehiclePositionRecord } from "../types/vehicle";

// Harita katmanı ve filtre çubuğu istatistiğinin birebir aynı araçları eşleştirmesini sağlayan saf yüklem (predicate)
export function matchesFilter(
  vehicle: VehiclePositionRecord,
  searchQuery: string,
  routeQuery: string
): boolean {
  const s = searchQuery.trim().toLowerCase();
  const r = routeQuery.trim().toLowerCase();

  // Araç numarası (vehicleId) veya araç tabelası/etiketi (vehicleLabel) eşleşmesi
  const matchesSearch =
    !s ||
    vehicle.vehicleId.toLowerCase().includes(s) ||
    (vehicle.vehicleLabel != null && vehicle.vehicleLabel.toLowerCase().includes(s));

  // Hat kodu (routeId) eşleşmesi
  const matchesRoute =
    !r || (vehicle.routeId != null && vehicle.routeId.toLowerCase().includes(r));

  return matchesSearch && matchesRoute;
}