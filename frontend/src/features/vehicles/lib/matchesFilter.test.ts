import { describe, expect, it } from 'vitest';
import { matchesFilter } from './matchesFilter';
import type { VehiclePositionRecord } from '../types/vehicle';

const vehicles = [
    {
        feedEntityId: 'entity-1',
        vehicleId: '1001',
        vehicleLabel: 'Local Bus',
        routeId: 'VKT:Line:54',
        latitude: 59.9,
        longitude: 10.7,
        timestamp: '2026-11-16T12:00:00Z',
        receivedAtUtc: '2026-11-16T12:00:02Z',
    },
    {
        feedEntityId: 'entity-2',
        vehicleId: '1002',
        vehicleLabel: 'City Bus',
        routeId: 'VKT:Line:54',
        latitude: 59.8,
        longitude: 10.6,
        timestamp: '2026-11-16T12:00:00Z',
        receivedAtUtc: '2026-11-16T12:00:02Z',
    },
    {
        feedEntityId: 'entity-3',
        vehicleId: '1003',
        vehicleLabel: 'Airport Express',
        routeId: 'VKT:Line:20',
        latitude: 59.7,
        longitude: 10.5,
        timestamp: '2026-11-16T12:00:00Z',
        receivedAtUtc: '2026-11-16T12:00:02Z',
    },
] satisfies VehiclePositionRecord[];

function filterVehicles(search: string, route: string) {
    return vehicles
        .filter((vehicle) => matchesFilter(vehicle, search, route))
        .map((vehicle) => vehicle.vehicleId);
}

describe('matchesFilter', () => {
    it('returns all vehicles when filters are empty', () => {
        expect(filterVehicles('', '')).toEqual([
            '1001',
            '1002',
            '1003',
        ]);
    });

    it('searches by vehicle ID', () => {
        expect(filterVehicles('1001', '')).toEqual(['1001']);
    });

    it('searches by vehicle label', () => {
        expect(filterVehicles('EXPRESS', '')).toEqual(['1003']);
    });

    it('includes route ID in general search', () => {
        expect(filterVehicles('line:54', '')).toEqual([
            '1001',
            '1002',
        ]);
    });

    it('supports partial route filtering', () => {
        expect(filterVehicles('', '54')).toEqual([
            '1001',
            '1002',
        ]);
    });

    it('combines search and route filters', () => {
        expect(filterVehicles('1002', '54')).toEqual(['1002']);
    });

    it('matches without case sensitivity', () => {
        expect(filterVehicles('airport', '')).toEqual(['1003']);

        expect(filterVehicles('', 'vkt:LINE:54')).toEqual([
            '1001',
            '1002',
        ]);
    });

    it('ignores surrounding whitespace', () => {
        expect(filterVehicles(' 1001 ', ' 54 ')).toEqual([
            '1001',
        ]);
    });
});
