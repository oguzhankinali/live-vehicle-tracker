
import { describe, expect, it } from 'vitest';
import { systemStatusSchema } from './systemStatus';

const validSystemStatus = {
    lastSuccessfulPollUtc: '2026-11-16T12:00:00Z',
    recordsWrittenLastHour: 250,
    totalRecordsWritten: 12500,
    failedPollAttempts: 0,
    connectedClientCount: 3,
    uptimeSeconds: 3600,
};

describe('systemStatusSchema', () => {
    it('accepts a valid system status', () => {
        expect(
            systemStatusSchema.safeParse(validSystemStatus).success
        ).toBe(true);
    });

    it('accepts null for the last successful poll', () => {
        expect(
            systemStatusSchema.safeParse({
                ...validSystemStatus,
                lastSuccessfulPollUtc: null,
            }).success
        ).toBe(true);
    });

    it('rejects a missing required metric', () => {
        const { connectedClientCount: _count, ...withoutCount } =
            validSystemStatus;

        expect(
            systemStatusSchema.safeParse(withoutCount).success
        ).toBe(false);
    });

    it('rejects metrics supplied as strings', () => {
        expect(
            systemStatusSchema.safeParse({
                ...validSystemStatus,
                uptimeSeconds: '3600',
            }).success
        ).toBe(false);
    });

    it('rejects null for a required numeric metric', () => {
        expect(
            systemStatusSchema.safeParse({
                ...validSystemStatus,
                totalRecordsWritten: null,
            }).success
        ).toBe(false);
    });
});
