'use client';

import { FormEvent, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { login } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/lib/i18n/provider';

export default function GymSlugLoginPage() {
  const params = useParams<{ slug: string }>();
  const tenantSlug = (params?.slug || '').toLowerCase();
  const [email, setEmail] = useState('owner@gym.local');
  const [password, setPassword] = useState('Owner123!');
  const [role, setRole] = useState<'OWNER' | 'MANAGER' | 'TRAINER' | 'MEMBER'>('OWNER');
  const [loading, setLoading] = useState(false);
  const { push } = useToast();
  const { t } = useI18n();

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await login({ tenantSlug, email, password, role });
      push(t('btn.login'), 'success');
      window.location.href = role === 'MEMBER' ? '/app/schedule' : '/dashboard';
    } catch (err) {
      push(String(err), 'danger');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className='mx-auto w-full max-w-xl space-y-4'>
      <Card>
        <CardTitle>Login {tenantSlug || 'palestra'}</CardTitle>
        <p className='mt-1 text-sm text-[var(--text-muted)]'>Accedi alla palestra <strong>{tenantSlug || '-'}</strong>.</p>
        <form onSubmit={onSubmit} className='mt-4 space-y-3'>
          <Input placeholder='Email' value={email} onChange={(e) => setEmail(e.target.value)} required />
          <Input placeholder='Password' value={password} onChange={(e) => setPassword(e.target.value)} required type='password' />
          <Select value={role} onChange={(e) => setRole(e.target.value as 'OWNER' | 'MANAGER' | 'TRAINER' | 'MEMBER')}>
            <option>OWNER</option>
            <option>MANAGER</option>
            <option>TRAINER</option>
            <option>MEMBER</option>
          </Select>
          <Button block disabled={loading}>{loading ? '...' : t('btn.login')}</Button>
        </form>
        <div className='mt-4 border-t border-[var(--border)] pt-3'>
          <Link href='/platform/login' className='text-sm text-[var(--primary)] underline underline-offset-2'>Login Super Admin Platform</Link>
        </div>
      </Card>
    </div>
  );
}
