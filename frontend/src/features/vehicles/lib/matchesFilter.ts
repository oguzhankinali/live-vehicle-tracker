import { VehiclePositionRecord } from "../types/vehicle";

export function matchesFilter(
  vehicle: VehiclePositionRecord,
  searchQuery: string,
  routeQuery: string
): boolean {
  const search = searchQuery.trim().toLowerCase();
  const route = routeQuery.trim().toLowerCase();

  const matchesSearch =
    !search ||
    vehicle.vehicleId.toLowerCase().includes(search) ||
    (vehicle.vehicleLabel?.toLowerCase().includes(search) ?? false) ||
    (vehicle.routeId?.toLowerCase().includes(search) ?? false);

  const matchesRoute =
    !route ||
    (vehicle.routeId?.toLowerCase().includes(route) ?? false);

  return matchesSearch && matchesRoute;
}