'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { SubscriptionStatusBadge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/empty-state';
import { useToast } from '@/components/ui/toast';

export default function MemberSubscriptionPage() {
  const router = useRouter();
  const [plans, setPlans] = useState<any[]>([]);
  const [subs, setSubs] = useState<any[]>([]);
  const [authorized, setAuthorized] = useState(false);
  const { push } = useToast();

  useEffect(() => {
    const token = localStorage.getItem('accessToken');
    const role = localStorage.getItem('role');
    if (!token) {
      push('Effettua login membro', 'warning');
      router.replace('/app');
      return;
    }
    if (role !== 'MEMBER') {
      push('Questa pagina e solo per membri', 'warning');
      router.replace('/dashboard');
      return;
    }
    setAuthorized(true);
  }, [push, router]);

  useEffect(() => {
    if (!authorized) return;
    Promise.all([apiFetch('/billing/plans'), apiFetch('/billing/me/subscriptions')])
      .then(([p, s]) => { setPlans(p); setSubs(s); })
      .catch((e) => push(String(e), 'danger'));
  }, [authorized, push]);

  const latest = useMemo(() => subs.slice().sort((a, b) => new Date(b.startedAtUtc).getTime() - new Date(a.startedAtUtc).getTime())[0], [subs]);

  async function checkout(planId: string) {
    const memberUserId = localStorage.getItem('userId');
    if (!memberUserId) return push('Fai login membro', 'warning');

    const origin = window.location.origin;
    try {
      const data = await apiFetch('/billing/checkout', {
        method: 'POST',
        body: JSON.stringify({ memberUserId, planId, successUrl: `${origin}/app/subscription?status=ok`, cancelUrl: `${origin}/app/subscription?status=cancel` })
      });
      if (data.url) window.location.href = data.url;
    } catch (e) {
      push(String(e), 'danger');
    }
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Subscription</CardTitle>
        <div className='mt-2 flex items-center gap-2'>
          <span className='text-sm text-[var(--text-muted)]'>Stato corrente:</span>
          {latest ? <SubscriptionStatusBadge status={latest.status} /> : <SubscriptionStatusBadge status='INCOMPLETE' />}
        </div>
        {latest?.endsAtUtc && <p className='mt-1 text-sm text-[var(--text-muted)]'>Scadenza: {new Date(latest.endsAtUtc).toLocaleDateString()}</p>}
      </Card>

      {plans.length === 0 ? <EmptyState title='Nessun piano disponibile' description='Contatta la reception per la configurazione piani.' /> : (
        <div className='grid gap-3 md:grid-cols-2'>
          {plans.map((p) => (
            <Card key={p.id}>
              <p className='text-base font-semibold'>{p.name}</p>
              <p className='text-sm text-[var(--text-muted)]'>€ {p.price} / {p.interval}</p>
              <Button className='mt-3' onClick={() => checkout(p.id)}>Checkout Stripe</Button>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
