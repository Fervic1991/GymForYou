'use client';

import { useEffect, useMemo, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { HelpHint } from '@/components/ui/help-hint';
import { useI18n } from '@/lib/i18n/provider';

type Kpis = {
  activeMembers: number;
  revenueMonth: number;
  weekCheckIns: number;
  fillRate: number;
  expiringMembers: number;
};

export default function DashboardPage() {
  const { t } = useI18n();
  const [data, setData] = useState<Kpis | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiFetch('/dashboard/kpis')
      .then(setData)
      .finally(() => setLoading(false));
  }, []);

  const revenueBars = useMemo(() => distribute(Number(data?.revenueMonth ?? 0), [0.2, 0.25, 0.3, 0.25]), [data]);
  const checkinBars = useMemo(() => distribute(Number(data?.weekCheckIns ?? 0), [0.12, 0.14, 0.2, 0.18, 0.16, 0.1, 0.1]), [data]);

  return (
    <div className='space-y-4'>
      <div className='grid gap-3 md:grid-cols-5'>
        {loading && Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className='h-28 w-full rounded-[var(--radius-lg)]' />)}
        {!loading && data && (
          <>
            <KpiCard icon='👥' title={t('dashboard.kpi.activeMembers')} value={data.activeMembers} trend='up' trendText={t} />
            <KpiCard icon='💶' title={t('dashboard.kpi.revenueMonth')} value={`€ ${Number(data.revenueMonth ?? 0).toFixed(2)}`} trend='up' trendText={t} />
            <KpiCard icon='✅' title={t('dashboard.kpi.weekCheckins')} value={data.weekCheckIns} trend='up' trendText={t} />
            <KpiCard icon='📈' title={t('dashboard.kpi.fillRate')} value={`${Math.round((data.fillRate || 0) * 100)}%`} trend='flat' trendText={t} />
            <KpiCard icon='⏳' title={t('dashboard.kpi.expiring7d')} value={Number(data.expiringMembers ?? 0)} trend={Number(data.expiringMembers ?? 0) > 0 ? 'down' : 'flat'} trendText={t} />
          </>
        )}
      </div>

      <div className='grid gap-4 lg:grid-cols-2'>
        <Card>
          <CardTitle>{t('dashboard.revenueMonthTitle')}</CardTitle>
          <p className='text-xs text-[var(--muted)]'>{t('dashboard.revenueMonthSubtitle')}</p>
          <div className='mt-4 grid grid-cols-4 gap-2'>
            {revenueBars.map((x, i) => <Bar key={i} label={`W${i + 1}`} value={x} max={Math.max(...revenueBars, 1)} tone='bg-[var(--accent)]' />)}
          </div>
        </Card>

        <Card>
          <CardTitle>{t('dashboard.weekPresenceTitle')}</CardTitle>
          <p className='text-xs text-[var(--muted)]'>{t('dashboard.weekPresenceSubtitle')}</p>
          <div className='mt-4 grid grid-cols-7 gap-2'>
            {checkinBars.map((x, i) => <Bar key={i} label={['L','M','M','G','V','S','D'][i]} value={x} max={Math.max(...checkinBars, 1)} tone='bg-[var(--primary)]' />)}
          </div>
        </Card>
      </div>

      <Card>
        <div className='flex items-center justify-between gap-2'>
          <CardTitle>{t('dashboard.quickActions')}</CardTitle>
          <Badge tone={Number(data?.expiringMembers ?? 0) > 0 ? 'warning' : 'success'}>
            {Number(data?.expiringMembers ?? 0)} {t('dashboard.expiringBadge')}
          </Badge>
        </div>
        <div className='mt-3 grid gap-2 md:grid-cols-4'>
          <div className='flex items-center gap-2'>
            <a href='/members'><Button>{t('dashboard.action.newMember')}</Button></a>
            <HelpHint text={t('help.dashboard.newMember')} />
          </div>
          <div className='flex items-center gap-2'>
            <a href='/classes'><Button variant='secondary'>{t('dashboard.action.newSession')}</Button></a>
            <HelpHint text={t('help.dashboard.newSession')} />
          </div>
          <div className='flex items-center gap-2'>
            <a href='/checkin'><Button variant='success'>{t('dashboard.action.quickCheckin')}</Button></a>
            <HelpHint text={t('help.dashboard.checkin')} />
          </div>
          <div className='flex items-center gap-2'>
            <a href='/billing'><Button variant='warning'>{t('dashboard.action.manualPayment')}</Button></a>
            <HelpHint text={t('help.dashboard.manualPayment')} />
          </div>
        </div>
      </Card>
    </div>
  );
}

function KpiCard({ icon, title, value, trend, trendText }: { icon: string; title: string; value: string | number; trend: 'up' | 'down' | 'flat'; trendText: (key: string) => string }) {
  return (
    <Card className='p-5'>
      <div className='flex items-start justify-between gap-2'>
        <p className='text-xl'>{icon}</p>
        <Badge tone={trend === 'up' ? 'success' : trend === 'down' ? 'danger' : 'info'}>
          {trend === 'up' ? trendText('dashboard.trendUp') : trend === 'down' ? trendText('dashboard.trendDown') : trendText('dashboard.trendFlat')}
        </Badge>
      </div>
      <p className='mt-2 text-sm text-[var(--muted)]'>{title}</p>
      <p className='mt-1 text-3xl font-bold text-[var(--text)]'>{value}</p>
    </Card>
  );
}

function Bar({ value, max, label, tone }: { value: number; max: number; label: string; tone: string }) {
  const pct = max <= 0 ? 0 : Math.max(6, Math.round((value / max) * 100));
  return (
    <div className='space-y-1'>
      <div className='h-28 w-full rounded bg-[var(--surface-3)] p-1'>
        <div className={`${tone} h-full w-full rounded`} style={{ clipPath: `inset(${100 - pct}% 0 0 0)` }} />
      </div>
      <p className='text-center text-xs text-[var(--muted)]'>{label}</p>
    </div>
  );
}

function distribute(total: number, weights: number[]) {
  if (!total) return weights.map(() => 0);
  return weights.map((w) => Math.round(total * w));
}
