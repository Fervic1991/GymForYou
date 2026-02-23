'use client';

import { FormEvent, useState } from 'react';
import { platformLogin } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/lib/i18n/provider';

export default function PlatformLoginPage() {
  const [email, setEmail] = useState('superadmin@gym.local');
  const [password, setPassword] = useState('SuperAdmin123!');
  const [loading, setLoading] = useState(false);
  const { push } = useToast();
  const { t } = useI18n();

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await platformLogin({ email, password });
      push(t('login.platform.success'), 'success');
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
        <CardTitle>{t('login.platform.title')}</CardTitle>
        <p className='mt-1 text-sm text-[var(--muted)]'>{t('login.platform.desc')}</p>
        <form onSubmit={onSubmit} className='mt-4 space-y-3'>
          <Input placeholder={t('field.email')} value={email} onChange={(e) => setEmail(e.target.value)} required />
          <Input placeholder={t('field.password')} value={password} onChange={(e) => setPassword(e.target.value)} required type='password' />
          <Button block disabled={loading}>{loading ? '...' : t('login.platform.submit')}</Button>
        </form>
      </Card>
    </div>
  );
}
