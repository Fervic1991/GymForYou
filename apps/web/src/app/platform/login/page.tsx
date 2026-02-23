'use client';

import { FormEvent, useState } from 'react';
import { platformLogin } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';

export default function PlatformLoginPage() {
  const [email, setEmail] = useState('superadmin@gym.local');
  const [password, setPassword] = useState('SuperAdmin123!');
  const [loading, setLoading] = useState(false);
  const { push } = useToast();

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await platformLogin({ email, password });
      push('Login super admin ok', 'success');
      window.location.href = '/platform/tenants';
    } catch (err) {
      push(String(err), 'danger');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className='mx-auto max-w-lg space-y-4'>
      <Card>
        <CardTitle>Super Admin Login</CardTitle>
        <p className='mt-1 text-sm text-[var(--muted)]'>Accesso globale a tutte le palestre e statistiche platform.</p>
        <form onSubmit={onSubmit} className='mt-4 space-y-3'>
          <Input placeholder='Email' value={email} onChange={(e) => setEmail(e.target.value)} required />
          <Input placeholder='Password' value={password} onChange={(e) => setPassword(e.target.value)} required type='password' />
          <Button block disabled={loading}>{loading ? '...' : 'Entra in Platform Console'}</Button>
        </form>
      </Card>
    </div>
  );
}
