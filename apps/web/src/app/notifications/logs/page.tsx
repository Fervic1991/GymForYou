'use client';

import { useEffect, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { useToast } from '@/components/ui/toast';

export default function NotificationLogsPage() {
  const [items, setItems] = useState<any[]>([]);
  const [sending, setSending] = useState(false);
  const [sendingRenewal, setSendingRenewal] = useState(false);
  const { push } = useToast();

  async function load() {
    const data = await apiFetch('/notifications/logs');
    setItems(data);
  }

  useEffect(() => {
    load().catch((e) => push(String(e), 'danger'));
  }, [push]);

  async function sendReminders() {
    setSending(true);
    try {
      const result = await apiFetch('/notifications/booking-reminders', { method: 'POST' });
      push(`Reminder inviati: ${result.sent}`, 'success');
      await load();
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setSending(false);
    }
  }

  async function sendRenewalReminders() {
    setSendingRenewal(true);
    try {
      const result = await apiFetch('/notifications/renewal-reminders', { method: 'POST' });
      push(`Renewal reminder inviati: ${result.sent}`, 'success');
      await load();
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setSendingRenewal(false);
    }
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Notifiche</CardTitle>
        <p className='text-sm text-[var(--text-muted)]'>Log email e invio reminder prenotazioni di domani.</p>
      </Card>

      <Card>
        <div className='flex flex-wrap gap-2'>
          <Button disabled={sending} onClick={sendReminders}>{sending ? 'Invio...' : 'Invia booking reminders'}</Button>
          <Button variant='warning' disabled={sendingRenewal} onClick={sendRenewalReminders}>
            {sendingRenewal ? 'Invio...' : 'Invia renewal reminders'}
          </Button>
        </div>
      </Card>

      <Card>
        <CardTitle>Ultimi log</CardTitle>
        {items.length === 0 ? <EmptyState title='Nessun log notifiche' description='I log compariranno dopo il primo invio.' /> : (
          <div className='mt-3 overflow-x-auto'>
            <table className='w-full min-w-[860px] text-sm'>
              <thead>
                <tr className='border-b border-[var(--border)] text-left text-[var(--text-muted)]'>
                  <th className='pb-2'>Data</th><th className='pb-2'>To</th><th className='pb-2'>Tipo</th><th className='pb-2'>Payload</th>
                </tr>
              </thead>
              <tbody>
                {items.map((x) => (
                  <tr key={x.id} className='border-b border-[var(--border)]'>
                    <td className='py-2'>{new Date(x.sentAtUtc).toLocaleString()}</td>
                    <td className='py-2'>{x.toEmail}</td>
                    <td className='py-2'>{x.type}</td>
                    <td className='py-2 text-xs'>{x.payload}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}
