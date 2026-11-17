import { useTranslations } from 'next-intl';
import {
    OccupancyStatus,
    VehicleStopStatus,
    type VehiclePositionRecord,
} from '../types/vehicle';

import type { VehicleReference } from '../api/getVehicleReference.schema';

type Props = {
    vehicle: VehiclePositionRecord;
    reference: VehicleReference | undefined;
    isReferencePending: boolean;
    diffSec: number;
    diffMin: number;
};

type DetailRowProps = {
    label: string;
    value: string;
    title?: string;
};

function DetailRow({ label, value, title }: DetailRowProps) {
    return (
        <div className="flex items-start justify-between gap-3 rounded-lg border border-zinc-800 bg-zinc-800/30 p-2.5">
            <span className="shrink-0 text-zinc-400">
                {label}
            </span>

            <span
                className="min-w-0 text-right font-medium text-zinc-200 break-words [overflow-wrap:anywhere]"
                title={title}
            >
                {value}
            </span>
        </div>
    );
}

export function VehicleDetailContent({
    vehicle,
    reference,
    isReferencePending,
    diffSec,
    diffMin,
}: Props) {
    const t = useTranslations('VehicleDetail');

    const routeDisplay =
        reference?.line?.publicCode ||
        vehicle.routeId ||
        '-';

    const tripDisplay =
        reference?.trip?.origin && reference?.trip?.destination
            ? `${reference.trip.origin} → ${reference.trip.destination}`
            : reference?.trip?.destination ||
            reference?.trip?.publicCode ||
            vehicle.tripId ||
            '-';

    const stopDisplay =
        reference?.stop?.name ||
        reference?.stop?.stopPlaceName ||
        vehicle.stopId ||
        '-';

    const speedDisplay =
        vehicle.speed != null
            ? `${Math.round(vehicle.speed * 3.6)} ${t('unitKmh')}`
            : '-';

    const bearingDisplay =
        vehicle.bearing != null
            ? `${Math.round(vehicle.bearing)}${t('unitDegree')}`
            : '-';

    const getStopLabel = () => {
        switch (vehicle.currentStatus) {
            case VehicleStopStatus.IncomingAt:
                return t('approachingStop');
            case VehicleStopStatus.StoppedAt:
                return t('currentStop');
            case VehicleStopStatus.InTransitTo:
                return t('nextStop');
            default:
                return t('stop');
        }
    };

    const getStatusText = () => {
        switch (vehicle.currentStatus) {
            case VehicleStopStatus.IncomingAt:
                return t('statusIncoming');
            case VehicleStopStatus.StoppedAt:
                return t('statusStopped');
            case VehicleStopStatus.InTransitTo:
                return t('statusInTransit');
            default:
                return t('statusUnknown');
        }
    };

    const getOccupancyText = () => {
        switch (vehicle.occupancyStatus) {
            case OccupancyStatus.Empty:
                return t('occEmpty');
            case OccupancyStatus.ManySeatsAvailable:
                return t('occManySeats');
            case OccupancyStatus.FewSeatsAvailable:
                return t('occFewSeats');
            case OccupancyStatus.StandingRoomOnly:
                return t('occStanding');
            case OccupancyStatus.CrushedStandingRoomOnly:
                return t('occCrushedStanding');
            case OccupancyStatus.Full:
                return t('occFull');
            case OccupancyStatus.NotAcceptingPassengers:
                return t('occNotAccepting');
            case OccupancyStatus.NotBoardable:
                return t('occNotBoardable');
            case OccupancyStatus.NoDataAvailable:
            default:
                return t('occNoData');
        }
    };

    const relativeTime =
        diffSec < 60
            ? t('secondsAgo', { sec: diffSec })
            : t('minutesAgo', { min: diffMin });

    return (
        <>
            <div className="mt-3 flex min-w-0 items-center justify-between gap-2 rounded-xl border border-zinc-800 bg-zinc-800/50 p-3">
                <div className="min-w-0">
                    <span className="block text-[10px] font-bold uppercase tracking-wider text-zinc-400">
                        {t('vehicleId')}
                    </span>

                    <span className="block font-mono text-base font-bold text-zinc-100 break-words [overflow-wrap:anywhere]">
                        {vehicle.vehicleId}
                    </span>
                </div>

                {vehicle.vehicleLabel && (
                    <span className="max-w-[45%] shrink-0 rounded-md bg-blue-950 px-2.5 py-1 text-xs font-semibold text-blue-300 break-words">
                        {vehicle.vehicleLabel}
                    </span>
                )}
            </div>

            <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
                <div className="col-span-2 rounded-lg border border-zinc-800 bg-zinc-800/30 p-2.5">
                    <span className="block text-[11px] text-zinc-400">
                        {t('route')}
                    </span>

                    <span
                        className="mt-0.5 block font-semibold text-zinc-200 break-words [overflow-wrap:anywhere]"
                        title={vehicle.routeId || undefined}
                    >
                        {routeDisplay}
                    </span>
                </div>

                <div className="col-span-2 rounded-lg border border-zinc-800 bg-zinc-800/30 p-2.5">
                    <span className="block text-[11px] text-zinc-400">
                        {t('trip')}
                    </span>

                    <span
                        className="mt-0.5 block font-semibold text-zinc-200 break-words [overflow-wrap:anywhere]"
                        title={vehicle.tripId || undefined}
                    >
                        {tripDisplay}
                    </span>
                </div>

                <div className="min-w-0 rounded-lg border border-zinc-800 bg-zinc-800/30 p-2.5">
                    <span className="block text-[11px] text-zinc-400">
                        {t('speed')}
                    </span>
                    <span className="mt-0.5 block font-semibold text-zinc-200">
                        {speedDisplay}
                    </span>
                </div>

                <div className="min-w-0 rounded-lg border border-zinc-800 bg-zinc-800/30 p-2.5">
                    <span className="block text-[11px] text-zinc-400">
                        {t('bearing')}
                    </span>
                    <span className="mt-0.5 block font-semibold text-zinc-200">
                        {bearingDisplay}
                    </span>
                </div>
            </div>

            <div className="mt-3 space-y-2 text-xs">
                {reference?.line?.name && (
                    <div className="rounded-lg border border-blue-900/40 bg-blue-950/30 p-2.5">
                        <span className="block text-[11px] text-blue-300">
                            {t('lineName')}
                        </span>
                        <span className="mt-1 block font-semibold text-zinc-100 break-words">
                            {reference.line.name}
                        </span>
                    </div>
                )}

                {reference?.line?.operatorName && (
                    <DetailRow
                        label={t('operator')}
                        value={reference.line.operatorName}
                    />
                )}

                {(vehicle.stopId || reference?.stop) && (
                    <DetailRow
                        label={getStopLabel()}
                        value={stopDisplay}
                        title={vehicle.stopId || undefined}
                    />
                )}

                {isReferencePending &&
                    Boolean(vehicle.routeId || vehicle.stopId || vehicle.tripId) && (
                        <span className="block px-1 text-[11px] text-zinc-500">
                            {t('referenceLoading')}
                        </span>
                    )}
            </div>

            <div className="mt-3 space-y-2 text-xs">
                <DetailRow
                    label={t('status')}
                    value={getStatusText()}
                />

                <DetailRow
                    label={t('occupancy')}
                    value={getOccupancyText()}
                />

                <DetailRow
                    label={t('lastUpdate')}
                    value={relativeTime}
                />
            </div>
        </>
    );
}
