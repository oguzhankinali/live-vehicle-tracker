'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Activity } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { SystemStatusModal } from './SystemStatusModal';

export function SystemStatusButton() {
  const t = useTranslations('SystemStatus');
  const [isOpen, setIsOpen] = useState(false);

  return (
    <>
      <Button
        variant="secondary"
        size="sm"
        onClick={() => setIsOpen(true)}
        className="gap-1.5 bg-zinc-800/80 hover:bg-zinc-700 text-zinc-200"
      >
        <Activity className="h-3.5 w-3.5 text-blue-400" />
        <span>{t('button')}</span>
      </Button>

      <SystemStatusModal isOpen={isOpen} onClose={() => setIsOpen(false)} />
    </>
  );
}