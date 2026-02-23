'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { API_URL, apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { SubscriptionStatusBadge, Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/empty-state';
import { useToast } from '@/components/ui/toast';
import { Tabs } from '@/components/ui/tabs';
import { Table, TD, TH, THead, TR } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { useI18n } from '@/lib/i18n/provider';
import { HelpHint } from '@/components/ui/help-hint';

type Tab = 'PLANS' | 'SUBSCRIPTIONS' | 'PAYMENTS';

export default function BillingPage() {
  const { locale, t } = useI18n();
  const [tab, setTab] = useState<Tab>('PAYMENTS');
  const [loading, setLoading] = useState(true);
  const [plans, setPlans] = useState<any[]>([]);
  const [payments, setPayments] = useState<any[]>([]);
  const [subs, setSubs] = useState<any[]>([]);
  const [members, setMembers] = useState<any[]>([]);

  const [memberUserId, setMemberUserId] = useState('');
  const [amount, setAmount] = useState('49');
  const [method, setMethod] = useState('CASH');
  const [renewDays, setRenewDays] = useState('30');
  const [planId, setPlanId] = useState('');
  const [paymentFilter, setPaymentFilter] = useState<'ALL' | 'CASH' | 'BANK_TRANSFER' | 'POS' | 'STRIPE'>('ALL');

  const [manualSubMember, setManualSubMember] = useState('');
  const [manualSubPlan, setManualSubPlan] = useState('');
  const [manualSubDays, setManualSubDays] = useState('30');
  const [manualSubAmount, setManualSubAmount] = useState('49');
  const [planName, setPlanName] = useState('');
  const [planDescription, setPlanDescription] = useState('');
  const [planPrice, setPlanPrice] = useState('49');
  const [planInterval, setPlanInterval] = useState('monthly');
  const [planStripePriceId, setPlanStripePriceId] = useState('');

  const token = useMemo(() => (typeof window !== 'undefined' ? localStorage.getItem('accessToken') : ''), []);
  const { push } = useToast();

  const intervalOptions = useMemo(
    () => [
      { value: 'weekly', label: locale === 'es' ? 'Semanal' : 'Settimanale' },
      { value: 'monthly', label: locale === 'es' ? 'Mensual' : 'Mensile' },
      { value: 'quarterly', label: locale === 'es' ? 'Trimestral' : 'Trimestrale' },
      { value: 'yearly', label: locale === 'es' ? 'Anual' : 'Annuale' }
    ],
    [locale]
  );

  const intervalLabel = (value: string) =>
    intervalOptions.find((o) => o.value === value)?.label ?? value;

  async function exportCsv() {
    if (!token) return push('Login richiesto', 'warning');
    const res = await fetch(`${API_URL}/billing/payments/export`, {
      headers: { Authorization: `Bearer ${token}` }
    });
    if (!res.ok) return push(await res.text(), 'danger');
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `payments-${Date.now()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    push('CSV esportato', 'success');
  }

  async function load() {
    setLoading(true);
    const [p, pay, s, m] = await Promise.all([apiFetch('/billing/plans'), apiFetch('/billing/payments'), apiFetch('/billing/subscriptions'), apiFetch('/members')]);
    setPlans(p);
    setPayments(pay);
    setSubs(s);
    setMembers(m);
    setLoading(false);
  }

  useEffect(() => {
    load().catch((e) => push(String(e), 'danger'));
  }, [push]);

  async function submitManualPayment(e: FormEvent) {
    e.preventDefault();
    await apiFetch('/billing/payments/manual', {
      method: 'POST',
      body: JSON.stringify({
        memberUserId,
        amount: Number(amount),
        currency: 'eur',
        method,
        notes: 'manual payment',
        renewDays: Number(renewDays),
        planId: planId || null
      })
    });
    push('Pagamento manuale registrato', 'success');
    await load();
  }

  async function submitManualSubscription(e: FormEvent) {
    e.preventDefault();
    await apiFetch('/billing/subscriptions/manual', {
      method: 'POST',
      body: JSON.stringify({
        memberUserId: manualSubMember,
        planId: manualSubPlan,
        durationDays: Number(manualSubDays),
        paymentMethod: method,
        amount: Number(manualSubAmount),
        notes: 'manual subscription'
      })
    });
    push('Subscription manuale salvata', 'success');
    await load();
  }

  async function submitCreatePlan(e: FormEvent) {
    e.preventDefault();
    await apiFetch('/billing/plans', {
      method: 'POST',
      body: JSON.stringify({
        name: planName,
        description: planDescription,
        price: Number(planPrice),
        interval: planInterval.trim().toLowerCase(),
        stripePriceId: planStripePriceId || null
      })
    });
    push('Piano creato', 'success');
    setPlanName('');
    setPlanDescription('');
    setPlanPrice('49');
    setPlanInterval('monthly');
    setPlanStripePriceId('');
    await load();
    setTab('PLANS');
  }

  const filteredPayments = useMemo(
    () => payments.filter((p) => paymentFilter === 'ALL' || p.method === paymentFilter),
    [payments, paymentFilter]
  );

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Billing</CardTitle>
        <p className='text-sm text-[var(--muted)]'>Gestisci piani, rinnovi e pagamenti con export immediato.</p>
      </Card>

      <Card>
        <Tabs
          value={tab}
          onChange={(value) => setTab(value as Tab)}
          items={[
            { value: 'PLANS', label: 'Plans', count: plans.length },
            { value: 'SUBSCRIPTIONS', label: 'Subscriptions', count: subs.length },
            { value: 'PAYMENTS', label: 'Payments', count: payments.length }
          ]}
        />
      </Card>

      <div className='grid gap-4 lg:grid-cols-2'>
        <Card>
          <CardTitle>Crea nuovo piano</CardTitle>
          <form onSubmit={submitCreatePlan} className='mt-3 space-y-2'>
            <Input value={planName} onChange={(e) => setPlanName(e.target.value)} placeholder='Nome piano' required />
            <Input value={planDescription} onChange={(e) => setPlanDescription(e.target.value)} placeholder='Descrizione' />
            <div className='grid grid-cols-2 gap-2'>
              <Input value={planPrice} onChange={(e) => setPlanPrice(e.target.value)} placeholder='Prezzo' required />
              <Select value={planInterval} onChange={(e) => setPlanInterval(e.target.value)} required>
                {intervalOptions.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </Select>
            </div>
            <Input value={planStripePriceId} onChange={(e) => setPlanStripePriceId(e.target.value)} placeholder='Stripe Price Id (opzionale)' />
            <div className='flex items-center gap-2'>
              <Button block variant='success'>Crea piano</Button>
              <HelpHint text={t('help.billing.createPlan')} />
            </div>
          </form>
        </Card>

        <Card>
          <CardTitle>Registra pagamento manuale</CardTitle>
          <form onSubmit={submitManualPayment} className='mt-3 space-y-2'>
            <Select value={memberUserId} onChange={(e) => setMemberUserId(e.target.value)} required>
              <option value=''>Membro</option>
              {members.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.fullName}
                </option>
              ))}
            </Select>
            <Select value={planId} onChange={(e) => setPlanId(e.target.value)}>
              <option value=''>Piano (opzionale)</option>
              {plans.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
            <div className='grid grid-cols-2 gap-2'>
              <Input value={amount} onChange={(e) => setAmount(e.target.value)} placeholder='Importo' />
              <Input value={renewDays} onChange={(e) => setRenewDays(e.target.value)} placeholder='Rinnovo giorni' />
            </div>
            <Select value={method} onChange={(e) => setMethod(e.target.value)}>
              <option>CASH</option>
              <option>BANK_TRANSFER</option>
              <option>POS</option>
              <option>STRIPE</option>
            </Select>
            <div className='flex items-center gap-2'>
              <Button block>Registra pagamento manuale</Button>
              <HelpHint text={t('help.billing.manualPayment')} />
            </div>
          </form>
        </Card>

        <Card>
          <CardTitle>Crea/Rinnova subscription manuale</CardTitle>
          <form onSubmit={submitManualSubscription} className='mt-3 space-y-2'>
            <Select value={manualSubMember} onChange={(e) => setManualSubMember(e.target.value)} required>
              <option value=''>Membro</option>
              {members.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.fullName}
                </option>
              ))}
            </Select>
            <Select value={manualSubPlan} onChange={(e) => setManualSubPlan(e.target.value)} required>
              <option value=''>Piano</option>
              {plans.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
            <div className='grid grid-cols-2 gap-2'>
              <Input value={manualSubDays} onChange={(e) => setManualSubDays(e.target.value)} placeholder='Durata giorni' />
              <Input value={manualSubAmount} onChange={(e) => setManualSubAmount(e.target.value)} placeholder='Importo' />
            </div>
            <div className='flex items-center gap-2'>
              <Button block variant='secondary'>
                Salva subscription
              </Button>
              <HelpHint text={t('help.billing.manualSubscription')} />
            </div>
          </form>
        </Card>
      </div>

      {tab === 'PLANS' && (
        <Card>
          <CardTitle>Piani attivi</CardTitle>
          {plans.length === 0 ? (
            <EmptyState title='Nessun piano disponibile' description='Aggiungi il primo piano per iniziare i rinnovi.' />
          ) : (
            <div className='mt-3 grid gap-2 md:grid-cols-2 xl:grid-cols-3'>
              {plans.map((p) => (
                <div key={p.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                  <p className='font-semibold'>{p.name}</p>
                  <p className='text-sm text-[var(--muted)]'>€ {p.price} / {intervalLabel(p.interval)}</p>
                </div>
              ))}
            </div>
          )}
        </Card>
      )}

      {tab === 'SUBSCRIPTIONS' && (
        <Card>
          <CardTitle>Subscriptions</CardTitle>
          {subs.length === 0 ? (
            <EmptyState title='Nessuna subscription' description='Crea una subscription manuale o via Stripe.' />
          ) : (
            <div className='mt-3 grid gap-2'>
              {subs.map((s) => (
                <div key={s.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                  <div className='flex items-center justify-between'>
                    <p className='text-sm font-semibold'>{members.find((m) => m.id === s.memberUserId)?.fullName || s.memberUserId}</p>
                    <SubscriptionStatusBadge status={s.status} />
                  </div>
                  <p className='text-xs text-[var(--muted)]'>Scadenza: {s.endsAtUtc ? new Date(s.endsAtUtc).toLocaleDateString() : 'N/D'}</p>
                </div>
              ))}
            </div>
          )}
        </Card>
      )}

      {tab === 'PAYMENTS' && (
        <Card>
          <div className='mb-3 flex flex-wrap items-center justify-between gap-2'>
            <CardTitle>Pagamenti</CardTitle>
            <div className='flex items-center gap-2'>
              <Select value={paymentFilter} onChange={(e) => setPaymentFilter(e.target.value as any)} className='min-w-40'>
                <option value='ALL'>Tutti i metodi</option>
                <option value='CASH'>Cash</option>
                <option value='BANK_TRANSFER'>Bank transfer</option>
                <option value='POS'>POS</option>
                <option value='STRIPE'>Stripe</option>
              </Select>
              <div className='flex items-center gap-2'>
                <Button variant='success' onClick={exportCsv}>
                  Export CSV
                </Button>
                <HelpHint text={t('help.billing.exportCsv')} />
              </div>
            </div>
          </div>

          {loading ? (
            <div className='space-y-2'>
              <Skeleton className='h-12 w-full' />
              <Skeleton className='h-12 w-full' />
            </div>
          ) : filteredPayments.length === 0 ? (
            <EmptyState title='Nessun pagamento' description='I pagamenti appariranno qui.' />
          ) : (
            <>
              <div className='space-y-2 lg:hidden'>
                {filteredPayments.map((p) => (
                  <div key={p.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                    <p className='font-semibold'>€ {Number(p.amount).toFixed(2)}</p>
                    <p className='text-xs text-[var(--muted)]'>
                      {new Date(p.createdAtUtc).toLocaleString()} · {members.find((m) => m.id === p.memberUserId)?.fullName || p.memberUserId}
                    </p>
                    <div className='mt-2 flex gap-2'>
                      <Badge tone='info'>{p.method}</Badge>
                      <Badge tone={p.status === 'paid' ? 'success' : 'warning'}>{p.status}</Badge>
                    </div>
                  </div>
                ))}
              </div>

              <div className='hidden lg:block'>
                <Table>
                  <THead>
                    <TR>
                      <TH>Data</TH>
                      <TH>Membro</TH>
                      <TH>Importo</TH>
                      <TH>Metodo</TH>
                      <TH>Stato</TH>
                    </TR>
                  </THead>
                  <tbody>
                    {filteredPayments.map((p) => (
                      <TR key={p.id}>
                        <TD>{new Date(p.createdAtUtc).toLocaleString()}</TD>
                        <TD>{members.find((m) => m.id === p.memberUserId)?.fullName || p.memberUserId}</TD>
                        <TD>€ {Number(p.amount).toFixed(2)}</TD>
                        <TD>{p.method}</TD>
                        <TD>
                          <Badge tone={p.status === 'paid' ? 'success' : 'warning'}>{p.status}</Badge>
                        </TD>
                      </TR>
                    ))}
                  </tbody>
                </Table>
              </div>
            </>
          )}
        </Card>
      )}
    </div>
  );
}
