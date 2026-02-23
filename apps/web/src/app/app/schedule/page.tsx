'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { BookingStatusBadge, SubscriptionStatusBadge } from '@/components/ui/badge';
import { useToast } from '@/components/ui/toast';
import { EmptyState } from '@/components/ui/empty-state';
import { Skeleton } from '@/components/ui/skeleton';

export default function MemberSchedulePage() {
  const router = useRouter();
  const [sessions, setSessions] = useState<any[]>([]);
  const [myBookings, setMyBookings] = useState<any[]>([]);
  const [mySubs, setMySubs] = useState<any[]>([]);
  const [selectedSessionId, setSelectedSessionId] = useState<string>('');
  const [authorized, setAuthorized] = useState(false);
  const [loading, setLoading] = useState(true);
  const { push } = useToast();

  async function load() {
    setLoading(true);
    const [s, b, sub] = await Promise.all([apiFetch('/classes/sessions'), apiFetch('/bookings/me'), apiFetch('/billing/me/subscriptions')]);
    setSessions(s);
    setMyBookings(b);
    setMySubs(sub);
    setLoading(false);
  }

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
    load().catch((e) => push(String(e), 'danger'));
  }, [authorized, push]);

  const bySession = useMemo(() => Object.fromEntries(myBookings.map((b) => [b.classId + b.startAtUtc, b])), [myBookings]);
  const activeSub = mySubs.find((s) => s.status === 'ACTIVE');

  async function book(sessionId: string) {
    const memberUserId = localStorage.getItem('userId');
    if (!memberUserId) return push('Fai login membro', 'warning');
    try {
      const res = await apiFetch('/bookings', { method: 'POST', body: JSON.stringify({ sessionId, memberUserId }) });
      push(`Prenotazione: ${res.status}`, res.status === 'WAITLISTED' ? 'warning' : 'success');
      load();
    } catch (e) {
      push(String(e), 'danger');
    }
  }

  async function cancel(bookingId: string) {
    await apiFetch(`/bookings/${bookingId}/cancel`, { method: 'PATCH' });
    push('Prenotazione annullata', 'success');
    load();
  }

  async function bookSelected() {
    if (!selectedSessionId) return;
    await book(selectedSessionId);
    setSelectedSessionId('');
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Schedule</CardTitle>
        <div className='mt-2 flex items-center gap-2'>
          <span className='text-sm text-[var(--muted)]'>Subscription:</span>
          {activeSub ? <SubscriptionStatusBadge status={activeSub.status} /> : <SubscriptionStatusBadge status='PAST_DUE' />}
        </div>
      </Card>

      {loading && (
        <Card>
          <div className='space-y-2'>
            <Skeleton className='h-24 w-full' />
            <Skeleton className='h-24 w-full' />
            <Skeleton className='h-24 w-full' />
          </div>
        </Card>
      )}

      {!loading && sessions.length === 0 ? <EmptyState title='Nessuna sessione disponibile' description='Le sessioni della settimana appariranno qui.' /> : (
        <div className='space-y-2 pb-20 md:pb-0'>
          {sessions.map((s) => {
            const key = s.gymClassId + s.startAtUtc;
            const mine = bySession[key];
            return (
              <Card key={s.id} className='flex flex-wrap items-center justify-between gap-3 bg-[var(--surface-2)]'>
                <div>
                  <p className='font-semibold'>Session {new Date(s.startAtUtc).toLocaleDateString()}</p>
                  <p className='text-sm text-[var(--muted)]'>{new Date(s.startAtUtc).toLocaleString()}</p>
                </div>
                <div className='flex items-center gap-2'>
                  {mine && <BookingStatusBadge status={mine.status} />}
                  {!mine && (
                    <>
                      <Button className='hidden md:inline-flex' onClick={() => book(s.id)}>Prenota</Button>
                      <Button className='md:hidden' variant='secondary' onClick={() => setSelectedSessionId(s.id)}>Seleziona</Button>
                    </>
                  )}
                  {mine && (mine.status === 'BOOKED' || mine.status === 'WAITLISTED') && (
                    <Button variant='danger' onClick={() => cancel(mine.bookingId)}>Annulla</Button>
                  )}
                </div>
              </Card>
            );
          })}
        </div>
      )}

      {selectedSessionId && (
        <div className='fixed inset-x-3 bottom-18 z-20 rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--surface)] p-3 shadow-[var(--shadow-md)] md:hidden'>
          <p className='mb-2 text-sm font-semibold'>Sessione selezionata</p>
          <Button block onClick={bookSelected}>Prenota ora</Button>
        </div>
      )}
    </div>
  );
}
