'use client';

import Link from 'next/link';
import { StaffCalendarView } from '@/components/calendar/staff-calendar-view';
import { Button } from '@/components/ui/button';

export default function CalendarScreenPage() {
  return (
    <div className='min-h-screen space-y-3 bg-[var(--bg)] p-2 md:p-4'>
      <div className='flex items-center justify-between'>
        <h1 className='text-base font-semibold text-[var(--text)] md:text-lg'>Calendar Screen</h1>
        <Link href='/calendar'>
          <Button variant='ghost'>Chiudi</Button>
        </Link>
      </div>
      <StaffCalendarView screenMode />
    </div>
  );
}

