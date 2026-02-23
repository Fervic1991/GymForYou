'use client';

import { useEffect, useMemo, useState } from 'react';
import { API_URL, apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useToast } from '@/components/ui/toast';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';

type TenantProfile = {
  tenantId: string;
  name: string;
  slug: string;
  defaultLocale: 'it' | 'es';
  city?: string | null;
  address?: string | null;
  phone?: string | null;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
};

type JoinLink = {
  code: string;
  url: string;
  expiresAtUtc?: string | null;
  maxUses?: number | null;
  usesCount: number;
};

export default function TenantProfilePage() {
  const { push } = useToast();
  const [data, setData] = useState<TenantProfile | null>(null);
  const [joinLink, setJoinLink] = useState<JoinLink | null>(null);
  const [confirmRotate, setConfirmRotate] = useState(false);
  const [savingBranding, setSavingBranding] = useState(false);

  async function load() {
    const [profile, link] = await Promise.all([apiFetch('/tenant/profile'), apiFetch('/tenant/join-link')]);
    setData(profile);
    setJoinLink(link);
  }

  useEffect(() => {
    load().catch((e) => push(String(e), 'danger'));
  }, [push]);

  const enrollLink = useMemo(() => {
    if (joinLink?.url) return joinLink.url;
    return '';
  }, [joinLink]);

  async function rotateCode() {
    await apiFetch('/tenant/join-link/rotate', { method: 'POST' });
    const next = await apiFetch('/tenant/join-link');
    setJoinLink(next);
    push('Join link rigenerato', 'success');
  }

  async function copyLink() {
    if (!enrollLink) return;
    await navigator.clipboard.writeText(enrollLink);
    push('Link copiato', 'success');
  }

  async function saveBranding() {
    if (!data) return;
    setSavingBranding(true);
    try {
      const updated = await apiFetch('/tenant/profile', {
        method: 'PUT',
        body: JSON.stringify({
          name: data.name,
          city: data.city || null,
          address: data.address || null,
          phone: data.phone || null,
          logoUrl: data.logoUrl || null,
          primaryColor: data.primaryColor || '#0ea5e9',
          secondaryColor: data.secondaryColor || '#111827'
        })
      });
      setData(updated);
      push('Branding palestra salvato', 'success');
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setSavingBranding(false);
    }
  }

  if (!data) return null;

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Profilo palestra</CardTitle>
        <p className='mt-2 text-sm text-[var(--muted)]'>{data.name} · {data.slug}</p>
        <p className='text-sm text-[var(--muted)]'>Locale default: {data.defaultLocale}</p>
      </Card>

      <Card>
        <CardTitle>Branding palestra</CardTitle>
        <div className='mt-3 grid gap-3 md:grid-cols-2'>
          <label className='text-sm'>
            Nome palestra
            <Input value={data.name} onChange={(e) => setData({ ...data, name: e.target.value })} />
          </label>
          <label className='text-sm'>
            Logo URL
            <Input placeholder='https://...' value={data.logoUrl || ''} onChange={(e) => setData({ ...data, logoUrl: e.target.value })} />
          </label>
          <label className='text-sm'>
            Colore primario
            <Input type='color' value={data.primaryColor || '#0ea5e9'} onChange={(e) => setData({ ...data, primaryColor: e.target.value })} />
          </label>
          <label className='text-sm'>
            Colore secondario
            <Input type='color' value={data.secondaryColor || '#111827'} onChange={(e) => setData({ ...data, secondaryColor: e.target.value })} />
          </label>
          <label className='text-sm'>
            Citta
            <Input value={data.city || ''} onChange={(e) => setData({ ...data, city: e.target.value })} />
          </label>
          <label className='text-sm'>
            Telefono
            <Input value={data.phone || ''} onChange={(e) => setData({ ...data, phone: e.target.value })} />
          </label>
        </div>
        <div className='mt-3 flex items-center gap-3'>
          <Button onClick={saveBranding} disabled={savingBranding}>{savingBranding ? '...' : 'Salva branding'}</Button>
          {data.logoUrl ? <img src={data.logoUrl} alt='Logo palestra' className='h-10 w-10 rounded border border-[var(--border)] bg-white object-contain' /> : null}
          <span className='inline-flex items-center gap-2 text-xs text-[var(--muted)]'>
            <span className='inline-block h-4 w-4 rounded' style={{ backgroundColor: data.primaryColor || '#0ea5e9' }} />
            <span className='inline-block h-4 w-4 rounded' style={{ backgroundColor: data.secondaryColor || '#111827' }} />
          </span>
        </div>
      </Card>

      <Card>
        <CardTitle>Link iscrizione membri</CardTitle>
        <p className='mt-2 rounded border border-[var(--border)] bg-[var(--surface-2)] p-2 text-sm font-mono break-all'>{enrollLink}</p>
        <div className='mt-2 flex gap-2'>
          <Button onClick={copyLink}>Copia link</Button>
        </div>
        <div className='mt-4'>
          <img
            src={`https://api.qrserver.com/v1/create-qr-code/?size=220x220&data=${encodeURIComponent(enrollLink)}`}
            alt='QR iscrizione membri'
            className='h-44 w-44 rounded border border-[var(--border)] bg-white p-1'
          />
        </div>
      </Card>

      <Card>
        <CardTitle>JoinCode</CardTitle>
        <p className='mt-2 text-2xl font-bold tracking-widest text-[var(--primary)]'>{joinLink?.code || 'N/D'}</p>
        <p className='mt-1 text-sm text-[var(--muted)]'>Uses: {joinLink?.usesCount ?? 0}{joinLink?.maxUses ? ` / ${joinLink.maxUses}` : ''}</p>
        <Button className='mt-3' variant='warning' onClick={() => setConfirmRotate(true)}>Rigenera link</Button>
      </Card>

      <ConfirmDialog
        open={confirmRotate}
        title='Rigenerare join link?'
        description='Il link precedente verrà disattivato.'
        onClose={() => setConfirmRotate(false)}
        onConfirm={() => {
          setConfirmRotate(false);
          rotateCode().catch((e) => push(String(e), 'danger'));
        }}
      />
    </div>
  );
}
