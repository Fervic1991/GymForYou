'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { BookingStatusBadge, Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { EmptyState } from '@/components/ui/empty-state';
import { Skeleton } from '@/components/ui/skeleton';
import { useI18n } from '@/lib/i18n/provider';

type MyBooking = {
  bookingId: string;
  status: 'BOOKED' | 'WAITLISTED' | 'LATE_CANCEL' | 'CANCELED' | 'NO_SHOW';
  startAtUtc: string;
  endAtUtc: string;
  classTitle: string;
  classId: string;
};

export default function MyBookingsPage() {
  const router = useRouter();
  const [items, setItems] = useState<MyBooking[]>([]);
  const [loading, setLoading] = useState(false);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [authorized, setAuthorized] = useState(false);
  const { push } = useToast();
  const { locale } = useI18n();

  const text = locale === 'es'
    ? {
        loginMember: 'Haz login como miembro',
        onlyMembers: 'Esta página es solo para miembros',
        lateCancelApplied: 'Cancelación tardía aplicada',
        canceled: 'Reserva cancelada',
        title: 'Mis reservas',
        subtitle: 'Muestra reservas futuras (confirmadas/en espera).',
        emptyTitle: 'No tienes reservas futuras',
        emptyDesc: 'Ve al horario y reserva tu próxima clase.',
        lateCancel: 'Cancelación tardía',
        cancel: 'Cancelar',
        confirmTitle: 'Confirmar cancelación',
        confirmDesc: '¿Quieres cancelar la reserva seleccionada?'
      }
    : {
        loginMember: 'Effettua login membro',
        onlyMembers: 'Questa pagina e solo per membri',
        lateCancelApplied: 'Cancellazione tardiva applicata',
        canceled: 'Prenotazione cancellata',
        title: 'Le mie prenotazioni',
        subtitle: 'Mostra prenotazioni future (confermate/in attesa).',
        emptyTitle: 'Nessuna prenotazione futura',
        emptyDesc: 'Vai su schedule e prenota il tuo prossimo corso.',
        lateCancel: 'Cancellazione tardiva',
        cancel: 'Cancella',
        confirmTitle: 'Conferma annullamento',
        confirmDesc: 'Vuoi annullare la prenotazione selezionata?'
      };

  async function load() {
    setLoading(true);
    const data = await apiFetch('/bookings/me');
    setItems(data);
    setLoading(false);
  }

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

  useEffect(() => {
    if (!authorized) return;
    load().catch((e) => push(String(e), 'danger'));
  }, [authorized, push]);

  async function cancelBooking(id: string) {
    setLoading(true);
    try {
      const res = await apiFetch(`/bookings/${id}/cancel`, { method: 'PATCH' });
      if (res.status === 'LATE_CANCEL') push(text.lateCancelApplied, 'warning');
      else push(text.canceled, 'success');
      await load();
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setLoading(false);
      setPendingId(null);
    }
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>{text.title}</CardTitle>
        <p className='text-sm text-[var(--muted)]'>{text.subtitle}</p>
      </Card>

      {loading && (
        <Card>
          <div className='space-y-2'>
            <Skeleton className='h-20 w-full' />
            <Skeleton className='h-20 w-full' />
          </div>
        </Card>
      )}

      {!loading && items.length === 0 ? <EmptyState title={text.emptyTitle} description={text.emptyDesc} /> : (
        <div className='space-y-2'>
          {items.map((b) => (
            <Card key={b.bookingId} className='flex flex-wrap items-center justify-between gap-3 bg-[var(--surface-2)]'>
              <div>
                <p className='font-semibold'>{b.classTitle}</p>
                <p className='text-sm text-[var(--muted)]'>{new Date(b.startAtUtc).toLocaleString()} - {new Date(b.endAtUtc).toLocaleTimeString()}</p>
                <div className='mt-1 flex gap-2'>
                  <BookingStatusBadge status={b.status} />
                  {b.status === 'LATE_CANCEL' && <Badge tone='warning'>{text.lateCancel}</Badge>}
                </div>
              </div>
              <Button variant='danger' disabled={loading} onClick={() => setPendingId(b.bookingId)}>{text.cancel}</Button>
            </Card>
          ))}
        </div>
      )}

      <ConfirmDialog
        open={Boolean(pendingId)}
        title={text.confirmTitle}
        description={text.confirmDesc}
        onClose={() => setPendingId(null)}
        onConfirm={() => pendingId && cancelBooking(pendingId)}
      />
    </div>
  );
}
