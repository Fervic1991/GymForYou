'use client';

import { FormEvent, useEffect, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';

type TenantSettings = {
  tenantId: string;
  cancelCutoffHours: number;
  maxNoShows30d: number;
  weeklyBookingLimit: number;
  bookingBlockDays: number;
  defaultLocale: 'it' | 'es';
};

export default function TenantSettingsPage() {
  const [settings, setSettings] = useState<TenantSettings | null>(null);
  const { push } = useToast();

  async function load() {
    const data = await apiFetch('/tenant/settings');
    setSettings(data);
  }

  useEffect(() => {
    load().catch((e) => push(String(e), 'danger'));
  }, [push]);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!settings) return;
    const payload = {
      cancelCutoffHours: settings.cancelCutoffHours,
      maxNoShows30d: settings.maxNoShows30d,
      weeklyBookingLimit: settings.weeklyBookingLimit,
      bookingBlockDays: settings.bookingBlockDays
    };
    const data = await apiFetch('/tenant/settings', { method: 'PUT', body: JSON.stringify(payload) });
    setSettings(data);
    push('Impostazioni salvate', 'success');
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Tenant Settings</CardTitle>
        <p className='text-sm text-[var(--text-muted)]'>Policy prenotazioni e no-show.</p>
      </Card>
      {settings && (
        <Card>
          <form onSubmit={save} className='grid gap-2 md:grid-cols-2'>
            <label className='text-sm'>Cancel cutoff hours<Input type='number' value={settings.cancelCutoffHours} onChange={(e) => setSettings({ ...settings, cancelCutoffHours: Number(e.target.value) })} /></label>
            <label className='text-sm'>Max no-shows 30d<Input type='number' value={settings.maxNoShows30d} onChange={(e) => setSettings({ ...settings, maxNoShows30d: Number(e.target.value) })} /></label>
            <label className='text-sm'>Weekly booking limit<Input type='number' value={settings.weeklyBookingLimit} onChange={(e) => setSettings({ ...settings, weeklyBookingLimit: Number(e.target.value) })} /></label>
            <label className='text-sm'>Booking block days<Input type='number' value={settings.bookingBlockDays} onChange={(e) => setSettings({ ...settings, bookingBlockDays: Number(e.target.value) })} /></label>
            <label className='text-sm'>Default locale (platform only)<Input value={settings.defaultLocale} disabled /></label>
            <Button className='md:col-span-2'>Salva</Button>
          </form>
        </Card>
      )}
    </div>
  );
}
