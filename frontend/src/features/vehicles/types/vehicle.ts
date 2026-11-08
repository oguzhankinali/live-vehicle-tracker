import { z } from "zod";

export enum VehicleStopStatus {
  IncomingAt = 0,
  StoppedAt = 1,
  InTransitTo = 2,
}

export enum OccupancyStatus {
  Empty = 0,
  ManySeatsAvailable = 1,
  FewSeatsAvailable = 2,
  StandingRoomOnly = 3,
  CrushedStandingRoomOnly = 4,
  Full = 5,
  NotAcceptingPassengers = 6,
  NoDataAvailable = 7,
  NotBoardable = 8,
}

// Backend VehiclePositionRecord modelinin Zod şeması
export const vehiclePositionRecordSchema = z.object({
  feedEntityId: z.string(),
  vehicleId: z.string(),
  vehicleLabel: z.string().nullable().optional(),
  tripId: z.string().nullable().optional(),
  routeId: z.string().nullable().optional(),
  directionId: z.number().nullable().optional(),
  latitude: z.number(),
  longitude: z.number(),
  bearing: z.number().nullable().optional(),
  speed: z.number().nullable().optional(),
  stopId: z.string().nullable().optional(),
  currentStopSequence: z.number().nullable().optional(),
  currentStatus: z.nativeEnum(VehicleStopStatus).nullable().optional(),
  occupancyStatus: z.nativeEnum(OccupancyStatus).nullable().optional(),
  timestamp: z.string(),
  receivedAtUtc: z.string(),
});

// TypeScript tipini doğrudan Zod şemasından türetiyoruz (çift tanım hatasını önler)
export type VehiclePositionRecord = z.infer<typeof vehiclePositionRecordSchema>;

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface SystemStatusDto {
  lastSuccessfulPollUtc: string | null;
  recordsWrittenLastHour: number;
  totalRecordsWritten: number;
  failedPollAttempts: number;
  connectedClientCount: number;
  uptimeSeconds: number;
}