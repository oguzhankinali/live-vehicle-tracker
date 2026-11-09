// Araç detay kartında "Sinyal Yok / Bayat Veri" uyarısının çıkacağı süre (10 dakika)
export const VEHICLE_STALE_WARN_MS = 10 * 60 * 1000;

// Bellekten ve haritadan eski araçların kalıcı silineceği (eviction) süre (30 dakika)
export const VEHICLE_EVICTION_MS = 30 * 60 * 1000;

// Bellek temizleyicisinin periyodik çalışma aralığı (60 saniye)
export const VEHICLE_EVICTION_INTERVAL_MS = 60 * 1000;