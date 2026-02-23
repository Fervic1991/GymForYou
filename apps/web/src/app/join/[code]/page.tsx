'use client';

import { FormEvent, useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { registerMember, resolveJoin } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/lib/i18n/provider';

export default function JoinRegisterPage() {
  const { code } = useParams<{ code: string }>();
  const { push } = useToast();
  const { setTenantPreviewLocale } = useI18n();
  const [tenantName, setTenantName] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('Member123!');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!code) return;
    resolveJoin(code)
      .then((data) => {
        setTenantName(data.tenantName);
        setTenantPreviewLocale(data.defaultLocale === 'es' ? 'es' : 'it');
      })
      .catch((e) => push(String(e), 'danger'));
  }, [code, push, setTenantPreviewLocale]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await registerMember({ joinCode: code, fullName, email, phone, password });
      push('Registrazione completata', 'success');
      window.location.href = '/app/schedule';
    } catch (e) {
      push(String(e), 'danger');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className='mx-auto max-w-xl space-y-4'>
      <Card>
        <CardTitle>Registrazione membro</CardTitle>
        <p className='mt-1 text-sm text-[var(--muted)]'>Registrazione per: <span className='font-semibold text-[var(--primary)]'>{tenantName || code}</span></p>
      </Card>
      <Card>
        <form onSubmit={onSubmit} className='space-y-3'>
          <Input placeholder='Nome completo (min 5)' value={fullName} onChange={(e) => setFullName(e.target.value)} minLength={5} required />
          <Input placeholder='Email' value={email} onChange={(e) => setEmail(e.target.value)} type='email' pattern='^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$' required />
          <Input placeholder='Telefono (solo numeri)' value={phone} onChange={(e) => setPhone(e.target.value)} pattern='^\d*$' inputMode='numeric' />
          <Input placeholder='Password' value={password} onChange={(e) => setPassword(e.target.value)} type='password' required />
          <Button block disabled={loading}>{loading ? '...' : 'Registrati'}</Button>
        </form>
      </Card>
    </div>
  );
}
