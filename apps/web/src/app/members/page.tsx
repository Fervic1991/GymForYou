'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/empty-state';
import { useToast } from '@/components/ui/toast';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TD, TH, THead, TR } from '@/components/ui/table';
import { HelpHint } from '@/components/ui/help-hint';
import { useI18n } from '@/lib/i18n/provider';

type Member = {
  id: string;
  fullName: string;
  email: string;
  phone?: string;
  status: 'ACTIVE' | 'SUSPENDED';
  lastCheckInUtc?: string | null;
  bookingBlockedUntilUtc?: string | null;
};
type Plan = { id: string; name: string; price: number; interval: string };
type Subscription = { memberUserId: string; status: string; endsAtUtc?: string | null };

export default function MembersPage() {
  const { t } = useI18n();
  const [items, setItems] = useState<Member[]>([]);
  const [plans, setPlans] = useState<Plan[]>([]);
  const [subs, setSubs] = useState<Subscription[]>([]);
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState<'ALL' | 'ACTIVE' | 'SUSPENDED'>('ALL');
  const [blocked, setBlocked] = useState<'ALL' | 'BLOCKED' | 'UNBLOCKED'>('ALL');
  const [expires, setExpires] = useState<'ALL' | '7D' | '30D'>('ALL');

  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('Member123!');

  const [assignPlanByMember, setAssignPlanByMember] = useState<Record<string, string>>({});
  const [suspendTarget, setSuspendTarget] = useState<Member | null>(null);
  const { push } = useToast();

  const load = async () => {
    setLoading(true);
    try {
      const [m, p, s] = await Promise.all([apiFetch('/members'), apiFetch('/billing/plans'), apiFetch('/billing/subscriptions')]);
      setItems(m);
      setPlans(p);
      setSubs(s);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load().catch((e) => push(String(e), 'danger'));
  }, [push]);

  const expiryByMember = useMemo(() => {
    const acc: Record<string, string> = {};
    items.forEach((m) => {
      const own = subs
        .filter((s) => s.memberUserId === m.id && s.endsAtUtc)
        .sort((a, b) => new Date(b.endsAtUtc || 0).getTime() - new Date(a.endsAtUtc || 0).getTime())[0];
      acc[m.id] = own?.endsAtUtc || '';
    });
    return acc;
  }, [items, subs]);

  const filtered = useMemo(
    () =>
      items.filter((m) => {
        const byStatus = status === 'ALL' || m.status === status;
        const isBlocked = !!(m.bookingBlockedUntilUtc && new Date(m.bookingBlockedUntilUtc) > new Date());
        const byBlocked = blocked === 'ALL' || (blocked === 'BLOCKED' ? isBlocked : !isBlocked);
        const expiry = expiryByMember[m.id] ? new Date(expiryByMember[m.id]) : null;
        const now = new Date();
        const in7d = expiry ? expiry.getTime() <= now.getTime() + 7 * 86400000 : false;
        const in30d = expiry ? expiry.getTime() <= now.getTime() + 30 * 86400000 : false;
        const byExpiry = expires === 'ALL' || (expires === '7D' ? in7d : in30d);
        const term = query.trim().toLowerCase();
        const byQuery =
          !term ||
          m.fullName.toLowerCase().includes(term) ||
          m.email.toLowerCase().includes(term) ||
          (m.phone || '').toLowerCase().includes(term);
        return byStatus && byBlocked && byExpiry && byQuery;
      }),
    [items, query, status, blocked, expires, expiryByMember]
  );

  async function submit(e: FormEvent) {
    e.preventDefault();
    await apiFetch('/members', { method: 'POST', body: JSON.stringify({ fullName: name, email, phone, password }) });
    setName('');
    setEmail('');
    setPhone('');
    push('Membro creato', 'success');
    load();
  }

  async function quickCheckIn(memberUserId: string) {
    await apiFetch('/members/checkin', { method: 'POST', body: JSON.stringify({ memberUserId }) });
    push('Check-in registrato', 'success');
    load();
  }

  async function toggleStatus(member: Member) {
    const next = member.status === 'ACTIVE' ? 'SUSPENDED' : 'ACTIVE';
    await apiFetch(`/members/${member.id}/status`, { method: 'PATCH', body: JSON.stringify({ status: next }) });
    push(next === 'SUSPENDED' ? 'Membro sospeso' : 'Membro riattivato', next === 'SUSPENDED' ? 'warning' : 'success');
    setSuspendTarget(null);
    load();
  }

  async function quickAssignPlan(member: Member) {
    const planId = assignPlanByMember[member.id];
    if (!planId) return push('Seleziona un piano', 'warning');
    const plan = plans.find((p) => p.id === planId);
    if (!plan) return push('Piano non trovato', 'danger');
    await apiFetch('/billing/subscriptions/manual', {
      method: 'POST',
      body: JSON.stringify({
        memberUserId: member.id,
        planId: plan.id,
        durationDays: plan.interval.toLowerCase().includes('year') ? 365 : 30,
        paymentMethod: 'POS',
        amount: Number(plan.price || 0),
        notes: 'quick assign from members page'
      })
    });
    push('Piano assegnato', 'success');
    load();
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Membri</CardTitle>
        <p className='mt-1 text-sm text-[var(--muted)]'>Ricerca rapida, stato abbonamento e azioni reception in un solo posto.</p>
      </Card>

      <Card>
        <form onSubmit={submit} className='grid gap-2 md:grid-cols-5'>
          <Input placeholder='Nome (min 5)' value={name} onChange={(e) => setName(e.target.value)} minLength={5} required />
          <Input placeholder='Email' value={email} onChange={(e) => setEmail(e.target.value)} type='email' pattern='^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$' required />
          <Input placeholder='Telefono (solo numeri)' value={phone} onChange={(e) => setPhone(e.target.value)} pattern='^\d*$' inputMode='numeric' />
          <Input placeholder='Password' value={password} onChange={(e) => setPassword(e.target.value)} />
          <div className='flex items-center gap-2'>
            <Button block>+ Nuovo membro</Button>
            <HelpHint text={t('help.members.create')} />
          </div>
        </form>
      </Card>

      <Card>
        <div className='grid gap-2 md:grid-cols-5'>
          <Input placeholder='Cerca per nome/email/telefono' value={query} onChange={(e) => setQuery(e.target.value)} />
          <Select value={status} onChange={(e) => setStatus(e.target.value as any)}>
            <option value='ALL'>Stato: tutti</option>
            <option value='ACTIVE'>Attivi</option>
            <option value='SUSPENDED'>Sospesi</option>
          </Select>
          <Select value={blocked} onChange={(e) => setBlocked(e.target.value as any)}>
            <option value='ALL'>Prenotazioni: tutti</option>
            <option value='BLOCKED'>Bloccati</option>
            <option value='UNBLOCKED'>Sbloccati</option>
          </Select>
          <Select value={expires} onChange={(e) => setExpires(e.target.value as any)}>
            <option value='ALL'>Scadenza: tutte</option>
            <option value='7D'>Scade in 7 giorni</option>
            <option value='30D'>Scade in 30 giorni</option>
          </Select>
          <div className='flex items-center text-sm text-[var(--muted)]'>Risultati: {filtered.length}</div>
        </div>
      </Card>

      {loading && (
        <Card>
          <div className='space-y-2'>
            <Skeleton className='h-12 w-full' />
            <Skeleton className='h-12 w-full' />
            <Skeleton className='h-12 w-full' />
          </div>
        </Card>
      )}

      {!loading && filtered.length === 0 ? (
        <EmptyState
          title='Nessun membro trovato'
          description='Crea il primo membro o modifica i filtri.'
          ctaLabel='Crea il primo membro'
          onCtaClick={() => {
            const el = document.querySelector('input[placeholder="Nome"]') as HTMLInputElement | null;
            el?.focus();
          }}
        />
      ) : (
        !loading && (
          <Card>
            <div className='space-y-3 lg:hidden'>
              {filtered.map((m) => (
                <div key={m.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                  <p className='font-semibold'>{m.fullName}</p>
                  <p className='text-xs text-[var(--muted)]'>
                    {m.email}
                    {m.phone ? ` · ${m.phone}` : ''}
                  </p>
                  <div className='mt-2 flex flex-wrap items-center gap-2'>
                    <Badge tone={m.status === 'ACTIVE' ? 'success' : 'warning'}>{m.status}</Badge>
                    <span className='text-xs text-[var(--muted)]'>
                      Scadenza: {expiryByMember[m.id] ? new Date(expiryByMember[m.id]).toLocaleDateString() : 'N/D'}
                    </span>
                  </div>
                  <p className='mt-2 text-xs text-[var(--muted)]'>Ultimo check-in: {m.lastCheckInUtc ? new Date(m.lastCheckInUtc).toLocaleString() : 'Mai'}</p>
                  <div className='mt-3 grid gap-2'>
                    <div className='flex gap-2'>
                      <div className='flex items-center gap-2'>
                        <Button variant='secondary' block onClick={() => quickCheckIn(m.id)}>
                          Check-in
                        </Button>
                        <HelpHint text={t('help.members.checkin')} />
                      </div>
                      <div className='flex items-center gap-2'>
                        <Button variant={m.status === 'ACTIVE' ? 'warning' : 'success'} block onClick={() => setSuspendTarget(m)}>
                          {m.status === 'ACTIVE' ? 'Sospendi' : 'Riattiva'}
                        </Button>
                        <HelpHint text={t('help.members.toggleStatus')} />
                      </div>
                    </div>
                    <Select value={assignPlanByMember[m.id] || ''} onChange={(e) => setAssignPlanByMember((prev) => ({ ...prev, [m.id]: e.target.value }))}>
                      <option value=''>Seleziona piano</option>
                      {plans.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name} (€ {p.price})
                        </option>
                      ))}
                    </Select>
                    <div className='flex items-center gap-2'>
                      <Button block onClick={() => quickAssignPlan(m)}>
                        Assegna piano
                      </Button>
                      <HelpHint text={t('help.members.assignPlan')} />
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <div className='hidden lg:block'>
              <Table>
                <THead>
                  <TR>
                    <TH>Membro</TH>
                    <TH>Stato</TH>
                    <TH>Scadenza abbonamento</TH>
                    <TH>Ultimo check-in</TH>
                    <TH>Azioni rapide</TH>
                  </TR>
                </THead>
                <tbody>
                  {filtered.map((m) => (
                    <TR key={m.id}>
                      <TD>
                        <p className='font-semibold'>{m.fullName}</p>
                        <p className='text-xs text-[var(--muted)]'>
                          {m.email}
                          {m.phone ? ` · ${m.phone}` : ''}
                        </p>
                      </TD>
                      <TD>
                        <Badge tone={m.status === 'ACTIVE' ? 'success' : 'warning'}>{m.status}</Badge>
                        {m.bookingBlockedUntilUtc && <p className='mt-1 text-xs text-[var(--danger)]'>Bloccato fino al {new Date(m.bookingBlockedUntilUtc).toLocaleDateString()}</p>}
                      </TD>
                      <TD>{expiryByMember[m.id] ? new Date(expiryByMember[m.id]).toLocaleDateString() : <span className='text-[var(--muted)]'>N/D</span>}</TD>
                      <TD>{m.lastCheckInUtc ? new Date(m.lastCheckInUtc).toLocaleString() : <span className='text-[var(--muted)]'>Mai</span>}</TD>
                      <TD>
                        <div className='flex flex-wrap gap-2'>
                          <div className='flex items-center gap-2'>
                            <Button variant='secondary' onClick={() => quickCheckIn(m.id)}>
                              Check-in
                            </Button>
                            <HelpHint text={t('help.members.checkin')} />
                          </div>
                          <div className='flex items-center gap-2'>
                            <Button variant={m.status === 'ACTIVE' ? 'warning' : 'success'} onClick={() => setSuspendTarget(m)}>
                              {m.status === 'ACTIVE' ? 'Sospendi' : 'Riattiva'}
                            </Button>
                            <HelpHint text={t('help.members.toggleStatus')} />
                          </div>
                        </div>
                        <div className='mt-2 flex gap-2'>
                          <Select value={assignPlanByMember[m.id] || ''} onChange={(e) => setAssignPlanByMember((prev) => ({ ...prev, [m.id]: e.target.value }))}>
                            <option value=''>Seleziona piano</option>
                            {plans.map((p) => (
                              <option key={p.id} value={p.id}>
                                {p.name} (€ {p.price})
                              </option>
                            ))}
                          </Select>
                          <div className='flex items-center gap-2'>
                            <Button onClick={() => quickAssignPlan(m)}>Assegna piano</Button>
                            <HelpHint text={t('help.members.assignPlan')} />
                          </div>
                        </div>
                      </TD>
                    </TR>
                  ))}
                </tbody>
              </Table>
            </div>
          </Card>
        )
      )}

      <ConfirmDialog
        open={Boolean(suspendTarget)}
        title='Conferma azione'
        description={suspendTarget ? `Vuoi ${suspendTarget.status === 'ACTIVE' ? 'sospendere' : 'riattivare'} ${suspendTarget.fullName}?` : ''}
        confirmText='Conferma'
        onClose={() => setSuspendTarget(null)}
        onConfirm={() => suspendTarget && toggleStatus(suspendTarget)}
      />
    </div>
  );
}
