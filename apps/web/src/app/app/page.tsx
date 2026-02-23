'use client';

import { useState } from 'react';
import { resolveJoin } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/lib/i18n/provider';

export default function MemberLogin() {
  const [joinCode, setJoinCode] = useState('DEMO123');
  const [tenantName, setTenantName] = useState('');
  const { push } = useToast();
  const { setTenantPreviewLocale, t } = useI18n();

  async function resolveTenantByJoin() {
    const data = await resolveJoin(joinCode);
    setTenantName(data.tenantName);
    setTenantPreviewLocale(data.defaultLocale === 'es' ? 'es' : 'it');
  }

  async function goToJoin() {
    try {
      await resolveTenantByJoin();
      window.location.href = `/join/${encodeURIComponent(joinCode)}`;
    } catch (e) {
      push(String(e), 'danger');
    }
  }

  return (
    <div className='mx-auto max-w-lg space-y-4'>
      <Card>
        <CardTitle>{t('login.member.title')}</CardTitle>
        <p className='mt-1 text-sm text-[var(--text-muted)]'>{t('login.member.desc')}</p>
      </Card>
      <Card>
        <div className='space-y-3'>
          <Input placeholder={t('login.member.joinCodePlaceholder')} value={joinCode} onChange={(e) => setJoinCode(e.target.value.toUpperCase())} required />
          {tenantName && <p className='text-sm text-[var(--primary)]'>{t('login.member.gymLabel')}: {tenantName}</p>}
          <div className='flex flex-wrap gap-2'>
            <Button type='button' variant='secondary' onClick={() => resolveTenantByJoin().catch((e) => push(String(e), 'danger'))}>{t('login.member.verifyCode')}</Button>
            <Button type='button' onClick={goToJoin}>{t('login.member.openJoinLink')}</Button>
          </div>
        </div>
      </Card>
    </div>
  );
}
