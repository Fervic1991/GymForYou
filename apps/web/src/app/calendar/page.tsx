'use client';

import Link from 'next/link';
import { StaffCalendarView } from '@/components/calendar/staff-calendar-view';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export default function CalendarPage() {
  return (
    <div className='space-y-4'>
      <Card className='flex flex-wrap items-center justify-between gap-3'>
        <p className='text-sm text-[var(--muted)]'>Vista dedicata calendario palestra.</p>
        <Link href='/calendar/screen' target='_blank'>
          <Button variant='secondary'>Apri modalita schermo</Button>
        </Link>
      </Card>
      <StaffCalendarView />
    </div>
  );
}

