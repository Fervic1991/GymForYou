'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { Tabs } from '@/components/ui/tabs';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/components/ui/toast';

type TenantDetail = {
  id: string;
  name: string;
  slug: string;
  defaultLocale: 'it' | 'es';
  city?: string | null;
  status: 'ACTIVE' | 'SUSPENDED' | string;
};

type StaffRow = {
  id: string;
  fullName: string;
  email: string;
  role: 'OWNER' | 'MANAGER' | 'TRAINER';
  isActive: boolean;
};

export default function PlatformTenantDetailPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const { push } = useToast();
  const [tab, setTab] = useState<'DETAILS' | 'STAFF'>('STAFF');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [tenant, setTenant] = useState<TenantDetail | null>(null);
  const [staff, setStaff] = useState<StaffRow[]>([]);

  async function load() {
    setLoading(true);
    try {
      const [tenantData, staffData] = await Promise.all([
        apiFetch(`/platform/tenants/${tenantId}`),
        apiFetch(`/platform/tenants/${tenantId}/staff`)
      ]);
      setTenant(tenantData);
      setStaff(staffData);
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const role = localStorage.getItem('role');
    if (role !== 'PLATFORM_ADMIN') {
      window.location.href = '/platform/login';
      return;
    }
    load().catch(() => {});
  }, [tenantId]);

  async function updateRole(userId: string, role: StaffRow['role']) {
    setSaving(true);
    try {
      await apiFetch(`/platform/tenants/${tenantId}/staff/${userId}/role`, {
        method: 'PATCH',
        body: JSON.stringify({ role })
      });
      push('Ruolo aggiornato', 'success');
      await load();
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setSaving(false);
    }
  }

  async function toggleUser(userId: string, isActive: boolean) {
    setSaving(true);
    try {
      await apiFetch(`/platform/tenants/${tenantId}/staff/${userId}/disable`, {
        method: 'PATCH',
        body: JSON.stringify({ isActive: !isActive })
      });
      push('Stato utente aggiornato', 'success');
      await load();
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setSaving(false);
    }
  }

  async function toggleSuspension() {
    if (!tenant) return;
    setSaving(true);
    try {
      const next = tenant.status === 'SUSPENDED' ? false : true;
      await apiFetch(`/platform/tenants/${tenantId}/suspension`, {
        method: 'PATCH',
        body: JSON.stringify({ isSuspended: next })
      });
      push(next ? 'Tenant sospeso' : 'Tenant riattivato', next ? 'warning' : 'success');
      await load();
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <div className='p-6 text-sm text-[var(--muted)]'>Caricamento...</div>;

  return (
    <div className='space-y-4'>
      <Card>
        <div className='flex flex-wrap items-center justify-between gap-2'>
          <div>
            <CardTitle>{tenant?.name || 'Tenant'}</CardTitle>
            <p className='text-sm text-[var(--muted)]'>
              {tenant?.slug} {tenant?.city ? `· ${tenant.city}` : ''}
            </p>
          </div>
          <div className='flex items-center gap-2'>
            <Badge tone={tenant?.status === 'SUSPENDED' ? 'danger' : 'success'}>{tenant?.status || 'N/A'}</Badge>
            <Button variant={tenant?.status === 'SUSPENDED' ? 'success' : 'danger'} onClick={toggleSuspension} disabled={saving}>
              {tenant?.status === 'SUSPENDED' ? 'Riattiva tenant' : 'Sospendi tenant'}
            </Button>
            <Link href='/platform/tenants'>
              <Button variant='ghost'>Indietro</Button>
            </Link>
          </div>
        </div>
      </Card>

      <Card>
        <Tabs
          value={tab}
          onChange={(v) => setTab(v as 'DETAILS' | 'STAFF')}
          items={[
            { value: 'STAFF', label: 'Staff', count: staff.length },
            { value: 'DETAILS', label: 'Dettagli' }
          ]}
        />
      </Card>

      {tab === 'DETAILS' && tenant && (
        <Card>
          <CardTitle>Dettagli tenant</CardTitle>
          <div className='mt-3 grid gap-2 text-sm text-[var(--text)]'>
            <p><span className='text-[var(--muted)]'>Nome:</span> {tenant.name}</p>
            <p><span className='text-[var(--muted)]'>Slug:</span> {tenant.slug}</p>
            <p><span className='text-[var(--muted)]'>Locale:</span> {tenant.defaultLocale}</p>
            <p><span className='text-[var(--muted)]'>Stato:</span> {tenant.status}</p>
          </div>
        </Card>
      )}

      {tab === 'STAFF' && (
        <Card>
          <CardTitle>Staff</CardTitle>
          <div className='mt-3 space-y-2'>
            {staff.map((u) => (
              <div key={u.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                <div className='flex flex-wrap items-center justify-between gap-2'>
                  <div>
                    <p className='font-semibold'>{u.fullName}</p>
                    <p className='text-xs text-[var(--muted)]'>{u.email}</p>
                  </div>
                  <div className='flex items-center gap-2'>
                    <Select value={u.role} onChange={(e) => updateRole(u.id, e.target.value as StaffRow['role'])}>
                      <option value='OWNER'>OWNER</option>
                      <option value='MANAGER'>MANAGER</option>
                      <option value='TRAINER'>TRAINER</option>
                    </Select>
                    <Button variant={u.isActive ? 'warning' : 'success'} onClick={() => toggleUser(u.id, u.isActive)} disabled={saving}>
                      {u.isActive ? 'Disattiva' : 'Attiva'}
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}
    </div>
  );
}

