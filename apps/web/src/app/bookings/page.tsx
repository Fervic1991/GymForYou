'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { BookingStatusBadge } from '@/components/ui/badge';
import { Tabs } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/toast';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Progress } from '@/components/ui/progress';
import { EmptyState } from '@/components/ui/empty-state';
import { Skeleton } from '@/components/ui/skeleton';
import { useI18n } from '@/lib/i18n/provider';
import { HelpHint } from '@/components/ui/help-hint';

type PendingAction = { id: string; status: string } | null;

const statusTabs = ['BOOKED', 'WAITLISTED', 'CANCELED', 'NO_SHOW'] as const;
type StatusTab = (typeof statusTabs)[number];

export default function BookingsPage() {
  const [items, setItems] = useState<any[]>([]);
  const [sessions, setSessions] = useState<any[]>([]);
  const [classes, setClasses] = useState<any[]>([]);
  const [members, setMembers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  const [sessionId, setSessionId] = useState('');
  const [memberUserId, setMemberUserId] = useState('');
  const [tab, setTab] = useState<StatusTab>('BOOKED');
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  const { push } = useToast();
  const { locale, t } = useI18n();

  const text = locale === 'es'
    ? {
        subtitle: 'Monitor completo: confirmadas, lista de espera, cancelaciones y no-show.',
        session: 'Sesión',
        class: 'Clase',
        member: 'Miembro',
        book: 'Reservar',
        tabBooked: 'Confirmadas',
        tabWaitlist: 'Espera',
        tabCanceled: 'Canceladas',
        tabNoShow: 'No-show',
        action: 'Acción',
        lateCancel: 'Cancelación tardía',
        noShow: 'No-show',
        cancel: 'Cancelar',
        empty: 'No hay reservas en este estado',
        emptyDesc: 'Cambia pestaña o crea una nueva reserva.',
        capacityTitle: 'Capacidad y lista de espera por sesión',
        bookedCapacity: 'Confirmadas/Capacidad',
        waitlist: 'Lista de espera',
        noWaitlist: 'Sin lista de espera.',
        confirmTitle: 'Confirmar cambio de estado',
        confirmDesc: 'Esta acción puede activar la promoción automática de la lista de espera.'
      }
    : {
        subtitle: 'Monitor completo: confermate, lista d’attesa, cancellazioni e no-show.',
        session: 'Sessione',
        class: 'Classe',
        member: 'Membro',
        book: '+ Prenota',
        tabBooked: 'Prenotate',
        tabWaitlist: "Lista d'attesa",
        tabCanceled: 'Cancellate',
        tabNoShow: 'No-show',
        action: 'Azione',
        lateCancel: 'Cancellazione tardiva',
        noShow: 'No-show',
        cancel: 'Cancella',
        empty: 'Nessuna prenotazione in questo stato',
        emptyDesc: 'Cambia tab o crea una nuova prenotazione.',
        capacityTitle: "Capienza e lista d'attesa per sessione",
        bookedCapacity: 'Prenotati/Capienza',
        waitlist: "Lista d'attesa",
        noWaitlist: "Nessuna lista d'attesa.",
        confirmTitle: 'Conferma modifica stato',
        confirmDesc: 'Questa azione può attivare la promozione automatica della waitlist.'
      };

  const load = async () => {
    setLoading(true);
    const [b, s, c, m] = await Promise.all([apiFetch('/bookings'), apiFetch('/classes/sessions'), apiFetch('/classes'), apiFetch('/members')]);
    setItems(b);
    setSessions(s);
    setClasses(c);
    setMembers(m);
    setLoading(false);
  };

  useEffect(() => {
    load().catch((e) => push(String(e), 'danger'));
  }, [push]);

  const classById = useMemo(() => Object.fromEntries(classes.map((x) => [x.id, x])), [classes]);
  const memberById = useMemo(() => Object.fromEntries(members.map((x) => [x.id, x])), [members]);
  const sessionById = useMemo(() => Object.fromEntries(sessions.map((x) => [x.id, x])), [sessions]);

  const bySession = useMemo(() => {
    const acc: Record<string, any[]> = {};
    items.forEach((it) => {
      acc[it.sessionId] = acc[it.sessionId] || [];
      acc[it.sessionId].push(it);
    });
    return acc;
  }, [items]);

  const list = useMemo(
    () => items.filter((x) => x.status === tab).sort((a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()),
    [items, tab]
  );

  async function create(e: FormEvent) {
    e.preventDefault();
    const res = await apiFetch('/bookings', { method: 'POST', body: JSON.stringify({ sessionId, memberUserId }) });
    push(res.status === 'WAITLISTED' ? "Sessione piena, in lista d'attesa" : 'Posto confermato', res.status === 'WAITLISTED' ? 'warning' : 'success');
    load();
  }

  async function applyStatus() {
    if (!pendingAction) return;
    await apiFetch(`/bookings/${pendingAction.id}/status`, { method: 'PATCH', body: JSON.stringify({ status: pendingAction.status }) });
    push('Stato aggiornato', 'success');
    setPendingAction(null);
    load();
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Prenotazioni</CardTitle>
        <p className='text-sm text-[var(--muted)]'>{text.subtitle}</p>
      </Card>

      <Card>
        <form onSubmit={create} className='grid gap-2 md:grid-cols-3'>
          <Select value={sessionId} onChange={(e) => setSessionId(e.target.value)} required>
            <option value=''>{text.session}</option>
            {sessions.map((s) => {
              const className = classById[s.gymClassId]?.title || text.class;
              return (
                <option key={s.id} value={s.id}>
                  {className} - {new Date(s.startAtUtc).toLocaleString()}
                </option>
              );
            })}
          </Select>
          <Select value={memberUserId} onChange={(e) => setMemberUserId(e.target.value)} required>
            <option value=''>{text.member}</option>
            {members.map((m) => (
              <option key={m.id} value={m.id}>
                {m.fullName}
              </option>
            ))}
          </Select>
          <div className='flex items-center gap-2'>
            <Button>{text.book}</Button>
            <HelpHint text={t('help.bookings.create')} />
          </div>
        </form>
      </Card>

      <Card>
        <Tabs
          value={tab}
          onChange={(x) => setTab(x as StatusTab)}
          items={[
            { value: 'BOOKED', label: text.tabBooked, count: items.filter((x) => x.status === 'BOOKED').length },
            { value: 'WAITLISTED', label: text.tabWaitlist, count: items.filter((x) => x.status === 'WAITLISTED').length },
            { value: 'CANCELED', label: text.tabCanceled, count: items.filter((x) => x.status === 'CANCELED' || x.status === 'LATE_CANCEL').length },
            { value: 'NO_SHOW', label: text.tabNoShow, count: items.filter((x) => x.status === 'NO_SHOW').length }
          ]}
        />

        {loading && (
          <div className='mt-4 space-y-2'>
            <Skeleton className='h-16 w-full' />
            <Skeleton className='h-16 w-full' />
          </div>
        )}

        {!loading && list.length === 0 ? (
          <div className='mt-4'>
            <EmptyState title={text.empty} description={text.emptyDesc} />
          </div>
        ) : (
          <div className='mt-4 space-y-2'>
            {list.map((b) => {
              const s = sessionById[b.sessionId];
              const c = classById[s?.gymClassId];
              return (
                <div key={b.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                  <div className='flex flex-wrap items-center justify-between gap-2'>
                    <div>
                      <p className='font-semibold'>
                        {c?.title || text.class} - {new Date(s?.startAtUtc).toLocaleString()}
                      </p>
                      <p className='text-xs text-[var(--muted)]'>{text.member}: {memberById[b.memberUserId]?.fullName || b.memberUserId}</p>
                    </div>
                    <div className='flex items-center gap-2'>
                      <BookingStatusBadge status={b.status} />
                      {tab === 'BOOKED' && (
                        <div className='flex items-center gap-2'>
                          <Select className='min-w-36' onChange={(e) => e.target.value && setPendingAction({ id: b.id, status: e.target.value })} value=''>
                            <option value=''>{text.action}</option>
                            <option value='LATE_CANCEL'>{text.lateCancel}</option>
                            <option value='NO_SHOW'>{text.noShow}</option>
                            <option value='CANCELED'>{text.cancel}</option>
                          </Select>
                          <HelpHint text={t('help.bookings.status')} />
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </Card>

      <Card>
        <CardTitle>{text.capacityTitle}</CardTitle>
        <div className='mt-3 grid gap-2'>
          {sessions.map((s) => {
            const all = bySession[s.id] || [];
            const booked = all.filter((x) => x.status === 'BOOKED').length;
            const waitlist = all.filter((x) => x.status === 'WAITLISTED').sort((a, b) => new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime());
            const capacity = s.capacityOverride > 0 ? s.capacityOverride : (classById[s.gymClassId]?.capacity ?? 0);
            return (
              <div key={s.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                <p className='font-semibold'>
                  {classById[s.gymClassId]?.title || text.class} - {new Date(s.startAtUtc).toLocaleString()}
                </p>
                <p className='text-sm text-[var(--muted)]'>
                  {text.bookedCapacity}: {booked}/{capacity} · {text.waitlist}: {waitlist.length}
                </p>
                <div className='mt-2'>
                  <Progress value={booked} max={capacity || 1} />
                </div>
                {waitlist.length > 0 ? (
                  <ol className='mt-2 list-decimal pl-5 text-sm text-[var(--text)]'>
                    {waitlist.map((w: any) => (
                      <li key={w.id}>{memberById[w.memberUserId]?.fullName || w.memberUserId}</li>
                    ))}
                  </ol>
                ) : (
                  <p className='text-sm text-[var(--muted)]'>{text.noWaitlist}</p>
                )}
              </div>
            );
          })}
        </div>
      </Card>

      <ConfirmDialog
        open={Boolean(pendingAction)}
        title={text.confirmTitle}
        description={text.confirmDesc}
        onClose={() => setPendingAction(null)}
        onConfirm={applyStatus}
      />
    </div>
  );
}
