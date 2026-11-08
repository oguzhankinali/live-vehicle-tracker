import { z } from "zod";

// Backend SystemStatusDto sözleşmesinin tip güvenli Zod şeması
export const systemStatusSchema = z.object({
  lastSuccessfulPollUtc: z.string().nullable(),
  recordsWrittenLastHour: z.number(),
  totalRecordsWritten: z.number(),
  failedPollAttempts: z.number(),
  connectedClientCount: z.number(),
  uptimeSeconds: z.number(),
});

export type SystemStatus = z.infer<typeof systemStatusSchema>;