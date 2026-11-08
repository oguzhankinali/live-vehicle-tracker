'use client';

import { useEffect, useState } from 'react';
import { useTranslations, useFormatter } from 'next-intl';
import { Activity, AlertTriangle, CheckCircle2, Clock, Database, RefreshCw, Users } from 'lucide-react';
import { useSystemStatus } from '../api/useSystemStatus';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

interface SystemStatusModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function SystemStatusModal({ isOpen, onClose }: SystemStatusModalProps) {
  const t = useTranslations('SystemStatus');
  const format = useFormatter();
  const { data, isLoading, isError, refetch } = useSystemStatus(isOpen);

  const [secondsAgo, setSecondsAgo] = useState<number | null>(null);

  useEffect(() => {
    if (!data?.lastSuccessfulPollUtc) {
      setSecondsAgo(null);
      return;
    }
    const updateDiff = () => {
      const pollTime = new Date(data.lastSuccessfulPollUtc!).getTime();
      const diffSec = Math.max(0, Math.floor((Date.now() - pollTime) / 1000));
      setSecondsAgo(diffSec);
    };
    updateDiff();
    const timer = setInterval(updateDiff, 1000);
    return () => clearInterval(timer);
  }, [data?.lastSuccessfulPollUtc]);

  // Sağlık kriteri: Son okuma 15 saniyeden eskiyse gecikmeli sayılır
  const isDelayed = secondsAgo !== null && secondsAgo > 15;

  const formatUptime = (totalSeconds: number) => {
    const h = Math.floor(totalSeconds / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = Math.floor(totalSeconds % 60);
    return t('uptimeFormat', { h, m, s });
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open: boolean) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg bg-zinc-900 border-zinc-800 text-zinc-100 p-6">
        <DialogHeader className="border-b border-zinc-800 pb-3">
          <div className="flex items-center gap-2.5">
            <Activity className="h-5 w-5 text-blue-400" />
            <DialogTitle className="text-base font-semibold tracking-tight text-zinc-100">
              {t('title')}
            </DialogTitle>
          </div>
        </DialogHeader>

        {/* 1. DURUM: YÜKLENİYOR (Loading Skeleton) */}
        {isLoading && (
          <div className="grid grid-cols-2 gap-3 py-6 animate-pulse" aria-label={t('loading')}>
            {[...Array(6)].map((_, i) => (
              <div key={i} className="h-20 bg-zinc-800/60 rounded-xl" />
            ))}
          </div>
        )}

        {/* 2. DURUM: HATA (Error State) */}
        {isError && (
          <div className="py-8 flex flex-col items-center justify-center gap-3 text-center">
            <AlertTriangle className="h-10 w-10 text-rose-500" />
            <p className="text-sm text-rose-400 font-medium">{t('error')}</p>
            <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-2">
              <RefreshCw className="h-3.5 w-3.5" />
              {t('retry')}
            </Button>
          </div>
        )}

        {/* 3. DURUM: BOŞ VERİ (Empty State) */}
        {!isLoading && !isError && data && !data.lastSuccessfulPollUtc && (
          <div className="py-8 text-center text-sm text-zinc-400">
            {t('empty')}
          </div>
        )}

        {/* 4. DURUM: BAŞARILI (Success State) */}
        {!isLoading && !isError && data && data.lastSuccessfulPollUtc && (
          <div className="flex flex-col gap-4 mt-2">
            {/* Sağlık Durumu Rozeti */}
            <div className="flex items-center justify-between p-3 rounded-xl bg-zinc-800/40 border border-zinc-800">
              <span className="text-xs text-zinc-400 font-medium">{t('healthStatus')}</span>
              {!isDelayed ? (
                <Badge variant="success" className="gap-1.5 py-1">
                  <CheckCircle2 className="h-3.5 w-3.5 text-emerald-400" />
                  {t('healthy')}
                </Badge>
              ) : (
                <Badge variant="warning" className="gap-1.5 py-1">
                  <AlertTriangle className="h-3.5 w-3.5 text-amber-400" />
                  {t('delayed')}
                </Badge>
              )}
            </div>

            {/* Metrik Kartları Grid */}
            <div className="grid grid-cols-2 gap-3 text-xs">
              <div className="p-3 bg-zinc-800/30 rounded-xl border border-zinc-800/80">
                <span className="text-zinc-400 block text-[11px] mb-1">{t('lastPoll')}</span>
                <span className="text-sm font-semibold font-mono text-zinc-100">
                  {secondsAgo === null || secondsAgo === 0
                    ? t('justNow')
                    : t('secondsAgo', { sec: secondsAgo })}
                </span>
              </div>

              <div className="p-3 bg-zinc-800/30 rounded-xl border border-zinc-800/80">
                <span className="text-zinc-400 block text-[11px] mb-1">{t('recordsLastHour')}</span>
                <div className="flex items-center gap-1.5">
                  <Database className="h-3.5 w-3.5 text-blue-400" />
                  <span className="text-sm font-semibold font-mono text-zinc-100">
                    {format.number(data.recordsWrittenLastHour)}
                  </span>
                </div>
              </div>

              <div className="p-3 bg-zinc-800/30 rounded-xl border border-zinc-800/80">
                <span className="text-zinc-400 block text-[11px] mb-1">{t('totalRecords')}</span>
                <span className="text-sm font-semibold font-mono text-zinc-200">
                  {format.number(data.totalRecordsWritten)}
                </span>
              </div>

              <div className="p-3 bg-zinc-800/30 rounded-xl border border-zinc-800/80">
                <span className="text-zinc-400 block text-[11px] mb-1">{t('failedAttempts')}</span>
                <span
                  className={`text-sm font-semibold font-mono ${
                    data.failedPollAttempts > 0 ? 'text-rose-400' : 'text-emerald-400'
                  }`}
                >
                  {format.number(data.failedPollAttempts)}
                </span>
              </div>

              <div className="p-3 bg-zinc-800/30 rounded-xl border border-zinc-800/80">
                <span className="text-zinc-400 block text-[11px] mb-1">{t('connectedClients')}</span>
                <div className="flex items-center gap-1.5">
                  <Users className="h-3.5 w-3.5 text-indigo-400" />
                  <span className="text-sm font-semibold font-mono text-zinc-100">
                    {format.number(data.connectedClientCount)}
                  </span>
                </div>
              </div>

              <div className="p-3 bg-zinc-800/30 rounded-xl border border-zinc-800/80">
                <span className="text-zinc-400 block text-[11px] mb-1">{t('uptime')}</span>
                <div className="flex items-center gap-1.5">
                  <Clock className="h-3.5 w-3.5 text-amber-400" />
                  <span className="text-xs font-semibold font-mono text-zinc-200">
                    {formatUptime(data.uptimeSeconds)}
                  </span>
                </div>
              </div>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}