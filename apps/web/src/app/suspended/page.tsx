'use client';

import Link from 'next/link';
import { Card, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export default function SuspendedPage() {
  return (
    <div className='mx-auto max-w-xl space-y-4'>
      <Card>
        <CardTitle>Account sospeso</CardTitle>
        <p className='mt-2 text-sm text-[var(--muted)]'>Il tuo tenant e sospeso. Contatta amministrazione.</p>
        <div className='mt-4'>
          <Link href='/login'>
            <Button variant='secondary'>Torna al login</Button>
          </Link>
        </div>
      </Card>
    </div>
  );
}

