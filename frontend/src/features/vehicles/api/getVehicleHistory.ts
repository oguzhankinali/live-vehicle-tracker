import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { VehiclePositionRecord, PagedResult } from '../types/vehicle';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5086';

const vehiclePositionSchema = z.object({
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
  currentStatus: z.number().nullable().optional(),
  occupancyStatus: z.number().nullable().optional(),
  timestamp: z.string(),
  receivedAtUtc: z.string()
});

const pagedResultSchema = z.object({
  items: z.array(vehiclePositionSchema),
  totalCount: z.number(),
  page: z.number(),
  pageSize: z.number(),
  totalPages: z.number(),
  hasNextPage: z.boolean(),
  hasPreviousPage: z.boolean()
});

interface FetchHistoryParams {
  vehicleId: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

// REST API Çağrısı
export async function fetchVehicleHistory(params: FetchHistoryParams): Promise<PagedResult<VehiclePositionRecord>> {
  const searchParams = new URLSearchParams();
  if (params.from) searchParams.set('from', params.from);
  if (params.to) searchParams.set('to', params.to);
  searchParams.set('page', String(params.page || 1));
  searchParams.set('pageSize', String(params.pageSize || 50));

  const url = `${API_BASE_URL}/api/vehicles/${encodeURIComponent(params.vehicleId)}/history?${searchParams.toString()}`;
  const res = await fetch(url);

  if (!res.ok) {
    throw new Error(`Geçmiş verisi alınamadı (HTTP ${res.status})`);
  }

  const rawJson = await res.json();
  return pagedResultSchema.parse(rawJson) as PagedResult<VehiclePositionRecord>;
}

// TanStack Query Hook'u - Tablo Sayfalaması İçin
export function useVehicleHistory(params: FetchHistoryParams, enabled = true) {
  return useQuery({
    queryKey: ['vehicleHistory', params.vehicleId, params.from, params.to, params.page, params.pageSize],
    queryFn: () => fetchVehicleHistory(params),
    enabled: enabled && !!params.vehicleId,
    staleTime: 60 * 1000,
    placeholderData: (prev) => prev
  });
}

// Harita Rotası İçin: Seçilen Zaman Aralığındaki TÜM Noktaları Birleştirip Getiren Fonksiyon
export async function fetchAllVehicleTrailPoints(params: {
  vehicleId: string;
  from?: string;
  to?: string;
}): Promise<[number, number][]> {
  const pageSize = 100;
  // 1. İlk sayfayı çek
  const firstPage = await fetchVehicleHistory({
    vehicleId: params.vehicleId,
    from: params.from,
    to: params.to,
    page: 1,
    pageSize
  });

  const allItems: VehiclePositionRecord[] = [...firstPage.items];

  // 2. Eğer birden fazla sayfa varsa (örn. 7 sayfa), kalan sayfaları paralel çek
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
          pageSize
        })
      );
    }

    const remainingPages = await Promise.all(pagePromises);
    for (const pageData of remainingPages) {
      allItems.push(...pageData.items);
    }
  }

  // 3. Zamana göre kronolojik sırala (Eskiden yeniye: Başlangıç -> Bitiş)
  allItems.sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());

  // 4. [lon, lat] koordinat dizisine dönüştür
  return allItems.map((item) => [item.longitude, item.latitude] as [number, number]);
}

// Harita Rotası İçin TanStack Query Hook'u (Sayfa değişiminden etkilenmez, 30m/1h/3h ile güncellenir)
export function useVehicleTrail(
  params: { vehicleId: string; from?: string; to?: string },
  enabled = true
) {
  return useQuery({
    queryKey: ['vehicleTrail', params.vehicleId, params.from, params.to],
    queryFn: () => fetchAllVehicleTrailPoints(params),
    enabled: enabled && !!params.vehicleId,
    staleTime: 60 * 1000
  });
}
