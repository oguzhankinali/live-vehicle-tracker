import { describe, expect, it } from 'vitest';
import { vehicleReferenceSchema } from './getVehicleReference.schema';

describe('vehicleReferenceSchema', () => {
    it('accepts journey destination information', () => {
        const result = vehicleReferenceSchema.safeParse({
            line: null,
            stop: null,
            trip: {
                id: 'FIN:ServiceJourney:5726',
                publicCode: null,
                origin: 'Øksfjord ferjekai',
                destination: 'Hasvik',
            },
        });

        expect(result.success).toBe(true);

        if (result.success) {
            expect(result.data.trip?.destination).toBe('Hasvik');
        }
    });

    it('allows missing journey reference data', () => {
        const result = vehicleReferenceSchema.safeParse({
            line: null,
            stop: null,
            trip: null,
        });

        expect(result.success).toBe(true);
    });

    it('rejects invalid journey data', () => {
        const result = vehicleReferenceSchema.safeParse({
            line: null,
            stop: null,
            trip: {
                id: 'FIN:ServiceJourney:5726',
                publicCode: null,
                origin: null,
                destination: 123,
            },
        });

        expect(result.success).toBe(false);
    });
});
