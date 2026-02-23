'use client';

import { useEffect, useMemo, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Tabs } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { Select } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/lib/i18n/provider';

type ViewMode = 'day' | 'week' | 'month';

export function StaffCalendarView({ screenMode = false }: { screenMode?: boolean }) {
  const [classes, setClasses] = useState<any[]>([]);
  const [sessions, setSessions] = useState<any[]>([]);
  const [bookings, setBookings] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [mode, setMode] = useState<ViewMode>('week');
  const [cursorDate, setCursorDate] = useState(new Date());
  const [autoRotate, setAutoRotate] = useState(screenMode);
  const [rotateSeconds, setRotateSeconds] = useState(30);
  const { push } = useToast();
  const { locale } = useI18n();

  const text = locale === 'es'
    ? {
        title: 'Calendario de gimnasio',
        subtitle: 'Vista diaria, semanal y mensual para operaciones y pantalla.',
        refresh: 'Actualizar',
        fullScreen: 'Pantalla completa',
        exitFullScreen: 'Salir pantalla completa',
        autoRotate: 'Rotación automática',
        interval: 'Intervalo',
        play: 'Iniciar',
        pause: 'Pausar',
        day: 'Día',
        week: 'Semana',
        month: 'Mes',
        prev: 'Anterior',
        next: 'Siguiente',
        today: 'Hoy',
        noSessions: 'Sin sesiones',
        available: 'Disponible',
        full: 'Completa',
        canceled: 'Cancelada',
        waitlist: 'Espera',
        booked: 'Reservadas'
      }
    : {
        title: 'Calendario palestra',
        subtitle: 'Vista giornaliera, settimanale e mensile per operativita e schermo.',
        refresh: 'Aggiorna',
        fullScreen: 'Schermo intero',
        exitFullScreen: 'Esci da schermo intero',
        autoRotate: 'Rotazione automatica',
        interval: 'Intervallo',
        play: 'Avvia',
        pause: 'Pausa',
        day: 'Giorno',
        week: 'Settimana',
        month: 'Mese',
        prev: 'Precedente',
        next: 'Successivo',
        today: 'Oggi',
        noSessions: 'Nessuna sessione',
        available: 'Disponibile',
        full: 'Piena',
        canceled: 'Cancellata',
        waitlist: 'Attesa',
        booked: 'Prenotati'
      };

  const load = async () => {
    setLoading(true);
    try {
      const [c, s, b] = await Promise.all([apiFetch('/classes'), apiFetch('/classes/sessions'), apiFetch('/bookings')]);
      setClasses(c);
      setSessions(s);
      setBookings(b);
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load().catch(() => {});
    const timer = setInterval(() => load().catch(() => {}), 30000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    if (!autoRotate) return;
    const timer = setInterval(() => {
      setMode((prev) => {
        if (prev === 'day') return 'week';
        if (prev === 'week') return 'month';
        return 'day';
      });
    }, rotateSeconds * 1000);
    return () => clearInterval(timer);
  }, [autoRotate, rotateSeconds]);

  const classById = useMemo(() => Object.fromEntries(classes.map((c) => [c.id, c])), [classes]);

  const monthDays = useMemo(() => {
    const year = cursorDate.getFullYear();
    const month = cursorDate.getMonth();
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
  }, [cursorDate]);

  const weekDays = useMemo(() => {
    const d = new Date(cursorDate);
    const mondayOffset = (d.getDay() + 6) % 7;
    const monday = new Date(d);
    monday.setDate(d.getDate() - mondayOffset);
    return Array.from({ length: 7 }).map((_, i) => {
      const day = new Date(monday);
      day.setDate(monday.getDate() + i);
      return day;
    });
  }, [cursorDate]);

  const sessionsByDay = useMemo(() => {
    const acc: Record<string, any[]> = {};
    sessions.forEach((s) => {
      const key = dayKey(new Date(s.startAtUtc));
      acc[key] = acc[key] || [];
      acc[key].push(s);
    });
    Object.values(acc).forEach((list) => list.sort((a, b) => new Date(a.startAtUtc).getTime() - new Date(b.startAtUtc).getTime()));
    return acc;
  }, [sessions]);

  function metrics(session: any) {
    const c = classById[session.gymClassId];
    const capacity = session.capacityOverride > 0 ? session.capacityOverride : (c?.capacity ?? 0);
    const booked = bookings.filter((b) => b.sessionId === session.id && b.status === 'BOOKED').length;
    const waitlist = bookings.filter((b) => b.sessionId === session.id && b.status === 'WAITLISTED').length;
    return { capacity, booked, waitlist };
  }

  function moveCursor(dir: -1 | 1) {
    const next = new Date(cursorDate);
    if (mode === 'day') next.setDate(next.getDate() + dir);
    if (mode === 'week') next.setDate(next.getDate() + dir * 7);
    if (mode === 'month') next.setMonth(next.getMonth() + dir);
    setCursorDate(next);
  }

  function resetToday() {
    setCursorDate(new Date());
  }

  async function toggleFullscreen() {
    if (typeof document === 'undefined') return;
    if (document.fullscreenElement) {
      await document.exitFullscreen();
      return;
    }
    await document.documentElement.requestFullscreen();
  }

  const isFullscreen = typeof document !== 'undefined' && Boolean(document.fullscreenElement);
  const containerClass = screenMode ? 'space-y-3 p-2 md:p-4' : 'space-y-4';

  return (
    <div className={containerClass}>
      <Card>
        <div className='flex flex-wrap items-center justify-between gap-3'>
          <div>
            <CardTitle>{text.title}</CardTitle>
            <p className='text-sm text-[var(--muted)]'>{text.subtitle}</p>
          </div>
          <div className='flex flex-wrap items-center gap-2'>
            <Button variant='ghost' onClick={() => load().catch(() => {})}>{text.refresh}</Button>
            <Button variant='secondary' onClick={toggleFullscreen}>
              {isFullscreen ? text.exitFullScreen : text.fullScreen}
            </Button>
          </div>
        </div>
      </Card>

      <Card>
        <div className='flex flex-wrap items-center justify-between gap-3'>
          <Tabs
            value={mode}
            onChange={(value) => setMode(value as ViewMode)}
            items={[
              { value: 'day', label: text.day },
              { value: 'week', label: text.week },
              { value: 'month', label: text.month }
            ]}
          />
          <div className='flex flex-wrap gap-2'>
            <Button variant='ghost' onClick={() => moveCursor(-1)}>← {text.prev}</Button>
            <Button variant='ghost' onClick={resetToday}>{text.today}</Button>
            <Button variant='ghost' onClick={() => moveCursor(1)}>{text.next} →</Button>
          </div>
        </div>
        <div className='mt-3 flex flex-wrap items-center gap-2'>
          <span className='text-xs text-[var(--muted)]'>{text.autoRotate}</span>
          <Button variant={autoRotate ? 'warning' : 'secondary'} onClick={() => setAutoRotate((v) => !v)}>
            {autoRotate ? text.pause : text.play}
          </Button>
          <span className='text-xs text-[var(--muted)]'>{text.interval}</span>
          <Select
            className='w-28'
            value={String(rotateSeconds)}
            onChange={(e) => setRotateSeconds(Number(e.target.value))}
          >
            <option value='15'>15s</option>
            <option value='30'>30s</option>
            <option value='60'>60s</option>
            <option value='120'>120s</option>
          </Select>
        </div>
      </Card>

      {loading && (
        <Card>
          <div className='space-y-2'>
            <Skeleton className='h-16 w-full' />
            <Skeleton className='h-16 w-full' />
            <Skeleton className='h-16 w-full' />
          </div>
        </Card>
      )}

      {!loading && mode === 'day' && (
        <Card>
          <CardTitle>
            {cursorDate.toLocaleDateString(locale === 'es' ? 'es-ES' : 'it-IT', { weekday: 'long', day: '2-digit', month: 'long', year: 'numeric' })}
          </CardTitle>
          <div className='mt-3 space-y-2'>
            {(sessionsByDay[dayKey(cursorDate)] || []).length === 0 && <p className='text-sm text-[var(--muted)]'>{text.noSessions}</p>}
            {(sessionsByDay[dayKey(cursorDate)] || []).map((s) => {
              const m = metrics(s);
              const c = classById[s.gymClassId];
              const full = m.capacity > 0 && m.booked >= m.capacity;
              const cancelled = Boolean(s.exception?.cancelled);
              return (
                <div key={s.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                  <div className='flex items-center justify-between gap-2'>
                    <p className='font-semibold'>{c?.title || 'Classe'}</p>
                    {cancelled ? <Badge tone='neutral'>{text.canceled}</Badge> : full ? <Badge tone='danger'>{text.full}</Badge> : <Badge tone='success'>{text.available}</Badge>}
                  </div>
                  <p className='text-xs text-[var(--muted)]'>
                    {new Date(s.startAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} - {new Date(s.endAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                  </p>
                  <div className='mt-2'>
                    <Progress value={m.booked} max={m.capacity || 1} />
                    <p className='mt-1 text-xs text-[var(--muted)]'>{text.booked}: {m.booked}/{m.capacity} · {text.waitlist}: {m.waitlist}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </Card>
      )}

      {!loading && mode === 'week' && (
        <div className='grid gap-3 lg:grid-cols-2'>
          {weekDays.map((day) => (
            <Card key={dayKey(day)}>
              <CardTitle>{day.toLocaleDateString(locale === 'es' ? 'es-ES' : 'it-IT', { weekday: 'long', day: '2-digit', month: '2-digit' })}</CardTitle>
              <div className='mt-2 space-y-2'>
                {(sessionsByDay[dayKey(day)] || []).length === 0 && <p className='text-sm text-[var(--muted)]'>{text.noSessions}</p>}
                {(sessionsByDay[dayKey(day)] || []).map((s) => {
                  const m = metrics(s);
                  const c = classById[s.gymClassId];
                  const full = m.capacity > 0 && m.booked >= m.capacity;
                  const cancelled = Boolean(s.exception?.cancelled);
                  return (
                    <div key={s.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                      <div className='flex items-center justify-between gap-2'>
                        <p className='font-semibold'>{c?.title || 'Classe'}</p>
                        {cancelled ? <Badge tone='neutral'>{text.canceled}</Badge> : full ? <Badge tone='danger'>{text.full}</Badge> : <Badge tone='info'>{text.available}</Badge>}
                      </div>
                      <p className='text-xs text-[var(--muted)]'>{new Date(s.startAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</p>
                      <p className='text-xs text-[var(--muted)]'>{text.booked}: {m.booked}/{m.capacity} · {text.waitlist}: {m.waitlist}</p>
                    </div>
                  );
                })}
              </div>
            </Card>
          ))}
        </div>
      )}

      {!loading && mode === 'month' && (
        <Card>
          <div className='grid grid-cols-7 gap-1 text-center text-xs font-semibold text-[var(--muted)]'>
            {(locale === 'es' ? ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'] : ['Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab', 'Dom']).map((d) => (
              <div key={d} className='rounded bg-[var(--surface-2)] p-2'>{d}</div>
            ))}
          </div>
          <div className='mt-1 grid grid-cols-7 gap-1'>
            {monthDays.map((day) => {
              const daySessions = sessionsByDay[dayKey(day)] || [];
              const inMonth = day.getMonth() === cursorDate.getMonth();
              const isToday = dayKey(day) === dayKey(new Date());
              return (
                <div key={dayKey(day)} className={`min-h-24 rounded border p-2 ${inMonth ? 'border-[var(--border)] bg-[var(--surface-2)]' : 'border-transparent bg-[var(--surface)] opacity-60'} ${isToday ? 'ring-1 ring-[var(--primary)]' : ''}`}>
                  <div className='mb-1 flex items-center justify-between'>
                    <span className='text-xs font-semibold'>{day.getDate()}</span>
                    {daySessions.length > 0 && <Badge tone='info'>{daySessions.length}</Badge>}
                  </div>
                  <div className='space-y-1'>
                    {daySessions.slice(0, 3).map((s) => {
                      const m = metrics(s);
                      const c = classById[s.gymClassId];
                      const full = m.capacity > 0 && m.booked >= m.capacity;
                      const cancelled = Boolean(s.exception?.cancelled);
                      return (
                        <div key={s.id} className='rounded bg-[var(--surface)] px-1.5 py-1 text-[11px]'>
                          <p className='truncate font-medium'>{c?.title || 'Classe'}</p>
                          <p className='text-[10px] text-[var(--muted)]'>{new Date(s.startAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</p>
                          {cancelled ? <Badge tone='neutral'>{text.canceled}</Badge> : full ? <Badge tone='danger'>{text.full}</Badge> : <Badge tone='success'>{m.booked}/{m.capacity}</Badge>}
                        </div>
                      );
                    })}
                    {daySessions.length > 3 && <p className='text-[11px] text-[var(--muted)]'>+{daySessions.length - 3}</p>}
                  </div>
                </div>
              );
            })}
          </div>
        </Card>
      )}
    </div>
  );
}

function dayKey(d: Date) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}
