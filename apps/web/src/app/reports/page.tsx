'use client';

import { useEffect, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { useToast } from '@/components/ui/toast';

export default function ReportsPage() {
  const [data, setData] = useState<any>(null);
  const { push } = useToast();

  useEffect(() => {
    apiFetch('/reports/summary').then(setData).catch((e) => push(String(e), 'danger'));
  }, [push]);

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Reports</CardTitle>
        <p className='text-sm text-[var(--text-muted)]'>Churn, frequenza media e top corsi.</p>
      </Card>

      {!data ? <EmptyState title='Nessun report disponibile' description='Caricamento dati report in corso.' /> : (
        <div className='grid gap-4 md:grid-cols-3'>
          <Card>
            <CardTitle>Churn</CardTitle>
            <p className='mt-2 text-sm'>Attivi correnti: <strong>{data.churn?.activeCurrent}</strong></p>
            <p className='text-sm'>Attivi mese precedente: <strong>{data.churn?.activePrevious}</strong></p>
            <p className='text-sm'>Delta: <strong>{data.churn?.delta}</strong></p>
          </Card>
          <Card>
            <CardTitle>Frequenza media 30d</CardTitle>
            <p className='mt-2 text-sm'>Check-in / membro: <strong>{Number(data.averageFrequency?.checkinsPerMember30d || 0).toFixed(2)}</strong></p>
            <p className='text-sm'>Booking / membro: <strong>{Number(data.averageFrequency?.bookingsPerMember30d || 0).toFixed(2)}</strong></p>
          </Card>
          <Card>
            <CardTitle>Top classi</CardTitle>
            <ul className='mt-2 space-y-1 text-sm'>
              {(data.topClasses || []).map((x: any) => <li key={x.title}>{x.title}: <strong>{x.bookings}</strong></li>)}
            </ul>
          </Card>
        </div>
      )}
    </div>
  );
}
