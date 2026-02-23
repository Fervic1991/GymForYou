'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/components/ui/toast';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Tabs } from '@/components/ui/tabs';
import { useI18n } from '@/lib/i18n/provider';

const DAY_OPTIONS = [
  { value: 1, label: 'Lunedi' },
  { value: 2, label: 'Martedi' },
  { value: 3, label: 'Mercoledi' },
  { value: 4, label: 'Giovedi' },
  { value: 5, label: 'Venerdi' },
  { value: 6, label: 'Sabato' },
  { value: 0, label: 'Domenica' }
];

type GymClass = {
  id: string;
  title: string;
  description: string;
  trainerUserId: string;
  capacity: number;
  weeklyDayOfWeek: number;
  startTimeUtc: string;
  durationMinutes: number;
  isActive: boolean;
  maxWeeklyBookingsPerMember: number;
};

export default function ClassesPage() {
  const [classes, setClasses] = useState<GymClass[]>([]);
  const [sessions, setSessions] = useState<any[]>([]);
  const [bookings, setBookings] = useState<any[]>([]);
  const [users, setUsers] = useState<any[]>([]);
  const [editingClassId, setEditingClassId] = useState('');
  const [discontinueId, setDiscontinueId] = useState('');

  const [title, setTitle] = useState('');
  const [trainerUserId, setTrainerUserId] = useState('');
  const [capacity, setCapacity] = useState('20');
  const [weeklyDayOfWeek, setWeeklyDayOfWeek] = useState('2');
  const [startTimeUtc, setStartTimeUtc] = useState('15:00');
  const [durationMinutes, setDurationMinutes] = useState('60');
  const [maxWeeklyBookingsPerMember, setMaxWeeklyBookingsPerMember] = useState('10');

  const [exceptionSessionId, setExceptionSessionId] = useState('');
  const [cancelled, setCancelled] = useState(false);
  const [rescheduledStartLocal, setRescheduledStartLocal] = useState('');
  const [rescheduledEndLocal, setRescheduledEndLocal] = useState('');
  const [calendarView, setCalendarView] = useState<'week' | 'month'>('week');
  const [monthCursor, setMonthCursor] = useState(() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  });

  const { push } = useToast();
  const { locale } = useI18n();

  const load = async () => {
    const [c, s, b, u] = await Promise.all([apiFetch('/classes'), apiFetch('/classes/sessions'), apiFetch('/bookings'), apiFetch('/staff')]);
    setClasses(c);
    setSessions(s);
    setBookings(b);
    setUsers(u.filter((x: any) => x.role === 'TRAINER' || x.role === 3));
  };

  useEffect(() => {
    load().catch((e) => push(String(e), 'danger'));
  }, [push]);

  const classById = useMemo(() => Object.fromEntries(classes.map((c) => [c.id, c])), [classes]);

  const weeklySessions = useMemo(() => {
    const acc: Record<number, any[]> = { 0: [], 1: [], 2: [], 3: [], 4: [], 5: [], 6: [] };
    sessions.forEach((s) => {
      const day = new Date(s.startAtUtc).getDay();
      acc[day].push(s);
    });
    Object.values(acc).forEach((list) => list.sort((a, b) => new Date(a.startAtUtc).getTime() - new Date(b.startAtUtc).getTime()));
    return acc;
  }, [sessions]);

  const monthDays = useMemo(() => {
    const year = monthCursor.getFullYear();
    const month = monthCursor.getMonth();
    const firstDay = new Date(year, month, 1);
    const mondayOffset = (firstDay.getDay() + 6) % 7;
    const gridStart = new Date(year, month, 1 - mondayOffset);
    const days: Date[] = [];
    for (let i = 0; i < 42; i += 1) {
      const d = new Date(gridStart);
      d.setDate(gridStart.getDate() + i);
      days.push(d);
    }
    return days;
  }, [monthCursor]);

  const monthSessions = useMemo(() => {
    const acc: Record<string, any[]> = {};
    sessions.forEach((s) => {
      const key = toLocalDayKey(new Date(s.startAtUtc));
      acc[key] = acc[key] || [];
      acc[key].push(s);
    });
    Object.values(acc).forEach((list) => list.sort((a, b) => new Date(a.startAtUtc).getTime() - new Date(b.startAtUtc).getTime()));
    return acc;
  }, [sessions]);

  function metrics(session: any) {
    const c = classById[session.gymClassId];
    const capacityValue = session.capacityOverride > 0 ? session.capacityOverride : (c?.capacity ?? 0);
    const booked = bookings.filter((b) => b.sessionId === session.id && b.status === 'BOOKED').length;
    const waitlist = bookings.filter((b) => b.sessionId === session.id && b.status === 'WAITLISTED').length;
    return { capacity: capacityValue, booked, waitlist };
  }

  function resetForm() {
    setEditingClassId('');
    setTitle('');
    setTrainerUserId('');
    setCapacity('20');
    setWeeklyDayOfWeek('2');
    setStartTimeUtc('15:00');
    setDurationMinutes('60');
    setMaxWeeklyBookingsPerMember('10');
  }

  async function submitClass(e: FormEvent) {
    e.preventDefault();
    const payload = {
      title,
      description: '',
      trainerUserId,
      capacity: Number(capacity),
      recurrenceRule: `FREQ=WEEKLY;BYDAY=${toByDay(Number(weeklyDayOfWeek))}`,
      weeklyDayOfWeek: Number(weeklyDayOfWeek),
      startTimeUtc,
      durationMinutes: Number(durationMinutes),
      maxWeeklyBookingsPerMember: Number(maxWeeklyBookingsPerMember)
    };

    if (editingClassId) {
      await apiFetch(`/classes/${editingClassId}`, { method: 'PUT', body: JSON.stringify(payload) });
      push('Corso aggiornato', 'success');
    } else {
      await apiFetch('/classes', { method: 'POST', body: JSON.stringify(payload) });
      push('Corso creato (sessioni future generate)', 'success');
    }

    resetForm();
    await load();
  }

  function startEdit(c: GymClass) {
    setEditingClassId(c.id);
    setTitle(c.title);
    setTrainerUserId(c.trainerUserId);
    setCapacity(String(c.capacity));
    setWeeklyDayOfWeek(String(c.weeklyDayOfWeek));
    setStartTimeUtc(c.startTimeUtc || '15:00');
    setDurationMinutes(String(c.durationMinutes || 60));
    setMaxWeeklyBookingsPerMember(String(c.maxWeeklyBookingsPerMember || 10));
  }

  async function discontinueClass() {
    await apiFetch(`/classes/${discontinueId}/discontinue`, { method: 'POST' });
    setDiscontinueId('');
    push('Corso discontinuato: future sessioni cancellate, storico preservato', 'success');
    await load();
  }

  async function saveException(e: FormEvent) {
    e.preventDefault();
    await apiFetch('/classes/sessions/exceptions', {
      method: 'POST',
      body: JSON.stringify({
        sessionId: exceptionSessionId,
        cancelled,
        rescheduledStartAtUtc: rescheduledStartLocal ? new Date(rescheduledStartLocal).toISOString() : null,
        rescheduledEndAtUtc: rescheduledEndLocal ? new Date(rescheduledEndLocal).toISOString() : null,
        trainerOverrideUserId: null,
        reason: cancelled ? 'Cancelled by staff' : 'Rescheduled by staff'
      })
    });
    setExceptionSessionId('');
    setCancelled(false);
    setRescheduledStartLocal('');
    setRescheduledEndLocal('');
    push('Eccezione sessione salvata', 'success');
    await load();
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Corsi ricorrenti e calendario</CardTitle>
        <p className='text-sm text-[var(--muted)]'>Configura corsi permanenti (es. Yoga ogni Martedi alle 15:00), modifica giorno/ora e discontinuazione senza perdere statistiche.</p>
      </Card>

      <div className='grid gap-4 lg:grid-cols-2'>
        <Card>
          <CardTitle>{editingClassId ? 'Modifica corso' : 'Nuovo corso ricorrente'}</CardTitle>
          <form onSubmit={submitClass} className='mt-3 space-y-2'>
            <Input placeholder='Titolo corso' value={title} onChange={(e) => setTitle(e.target.value)} required />
            <Select value={trainerUserId} onChange={(e) => setTrainerUserId(e.target.value)} required>
              <option value=''>Trainer</option>
              {users.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
            </Select>
            <div className='grid grid-cols-2 gap-2'>
              <Select value={weeklyDayOfWeek} onChange={(e) => setWeeklyDayOfWeek(e.target.value)}>
                {DAY_OPTIONS.map((d) => <option key={d.value} value={d.value}>{d.label}</option>)}
              </Select>
              <Input type='time' value={startTimeUtc} onChange={(e) => setStartTimeUtc(e.target.value)} required />
            </div>
            <div className='grid grid-cols-3 gap-2'>
              <Input type='number' min='1' value={capacity} onChange={(e) => setCapacity(e.target.value)} placeholder='Capienza' />
              <Input type='number' min='15' step='15' value={durationMinutes} onChange={(e) => setDurationMinutes(e.target.value)} placeholder='Durata min' />
              <Input type='number' min='1' value={maxWeeklyBookingsPerMember} onChange={(e) => setMaxWeeklyBookingsPerMember(e.target.value)} placeholder='Limite sett.' />
            </div>
            <div className='flex gap-2'>
              <Button block>{editingClassId ? 'Salva modifiche' : 'Crea corso ricorrente'}</Button>
              {editingClassId && <Button type='button' variant='ghost' onClick={resetForm}>Annulla</Button>}
            </div>
          </form>
        </Card>

        <Card>
          <CardTitle>Eccezione singola sessione</CardTitle>
          <form onSubmit={saveException} className='mt-3 space-y-2'>
            <Select value={exceptionSessionId} onChange={(e) => setExceptionSessionId(e.target.value)} required>
              <option value=''>Sessione</option>
              {sessions.map((s) => <option key={s.id} value={s.id}>{formatSessionLabel(s.startAtUtc, classById[s.gymClassId]?.title)}</option>)}
            </Select>
            <label className='flex items-center gap-2 text-sm'>
              <input type='checkbox' checked={cancelled} onChange={(e) => setCancelled(e.target.checked)} /> Sessione cancellata
            </label>
            <Input type='datetime-local' value={rescheduledStartLocal} onChange={(e) => setRescheduledStartLocal(e.target.value)} />
            <Input type='datetime-local' value={rescheduledEndLocal} onChange={(e) => setRescheduledEndLocal(e.target.value)} />
            <Button block variant='warning'>Salva eccezione</Button>
          </form>
        </Card>
      </div>

      <Card>
        <CardTitle>Elenco corsi</CardTitle>
        <div className='mt-3 space-y-2'>
          {classes.map((c) => (
            <div key={c.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
              <div className='flex flex-wrap items-center justify-between gap-2'>
                <p className='font-semibold'>{c.title}</p>
                <div className='flex items-center gap-2'>
                  <Badge tone={c.isActive ? 'success' : 'neutral'}>{c.isActive ? 'Attivo' : 'Discontinuato'}</Badge>
                  {c.isActive && <Button variant='ghost' onClick={() => startEdit(c)}>Modifica</Button>}
                  {c.isActive && <Button variant='danger' onClick={() => setDiscontinueId(c.id)}>Discontinua</Button>}
                </div>
              </div>
              <p className='text-xs text-[var(--muted)]'>
                {DAY_OPTIONS.find((d) => d.value === c.weeklyDayOfWeek)?.label} ore {c.startTimeUtc} · durata {c.durationMinutes} min · capienza {c.capacity}
              </p>
            </div>
          ))}
        </div>
      </Card>

      <div className='grid gap-3 lg:grid-cols-2'>
        <Card className='lg:col-span-2'>
          <div className='flex flex-wrap items-center justify-between gap-3'>
            <CardTitle>Calendario corsi</CardTitle>
            <Tabs
              value={calendarView}
              onChange={(value) => setCalendarView(value as 'week' | 'month')}
              items={[
                { value: 'week', label: locale === 'es' ? 'Semana' : 'Settimana' },
                { value: 'month', label: locale === 'es' ? 'Mes' : 'Mese' }
              ]}
            />
          </div>

          {calendarView === 'week' ? (
            <div className='mt-3 grid gap-3 lg:grid-cols-2'>
              {DAY_OPTIONS.slice().sort((a, b) => a.value - b.value).map((day) => (
                <div key={day.value} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                  <p className='font-semibold'>{day.label}</p>
                  <div className='mt-2 space-y-2'>
                    {(weeklySessions[day.value] || []).length === 0 && <p className='text-sm text-[var(--muted)]'>Nessuna sessione.</p>}
                    {(weeklySessions[day.value] || []).map((s) => {
                      const m = metrics(s);
                      const c = classById[s.gymClassId];
                      const isFull = m.capacity > 0 && m.booked >= m.capacity;
                      const cancelledByException = Boolean(s.exception?.cancelled);
                      return (
                        <div key={s.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface)] p-3'>
                          <div className='flex items-center justify-between gap-2'>
                            <p className='font-semibold'>{c?.title || 'Classe'}</p>
                            {cancelledByException ? <Badge tone='neutral'>Cancellata</Badge> : isFull ? <Badge tone='danger'>Piena</Badge> : <Badge tone='info'>Disponibile</Badge>}
                          </div>
                          <p className='text-xs text-[var(--muted)]'>{formatDateRange(s.startAtUtc, s.endAtUtc)}</p>
                          <div className='mt-2'>
                            <Progress value={m.booked} max={m.capacity || 1} />
                            <p className='mt-1 text-xs text-[var(--muted)]'>Prenotati: {m.booked}/{m.capacity} · Waitlist: {m.waitlist}</p>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className='mt-3 space-y-3'>
              <div className='flex items-center justify-between'>
                <Button
                  type='button'
                  variant='ghost'
                  onClick={() => setMonthCursor(new Date(monthCursor.getFullYear(), monthCursor.getMonth() - 1, 1))}
                >
                  ← {locale === 'es' ? 'Mes anterior' : 'Mese precedente'}
                </Button>
                <p className='text-sm font-semibold'>
                  {monthCursor.toLocaleDateString(locale === 'es' ? 'es-ES' : 'it-IT', { month: 'long', year: 'numeric' })}
                </p>
                <Button
                  type='button'
                  variant='ghost'
                  onClick={() => setMonthCursor(new Date(monthCursor.getFullYear(), monthCursor.getMonth() + 1, 1))}
                >
                  {locale === 'es' ? 'Mes siguiente' : 'Mese successivo'} →
                </Button>
              </div>

              <div className='grid grid-cols-7 gap-1 text-center text-xs font-semibold text-[var(--muted)]'>
                {(locale === 'es' ? ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'] : ['Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab', 'Dom']).map((d) => (
                  <div key={d} className='rounded bg-[var(--surface-2)] p-2'>{d}</div>
                ))}
              </div>

              <div className='grid grid-cols-7 gap-1'>
                {monthDays.map((day) => {
                  const inMonth = day.getMonth() === monthCursor.getMonth();
                  const dayKey = toLocalDayKey(day);
                  const daySessions = monthSessions[dayKey] || [];
                  const isToday = toLocalDayKey(new Date()) === dayKey;
                  return (
                    <div
                      key={dayKey}
                      className={`min-h-28 rounded border p-2 ${inMonth ? 'border-[var(--border)] bg-[var(--surface-2)]' : 'border-transparent bg-[var(--surface)] opacity-60'} ${isToday ? 'ring-1 ring-[var(--primary)]' : ''}`}
                    >
                      <div className='mb-1 flex items-center justify-between'>
                        <span className='text-xs font-semibold'>{day.getDate()}</span>
                        {daySessions.length > 0 && <Badge tone='info'>{daySessions.length}</Badge>}
                      </div>

                      <div className='space-y-1'>
                        {daySessions.slice(0, 3).map((s) => {
                          const m = metrics(s);
                          const c = classById[s.gymClassId];
                          const isFull = m.capacity > 0 && m.booked >= m.capacity;
                          const cancelledByException = Boolean(s.exception?.cancelled);
                          return (
                            <div key={s.id} className='rounded bg-[var(--surface)] px-1.5 py-1 text-[11px]'>
                              <p className='truncate font-medium'>{c?.title || 'Classe'}</p>
                              <p className='text-[10px] text-[var(--muted)]'>{new Date(s.startAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</p>
                              {cancelledByException ? (
                                <Badge tone='neutral'>Cancellata</Badge>
                              ) : isFull ? (
                                <Badge tone='danger'>Piena</Badge>
                              ) : (
                                <Badge tone='success'>{m.booked}/{m.capacity}</Badge>
                              )}
                            </div>
                          );
                        })}
                        {daySessions.length > 3 && (
                          <p className='text-[11px] text-[var(--muted)]'>+{daySessions.length - 3} altre</p>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </Card>
      </div>

      <ConfirmDialog
        open={Boolean(discontinueId)}
        title='Discontinuare il corso?'
        description='Il corso verra fermato per il futuro. Lo storico e le statistiche rimangono disponibili.'
        onClose={() => setDiscontinueId('')}
        onConfirm={() => {
          discontinueClass().catch((e) => push(String(e), 'danger'));
        }}
      />
    </div>
  );
}

function toLocalDayKey(d: Date) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function formatDateRange(startIso: string, endIso: string) {
  const s = new Date(startIso);
  const e = new Date(endIso);
  return `${s.toLocaleDateString()} ${s.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} - ${e.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
}

function formatSessionLabel(startIso: string, title?: string) {
  const d = new Date(startIso);
  return `${title || 'Sessione'} · ${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
}

function toByDay(day: number) {
  switch (day) {
    case 0: return 'SU';
    case 1: return 'MO';
    case 2: return 'TU';
    case 3: return 'WE';
    case 4: return 'TH';
    case 5: return 'FR';
    case 6: return 'SA';
    default: return 'MO';
  }
}
