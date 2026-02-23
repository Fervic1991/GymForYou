'use client';

import { Button } from '@/components/ui/button';
import { useI18n } from '@/lib/i18n/provider';

export function EmptyState({
  title,
  description,
  ctaLabel,
  onCtaClick
}: {
  title: string;
  description: string;
  ctaLabel?: string;
  onCtaClick?: () => void;
}) {
  const { t } = useI18n();
  return (
    <div className='rounded-[var(--radius-lg)] border border-dashed border-[var(--border)] bg-[var(--surface)] p-8 text-center'>
      <p className='text-2xl'>🏋️</p>
      <p className='mt-2 text-base font-semibold text-[var(--text)]'>{title || t('empty.none')}</p>
      <p className='mt-1 text-sm text-[var(--muted)]'>{description || t('empty.createFirst')}</p>
      {ctaLabel && onCtaClick && (
        <Button className='mt-4' onClick={onCtaClick}>
          {ctaLabel}
        </Button>
      )}
    </div>
  );
}
