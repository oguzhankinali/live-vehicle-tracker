import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { ENV } from '@/shared/config/env';

const lineReferenceSchema = z.object({
    id: z.string(),
    publicCode: z.string().nullable(),
    name: z.string().nullable(),
    transportMode: z.string().nullable(),
    operatorName: z.string().nullable(),
});

const stopReferenceSchema = z.object({
    id: z.string(),
    name: z.string(),
    stopPlaceName: z.string().nullable(),
});

export const vehicleReferenceSchema = z.object({
    line: lineReferenceSchema.nullable(),
    stop: stopReferenceSchema.nullable(),
});

export type VehicleReference = z.infer<typeof vehicleReferenceSchema>;

export async function fetchVehicleReference(
    routeId: string | null,
    stopId: string | null,
    signal?: AbortSignal
): Promise<VehicleReference> {
    const params = new URLSearchParams();

    if (routeId) params.set('routeId', routeId);
    if (stopId) params.set('stopId', stopId);

    const response = await fetch(
        `${ENV.API_BASE_URL}/api/vehicles/reference?${params.toString()}`,
        { signal }
    );

    if (!response.ok) {
        throw new Error(`FETCH_VEHICLE_REFERENCE_FAILED_${response.status}`);
    }

    const json = await response.json();

    return vehicleReferenceSchema.parse(json);
}

export function useVehicleReference(
    routeId: string | null | undefined,
    stopId: string | null | undefined,
    enabled = true
) {
    const normalizedRouteId = routeId?.trim() || null;
    const normalizedStopId = stopId?.trim() || null;

    return useQuery({
        queryKey: [
            'vehicleReference',
            normalizedRouteId,
            normalizedStopId,
        ],
        queryFn: ({ signal }) =>
            fetchVehicleReference(
                normalizedRouteId,
                normalizedStopId,
                signal
            ),
        enabled:
            enabled &&
            Boolean(normalizedRouteId || normalizedStopId),
        staleTime: 12 * 60 * 60 * 1000,
        gcTime: 24 * 60 * 60 * 1000,
        retry: 1,
        refetchOnWindowFocus: false,
    });
}
