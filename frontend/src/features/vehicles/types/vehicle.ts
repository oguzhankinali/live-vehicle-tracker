export enum VehicleStopStatus {
  IncomingAt = 0,
  StoppedAt = 1,
  InTransitTo = 2
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
  NotBoardable = 8
}

export interface VehiclePositionRecord {
  feedEntityId: string;
  vehicleId: string;
  vehicleLabel: string | null;
  tripId: string | null;
  routeId: string | null;
  directionId: number | null;
  latitude: number;
  longitude: number;
  bearing: number | null;
  speed: number | null;
  stopId: string | null;
  currentStopSequence: number | null;
  currentStatus: VehicleStopStatus | null;
  occupancyStatus: OccupancyStatus | null;
  timestamp: string;
  receivedAtUtc: string;
}

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