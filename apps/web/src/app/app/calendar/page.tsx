'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState } from '@/components/ui/empty-state';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/lib/i18n/provider';

type MemberBooking = {
  bookingId: string;
  status: 'BOOKED' | 'WAITLISTED' | 'LATE_CANCEL' | 'CANCELED' | 'NO_SHOW';
  startAtUtc: string;
  endAtUtc: string;
  classTitle: string;
  classId: string;
};

type SessionVm = {
  id: string;
  gymClassId: string;
  startAtUtc: string;
  endAtUtc: string;
  capacityOverride: number;
  bookedCount?: number;
  exception?: { cancelled?: boolean } | null;
};

type DisplayStatus = 'BOOKED' | 'WAITLISTED' | 'AVAILABLE' | 'FULL' | 'CANCELED';

export default function MemberCalendarPage() {
  const router = useRouter();
  const { push } = useToast();
  const { locale } = useI18n();
  const [authorized, setAuthorized] = useState(false);
  const [loading, setLoading] = useState(true);
  const [weekCursor, setWeekCursor] = useState(new Date());
  const [sessions, setSessions] = useState<SessionVm[]>([]);
  const [classes, setClasses] = useState<any[]>([]);
  const [bookings, setBookings] = useState<MemberBooking[]>([]);
  const [busySessionId, setBusySessionId] = useState<string | null>(null);

  const text = locale === 'es'
    ? {
        title: 'Calendario',
        subtitle: 'Tus sesiones de la semana',
        prevWeek: 'Semana anterior',
        thisWeek: 'Esta semana',
        nextWeek: 'Semana siguiente',
        noSessions: 'No hay sesiones esta semana',
        booked: 'Reservada',
        waitlisted: 'En espera',
        available: 'Disponible',
        full: 'Completa',
        canceled: 'Cancelada',
        book: 'Reservar',
        cancel: 'Cancelar',
        done: 'Actualizado',
        loginMember: 'Haz login como miembro',
        onlyMembers: 'Esta página es solo para miembros'
      }
    : {
        title: 'Calendario',
        subtitle: 'Le tue sessioni della settimana',
        prevWeek: 'Settimana precedente',
        thisWeek: 'Questa settimana',
        nextWeek: 'Settimana successiva',
        noSessions: 'Nessuna sessione questa settimana',
        booked: 'Prenotato',
        waitlisted: 'In attesa',
        available: 'Disponibile',
        full: 'Piena',
        canceled: 'Cancellata',
        book: 'Prenota',
        cancel: 'Annulla',
        done: 'Aggiornato',
        loginMember: 'Effettua login membro',
        onlyMembers: 'Questa pagina e solo per membri'
      };

  useEffect(() => {
    const token = localStorage.getItem('accessToken');
    const role = localStorage.getItem('role');
    if (!token) {
      push(text.loginMember, 'warning');
      router.replace('/app');
      return;
    }
    if (role !== 'MEMBER') {
      push(text.onlyMembers, 'warning');
      router.replace('/dashboard');
      return;
    }
    setAuthorized(true);
  }, [push, router, text.loginMember, text.onlyMembers]);

  const weekRange = useMemo(() => getWeekRange(weekCursor), [weekCursor]);

  async function load() {
    setLoading(true);
    try {
      const fromIso = weekRange.start.toISOString();
      const toIso = weekRange.end.toISOString();
      const [sessionData, classData, myBookingData] = await Promise.all([
        apiFetch(`/classes/sessions?weekStart=${encodeURIComponent(fromIso)}`),
        apiFetch('/classes'),
        apiFetch(`/bookings/me?from=${encodeURIComponent(fromIso)}&to=${encodeURIComponent(toIso)}`)
      ]);
      setSessions(sessionData);
      setClasses(classData);
      setBookings(myBookingData);
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (!authorized) return;
    load().catch(() => {});
  }, [authorized, weekRange.start.getTime(), weekRange.end.getTime()]);

  const classById = useMemo(() => Object.fromEntries(classes.map((c) => [c.id, c])), [classes]);
  const myBookingBySession = useMemo(() => {
    const map: Record<string, MemberBooking> = {};
    bookings.forEach((b) => {
      const key = `${b.classId}-${b.startAtUtc}`;
      map[key] = b;
    });
    return map;
  }, [bookings]);

  const sessionsInWeek = useMemo(() => {
    return sessions
      .filter((s) => {
        const d = new Date(s.startAtUtc);
        return d >= weekRange.start && d <= weekRange.end;
      })
      .sort((a, b) => new Date(a.startAtUtc).getTime() - new Date(b.startAtUtc).getTime());
  }, [sessions, weekRange.end, weekRange.start]);

  const days = useMemo(() => {
    const list = Array.from({ length: 7 }).map((_, i) => {
      const d = new Date(weekRange.start);
      d.setDate(weekRange.start.getDate() + i);
      return d;
    });
    return list.map((day) => ({
      day,
      sessions: sessionsInWeek.filter((s) => isSameDay(new Date(s.startAtUtc), day))
    }));
  }, [sessionsInWeek, weekRange.start]);

  async function handleAction(session: SessionVm, status: DisplayStatus, bookingId?: string) {
    if (busySessionId) return;
    setBusySessionId(session.id);
    try {
      const memberUserId = localStorage.getItem('userId');
      if (!memberUserId) throw new Error('User not found');

      if (status === 'AVAILABLE') {
        await apiFetch('/bookings', { method: 'POST', body: JSON.stringify({ sessionId: session.id, memberUserId }) });
      } else if ((status === 'BOOKED' || status === 'WAITLISTED') && bookingId) {
        await apiFetch(`/bookings/${bookingId}/cancel`, { method: 'PATCH' });
      }
      await load();
      push(text.done, 'success');
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setBusySessionId(null);
    }
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>{text.title}</CardTitle>
        <p className='text-sm text-[var(--muted)]'>{text.subtitle}</p>
      </Card>

      <Card>
        <div className='grid gap-2 sm:grid-cols-3'>
          <Button variant='ghost' onClick={() => setWeekCursor(addDays(weekCursor, -7))}>← {text.prevWeek}</Button>
          <Button variant='secondary' onClick={() => setWeekCursor(new Date())}>{text.thisWeek}</Button>
          <Button variant='ghost' onClick={() => setWeekCursor(addDays(weekCursor, 7))}>{text.nextWeek} →</Button>
        </div>
      </Card>

      {loading && (
        <Card>
          <div className='space-y-2'>
            <Skeleton className='h-20 w-full' />
            <Skeleton className='h-20 w-full' />
            <Skeleton className='h-20 w-full' />
          </div>
        </Card>
      )}

      {!loading && sessionsInWeek.length === 0 && (
        <EmptyState title={text.noSessions} description='' />
      )}

      {!loading && days.map(({ day, sessions: daySessions }) => (
        <Card key={day.toISOString()}>
          <CardTitle className='text-sm'>
            {day.toLocaleDateString(locale === 'es' ? 'es-ES' : 'it-IT', { weekday: 'long', day: '2-digit', month: '2-digit' })}
          </CardTitle>
          <div className='mt-3 space-y-2'>
            {daySessions.length === 0 && <p className='text-sm text-[var(--muted)]'>-</p>}
            {daySessions.map((s) => {
              const classItem = classById[s.gymClassId];
              const key = `${s.gymClassId}-${s.startAtUtc}`;
              const myBooking = myBookingBySession[key];
              const bookedCount = Number(s.bookedCount || 0);
              const capacity = s.capacityOverride > 0 ? s.capacityOverride : Number(classItem?.capacity || 0);
              const canceled = Boolean(s.exception?.cancelled);
              let status: DisplayStatus = 'AVAILABLE';
              if (myBooking?.status === 'BOOKED') status = 'BOOKED';
              else if (myBooking?.status === 'WAITLISTED') status = 'WAITLISTED';
              else if (canceled) status = 'CANCELED';
              else if (capacity > 0 && bookedCount >= capacity) status = 'FULL';

              const tone = status === 'BOOKED'
                ? 'success'
                : status === 'WAITLISTED'
                  ? 'warning'
                  : status === 'AVAILABLE'
                    ? 'info'
                    : status === 'FULL'
                      ? 'danger'
                      : 'neutral';

              const statusLabel = status === 'BOOKED'
                ? text.booked
                : status === 'WAITLISTED'
                  ? text.waitlisted
                  : status === 'AVAILABLE'
                    ? text.available
                    : status === 'FULL'
                      ? text.full
                      : text.canceled;

              return (
                <div key={s.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                  <div className='flex items-start justify-between gap-2'>
                    <div>
                      <p className='font-semibold'>{classItem?.title || 'Class'}</p>
                      <p className='text-xs text-[var(--muted)]'>
                        {new Date(s.startAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} - {new Date(s.endAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                      </p>
                      <p className='text-xs text-[var(--muted)]'>{bookedCount}/{capacity}</p>
                    </div>
                    <Badge tone={tone as any}>{statusLabel}</Badge>
                  </div>
                  <div className='mt-2'>
                    {status === 'AVAILABLE' && (
                      <Button
                        block
                        disabled={busySessionId === s.id}
                        onClick={() => handleAction(s, status)}
                      >
                        {text.book}
                      </Button>
                    )}
                    {(status === 'BOOKED' || status === 'WAITLISTED') && myBooking && (
                      <Button
                        block
                        variant='danger'
                        disabled={busySessionId === s.id}
                        onClick={() => handleAction(s, status, myBooking.bookingId)}
                      >
                        {text.cancel}
                      </Button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </Card>
      ))}
    </div>
  );
}

function addDays(d: Date, days: number) {
  const next = new Date(d);
  next.setDate(next.getDate() + days);
  return next;
}

function getWeekRange(cursor: Date) {
  const start = new Date(cursor);
  const mondayOffset = (start.getDay() + 6) % 7;
  start.setDate(start.getDate() - mondayOffset);
  start.setHours(0, 0, 0, 0);

  const end = new Date(start);
  end.setDate(start.getDate() + 6);
  end.setHours(23, 59, 59, 999);
  return { start, end };
}

function isSameDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate();
}
