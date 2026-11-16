
import { describe, expect, it } from 'vitest';
import {
    OccupancyStatus,
    VehicleStopStatus,
    vehiclePositionRecordSchema,
} from './vehicle';

const validVehicle = {
    feedEntityId: 'entity-101',
    vehicleId: '12345',
    latitude: 59.9139,
    longitude: 10.7522,
    timestamp: '2026-11-16T12:00:00Z',
    receivedAtUtc: '2026-11-16T12:00:02Z',
};

describe('vehiclePositionRecordSchema', () => {
    it('accepts a valid vehicle position', () => {
        const result = vehiclePositionRecordSchema.safeParse(validVehicle);

        expect(result.success).toBe(true);
    });

    it('accepts nullable and optional fields', () => {
        const result = vehiclePositionRecordSchema.safeParse({
            ...validVehicle,
            vehicleLabel: null,
            tripId: null,
            routeId: 'R31',
            directionId: null,
            bearing: null,
            speed: 12.5,
            stopId: null,
            currentStopSequence: null,
            currentStatus: null,
            occupancyStatus: null,
        });

        expect(result.success).toBe(true);
    });

    it('accepts defined numeric enum values', () => {
        const result = vehiclePositionRecordSchema.safeParse({
            ...validVehicle,
            currentStatus: VehicleStopStatus.InTransitTo,
            occupancyStatus: OccupancyStatus.FewSeatsAvailable,
        });

        expect(result.success).toBe(true);
    });

    it('rejects invalid numeric enum values', () => {
        const result = vehiclePositionRecordSchema.safeParse({
            ...validVehicle,
            currentStatus: 99,
        });

        expect(result.success).toBe(false);
    });

    it('rejects numeric enum values supplied as strings', () => {
        const result = vehiclePositionRecordSchema.safeParse({
            ...validVehicle,
            currentStatus: '1',
        });

        expect(result.success).toBe(false);
    });

    it('rejects a missing latitude', () => {
        const { latitude: _latitude, ...withoutLatitude } = validVehicle;

        expect(
            vehiclePositionRecordSchema.safeParse(withoutLatitude).success
        ).toBe(false);
    });

    it('rejects a string longitude', () => {
        const result = vehiclePositionRecordSchema.safeParse({
            ...validVehicle,
            longitude: '10.7522',
        });

        expect(result.success).toBe(false);
    });

    it('rejects missing required identifiers', () => {
        const result = vehiclePositionRecordSchema.safeParse({
            latitude: 59.9139,
            longitude: 10.7522,
            timestamp: validVehicle.timestamp,
            receivedAtUtc: validVehicle.receivedAtUtc,
        });

        expect(result.success).toBe(false);
    });
});
