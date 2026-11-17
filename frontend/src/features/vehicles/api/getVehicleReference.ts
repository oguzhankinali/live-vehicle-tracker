import { useQuery } from '@tanstack/react-query';
import { ENV } from '@/shared/config/env';
import {
    vehicleReferenceSchema,
    type VehicleReference,
} from './getVehicleReference.schema';

export {
    vehicleReferenceSchema,
} from './getVehicleReference.schema';

export type {
    VehicleReference,
} from './getVehicleReference.schema';

export async function fetchVehicleReference(
    routeId: string | null,
    stopId: string | null,
    tripId: string | null,
    signal?: AbortSignal
): Promise<VehicleReference> {
    const params = new URLSearchParams();

    if (routeId) params.set('routeId', routeId);
    if (stopId) params.set('stopId', stopId);
    if (tripId) params.set('tripId', tripId);

    const response = await fetch(
        `${ENV.API_BASE_URL}/api/vehicles/reference?${params.toString()}`,
        { signal }
    );

    if (!response.ok) {
        throw new Error(
            `FETCH_VEHICLE_REFERENCE_FAILED_${response.status}`
        );
    }

    const json = await response.json();

    return vehicleReferenceSchema.parse(json);
}

export function useVehicleReference(
    routeId: string | null | undefined,
    stopId: string | null | undefined,
    tripId: string | null | undefined,
    enabled = true
) {
    const normalizedRouteId = routeId?.trim() || null;
    const normalizedStopId = stopId?.trim() || null;
    const normalizedTripId = tripId?.trim() || null;

    return useQuery({
        queryKey: [
            'vehicleReference',
            normalizedRouteId,
            normalizedStopId,
            normalizedTripId,
        ],
        queryFn: ({ signal }) =>
            fetchVehicleReference(
                normalizedRouteId,
                normalizedStopId,
                normalizedTripId,
                signal
            ),
        enabled:
            enabled &&
            Boolean(
                normalizedRouteId ||
                normalizedStopId ||
                normalizedTripId
            ),
        staleTime: 12 * 60 * 60 * 1000,
        gcTime: 24 * 60 * 60 * 1000,
        retry: 1,
        refetchOnWindowFocus: false,
    });
}
