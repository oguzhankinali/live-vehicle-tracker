'use client';

import { useTranslations } from 'next-intl';
import { useVehicleStore } from '../store/useVehicleStore';

export function ConnectionBadge() {
  const t = useTranslations('Connection');
  const connectionStatus = useVehicleStore((state) => state.connectionStatus);

  const statusConfig = {
    Connected: {
      color: 'bg-emerald-500',
      textColor: 'text-emerald-700 dark:text-emerald-400',
      bgColor: 'bg-emerald-50 dark:bg-emerald-950/40',
      borderColor: 'border-emerald-200 dark:border-emerald-800',
      ping: true,
      text: t('Connected')
    },
    Connecting: {
      color: 'bg-amber-500',
      textColor: 'text-amber-700 dark:text-amber-400',
      bgColor: 'bg-amber-50 dark:bg-amber-950/40',
      borderColor: 'border-amber-200 dark:border-amber-800',
      ping: true,
      text: t('Connecting')
    },
    Reconnecting: {
      color: 'bg-amber-500',
      textColor: 'text-amber-700 dark:text-amber-400',
      bgColor: 'bg-amber-50 dark:bg-amber-950/40',
      borderColor: 'border-amber-200 dark:border-amber-800',
      ping: true,
      text: t('Reconnecting')
    },
    Disconnected: {
      color: 'bg-rose-500',
      textColor: 'text-rose-700 dark:text-rose-400',
      bgColor: 'bg-rose-50 dark:bg-rose-950/40',
      borderColor: 'border-rose-200 dark:border-rose-800',
      ping: false,
      text: t('Disconnected')
    }
  };

  const current = statusConfig[connectionStatus];

  return (
    <div
      className={`inline-flex items-center gap-2 px-3 py-1.5 rounded-full border text-xs font-medium transition-colors ${current.bgColor} ${current.borderColor} ${current.textColor}`}
    >
      <span className="relative flex h-2 w-2">
        {current.ping && (
          <span
            className={`animate-ping absolute inline-flex h-full w-full rounded-full opacity-75 ${current.color}`}
          />
        )}
        <span className={`relative inline-flex rounded-full h-2 w-2 ${current.color}`} />
      </span>
      <span>{current.text}</span>
    </div>
  );
}