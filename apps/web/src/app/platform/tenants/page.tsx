'use client';

import { useEffect, useMemo, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useToast } from '@/components/ui/toast';
import Link from 'next/link';

type Tenant = {
  id: string;
  name: string;
  slug: string;
  defaultLocale: 'it' | 'es';
  status?: string;
  city?: string | null;
  billingStatus?: 'PAID' | 'UNPAID';
  billingValidUntilUtc?: string | null;
};

type OverviewResponse = {
  totals: {
    tenants: number;
    activeTenants: number;
    expiredTenants: number;
    suspendedTenants: number;
    members: number;
    revenueMonth: number;
  };
  tenants: Array<{
    tenantId: string;
    name: string;
    slug: string;
    city?: string | null;
    status: string;
    billingStatus: 'PAID' | 'UNPAID';
    billingValidUntilUtc?: string | null;
    members: number;
    classes: number;
    revenueMonth: number;
  }>;
};

export default function PlatformTenantsPage() {
  const { push } = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [overview, setOverview] = useState<OverviewResponse | null>(null);
  const [selectedTenantId, setSelectedTenantId] = useState('');
  const [selectedLocale, setSelectedLocale] = useState<'it' | 'es'>('it');
  const [billingStatus, setBillingStatus] = useState<'PAID' | 'UNPAID'>('PAID');
  const [billingValidUntil, setBillingValidUntil] = useState('');
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState({
    name: '',
    slug: '',
    ownerName: '',
    ownerEmail: '',
    ownerPassword: 'Owner123!',
    city: '',
    address: '',
    phone: ''
  });

  useEffect(() => {
    const role = localStorage.getItem('role');
    if (role !== 'PLATFORM_ADMIN') {
      window.location.href = '/platform/login';
      return;
    }
    void loadAll();
  }, []);

  const selected = useMemo(() => tenants.find((x) => x.id === selectedTenantId), [selectedTenantId, tenants]);

  useEffect(() => {
    if (!selected) return;
    setSelectedLocale(selected.defaultLocale === 'es' ? 'es' : 'it');
    setBillingStatus(selected.billingStatus === 'UNPAID' ? 'UNPAID' : 'PAID');
    setBillingValidUntil(selected.billingValidUntilUtc ? selected.billingValidUntilUtc.slice(0, 10) : '');
  }, [selected]);

  async function loadAll() {
    setLoading(true);
    try {
      const [tenantData, overviewData] = await Promise.all([
        apiFetch('/platform/tenants') as Promise<Tenant[]>,
        apiFetch('/platform/tenants/overview') as Promise<OverviewResponse>
      ]);
      setTenants(tenantData);
      setOverview(overviewData);
      if (tenantData.length > 0) setSelectedTenantId(tenantData[0].id);
    } catch (e) {
      push(String(e), 'danger');
      window.location.href = '/platform/login';
    } finally {
      setLoading(false);
    }
  }

  async function saveLocale() {
    if (!selectedTenantId) return;
    setSaving(true);
    try {
      await apiFetch(`/platform/tenants/${selectedTenantId}/locale`, {
        method: 'PATCH',
        body: JSON.stringify({ defaultLocale: selectedLocale })
      });
      setTenants((prev) => prev.map((t) => (t.id === selectedTenantId ? { ...t, defaultLocale: selectedLocale } : t)));
      push('Lingua palestra aggiornata', 'success');
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setSaving(false);
    }
  }

  async function saveBilling() {
    if (!selectedTenantId) return;
    setSaving(true);
    try {
      const payload = {
        billingStatus,
        billingValidUntilUtc: billingValidUntil ? new Date(`${billingValidUntil}T00:00:00Z`).toISOString() : null
      };
      await apiFetch(`/platform/tenants/${selectedTenantId}/billing`, {
        method: 'PATCH',
        body: JSON.stringify(payload)
      });
      setTenants((prev) =>
        prev.map((t) =>
          t.id === selectedTenantId
            ? { ...t, billingStatus, billingValidUntilUtc: payload.billingValidUntilUtc }
            : t
        )
      );
      push('Billing tenant aggiornato', 'success');
      await loadAll();
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setSaving(false);
    }
  }

  async function createTenant() {
    if (!createForm.name || !createForm.slug || !createForm.ownerName || !createForm.ownerEmail || !createForm.ownerPassword) {
      push('Compila tutti i campi obbligatori', 'warning');
      return;
    }
    setCreating(true);
    try {
      await apiFetch('/platform/tenants', {
        method: 'POST',
        body: JSON.stringify(createForm)
      });
      push('Palestra creata', 'success');
      setCreateForm({
        name: '',
        slug: '',
        ownerName: '',
        ownerEmail: '',
        ownerPassword: 'Owner123!',
        city: '',
        address: '',
        phone: ''
      });
      await loadAll();
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setCreating(false);
    }
  }

  if (loading) {
    return <div className='p-6 text-sm text-[var(--muted)]'>Caricamento Platform Console...</div>;
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Platform Console (Super Admin)</CardTitle>
        <p className='text-sm text-[var(--muted)]'>Vista globale palestre, statistiche e gestione scadenze pagamento tenant.</p>
      </Card>

      {overview && (
        <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3'>
          <StatCard label='Palestre totali' value={overview.totals.tenants} />
          <StatCard label='Palestre attive' value={overview.totals.activeTenants} />
          <StatCard label='Palestre scadute' value={overview.totals.expiredTenants} />
          <StatCard label='Palestre sospese' value={overview.totals.suspendedTenants} />
          <StatCard label='Membri globali' value={overview.totals.members} />
          <StatCard label='Entrate mese (EUR)' value={overview.totals.revenueMonth.toFixed(2)} />
        </div>
      )}

      {selected && (
        <Card>
          <CardTitle>Tenant Detail</CardTitle>
          <div className='mt-3 grid gap-2 md:grid-cols-[1fr_auto_auto]'>
            <Select value={selectedTenantId} onChange={(e) => setSelectedTenantId(e.target.value)}>
              {tenants.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} - {t.slug}
                </option>
              ))}
            </Select>
            <Select value={selectedLocale} onChange={(e) => setSelectedLocale(e.target.value as 'it' | 'es')}>
              <option value='it'>Lingua: Italiano</option>
              <option value='es'>Lingua: Español</option>
            </Select>
            <Button onClick={saveLocale} disabled={saving}>Salva lingua</Button>
          </div>

          <div className='mt-4 grid gap-2 md:grid-cols-[1fr_1fr_auto]'>
            <Select value={billingStatus} onChange={(e) => setBillingStatus(e.target.value as 'PAID' | 'UNPAID')}>
              <option value='PAID'>Pagamento tenant: PAID</option>
              <option value='UNPAID'>Pagamento tenant: UNPAID</option>
            </Select>
            <Input type='date' value={billingValidUntil} onChange={(e) => setBillingValidUntil(e.target.value)} />
            <Button onClick={saveBilling} disabled={saving}>Salva billing</Button>
          </div>
        </Card>
      )}

      <Card>
        <CardTitle>Directory palestre</CardTitle>
        <div className='mt-2 space-y-2'>
          {overview?.tenants.map((t) => (
            <div key={t.tenantId} className='rounded border border-[var(--border)] bg-[var(--surface-2)] p-2 text-sm'>
              <span className='font-semibold'>{t.name}</span> · {t.slug} · {t.status} · membri {t.members} · corsi {t.classes} · revenue {t.revenueMonth.toFixed(2)} EUR {t.city ? `· ${t.city}` : ''}
              <div className='mt-2'>
                <Link href={`/platform/tenants/${t.tenantId}`}>
                  <Button variant='secondary'>Apri dettaglio</Button>
                </Link>
              </div>
            </div>
          ))}
        </div>
      </Card>

      <Card>
        <CardTitle>Crea nuova palestra</CardTitle>
        <div className='mt-3 grid gap-2 md:grid-cols-2'>
          <Input placeholder='Nome palestra *' value={createForm.name} onChange={(e) => setCreateForm((p) => ({ ...p, name: e.target.value }))} />
          <Input placeholder='Slug * (es: gym-roma)' value={createForm.slug} onChange={(e) => setCreateForm((p) => ({ ...p, slug: e.target.value.toLowerCase().replace(/\s+/g, '-') }))} />
          <Input placeholder='Nome owner *' value={createForm.ownerName} onChange={(e) => setCreateForm((p) => ({ ...p, ownerName: e.target.value }))} />
          <Input placeholder='Email owner *' value={createForm.ownerEmail} onChange={(e) => setCreateForm((p) => ({ ...p, ownerEmail: e.target.value }))} />
          <Input placeholder='Password owner *' type='password' value={createForm.ownerPassword} onChange={(e) => setCreateForm((p) => ({ ...p, ownerPassword: e.target.value }))} />
          <Input placeholder='Citta' value={createForm.city} onChange={(e) => setCreateForm((p) => ({ ...p, city: e.target.value }))} />
          <Input placeholder='Indirizzo' value={createForm.address} onChange={(e) => setCreateForm((p) => ({ ...p, address: e.target.value }))} />
          <Input placeholder='Telefono' value={createForm.phone} onChange={(e) => setCreateForm((p) => ({ ...p, phone: e.target.value }))} />
        </div>
        <div className='mt-3'>
          <Button onClick={createTenant} disabled={creating}>{creating ? '...' : 'Crea palestra'}</Button>
        </div>
      </Card>
    </div>
  );
}

function StatCard({ label, value }: { label: string; value: string | number }) {
  return (
    <Card>
      <p className='text-sm text-[var(--muted)]'>{label}</p>
      <p className='mt-1 text-2xl font-semibold'>{value}</p>
    </Card>
  );
}
