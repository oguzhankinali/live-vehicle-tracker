import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { ENV } from '@/shared/config/env';
import { vehiclePositionRecordSchema, VehiclePositionRecord, PagedResult } from '../types/vehicle';

const pagedResultSchema = z.object({
  items: z.array(vehiclePositionRecordSchema),
  totalCount: z.number(),
  page: z.number(),
  pageSize: z.number(),
  totalPages: z.number(),
  hasNextPage: z.boolean(),
  hasPreviousPage: z.boolean(),
});

interface FetchHistoryParams {
  vehicleId: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export async function fetchVehicleHistory(params: FetchHistoryParams): Promise<PagedResult<VehiclePositionRecord>> {
  const searchParams = new URLSearchParams();
  if (params.from) searchParams.set('from', params.from);
  if (params.to) searchParams.set('to', params.to);
  searchParams.set('page', String(params.page || 1));
  searchParams.set('pageSize', String(params.pageSize || 50));

  const url = `${ENV.API_BASE_URL}/api/vehicles/${encodeURIComponent(params.vehicleId)}/history?${searchParams.toString()}`;
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`FETCH_HISTORY_FAILED_${res.status}`);
  }
  const rawJson = await res.json();
  return pagedResultSchema.parse(rawJson);
}

export function useVehicleHistory(params: FetchHistoryParams, enabled = true) {
  return useQuery({
    queryKey: ['vehicleHistory', params.vehicleId, params.from, params.to, params.page, params.pageSize],
    queryFn: () => fetchVehicleHistory(params),
    enabled: enabled && !!params.vehicleId,
    staleTime: 60 * 1000,
    placeholderData: (prev) => prev,
  });
}

export async function fetchAllVehicleTrailPoints(params: {
  vehicleId: string;
  from?: string;
  to?: string;
}): Promise<[number, number][]> {
  const pageSize = 100;
  const firstPage = await fetchVehicleHistory({
    vehicleId: params.vehicleId,
    from: params.from,
    to: params.to,
    page: 1,
    pageSize,
  });

  const allItems: VehiclePositionRecord[] = [...firstPage.items];

  if (firstPage.totalPages > 1) {
    const pagePromises: Promise<PagedResult<VehiclePositionRecord>>[] = [];
    const maxPagesToFetch = Math.min(firstPage.totalPages, 20);
    for (let p = 2; p <= maxPagesToFetch; p++) {
      pagePromises.push(
        fetchVehicleHistory({
          vehicleId: params.vehicleId,
          from: params.from,
          to: params.to,
          page: p,
          pageSize,
        })
      );
    }
    const remainingPages = await Promise.all(pagePromises);
    for (const pageData of remainingPages) {
      allItems.push(...pageData.items);
    }
  }

  // Kronolojik sıralama: Eskiden yeniye (Başlangıç -> Son Konum)
  allItems.sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());

  return allItems.map((item) => [item.longitude, item.latitude] as [number, number]);
}

export function useVehicleTrail(
  params: { vehicleId: string; from?: string; to?: string },
  enabled = true
) {
  return useQuery({
    queryKey: ['vehicleTrail', params.vehicleId, params.from, params.to],
    queryFn: () => fetchAllVehicleTrailPoints(params),
    enabled: enabled && !!params.vehicleId,
    staleTime: 60 * 1000,
  });
}