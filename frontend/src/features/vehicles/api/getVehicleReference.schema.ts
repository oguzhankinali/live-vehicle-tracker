import { z } from 'zod';

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

const tripReferenceSchema = z.object({
    id: z.string(),
    publicCode: z.string().nullable(),
    origin: z.string().nullable(),
    destination: z.string().nullable(),
});

export const vehicleReferenceSchema = z.object({
    line: lineReferenceSchema.nullable(),
    stop: stopReferenceSchema.nullable(),
    trip: tripReferenceSchema.nullable(),
});

export type VehicleReference = z.infer<
    typeof vehicleReferenceSchema
>;
