'use client';

import { cn } from '@/lib/cn';
import { useI18n } from '@/lib/i18n/provider';
import { tStatusBooking, tSubscriptionStatus } from '@/lib/i18n/status';

export function Badge({ children, tone = 'neutral' }: { children: React.ReactNode; tone?: 'neutral' | 'success' | 'warning' | 'danger' | 'info'; }) {
  const toneClass = {
    neutral: 'border border-[var(--border)] bg-[var(--surface-3)] text-[var(--muted)]',
    success: 'bg-[color-mix(in_srgb,var(--primary)_22%,transparent)] text-[var(--primary)]',
    warning: 'bg-[color-mix(in_srgb,var(--warning)_22%,transparent)] text-[var(--warning)]',
    danger: 'bg-[color-mix(in_srgb,var(--danger)_22%,transparent)] text-[var(--danger)]',
    info: 'bg-[color-mix(in_srgb,var(--info)_22%,transparent)] text-[var(--info)]'
  }[tone];

  return <span className={cn('inline-flex items-center rounded-full px-2.5 py-1 text-xs font-semibold uppercase tracking-wide', toneClass)}>{children}</span>;
}

export function BookingStatusBadge({ status }: { status: string }) {
  const { locale } = useI18n();
  const map: Record<string, 'success'|'warning'|'danger'|'neutral'|'info'> = {
    BOOKED: 'success',
    WAITLISTED: 'warning',
    CANCELED: 'neutral',
    NO_SHOW: 'danger',
    LATE_CANCEL: 'warning'
  };
  return <Badge tone={map[status] ?? 'info'}>{tStatusBooking(status, locale)}</Badge>;
}

export function SubscriptionStatusBadge({ status }: { status: string }) {
  const { locale } = useI18n();
  const map: Record<string, 'success'|'warning'|'danger'|'neutral'|'info'> = {
    ACTIVE: 'success',
    PAST_DUE: 'warning',
    CANCELED: 'neutral',
    INCOMPLETE: 'info'
  };
  return <Badge tone={map[status] ?? 'info'}>{tSubscriptionStatus(status, locale)}</Badge>;
}
