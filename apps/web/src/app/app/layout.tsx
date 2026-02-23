import Link from 'next/link';

export default function MemberAreaLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className='space-y-4'>
      <nav className='flex flex-wrap gap-2 rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--surface)] p-2 shadow-[var(--shadow-sm)]'>
        <Link className='rounded-[var(--radius-md)] bg-[var(--surface-3)] px-3 py-2 text-sm font-medium text-[var(--text)]' href='/app/calendar'>Calendar</Link>
        <Link className='rounded-[var(--radius-md)] bg-[var(--surface-3)] px-3 py-2 text-sm font-medium text-[var(--text)]' href='/app/schedule'>Schedule</Link>
        <Link className='rounded-[var(--radius-md)] bg-[var(--surface-3)] px-3 py-2 text-sm font-medium text-[var(--text)]' href='/app/videos'>Video training</Link>
        <Link className='rounded-[var(--radius-md)] bg-[var(--surface-3)] px-3 py-2 text-sm font-medium text-[var(--text)]' href='/app/my-bookings'>Le mie prenotazioni</Link>
        <Link className='rounded-[var(--radius-md)] bg-[var(--surface-3)] px-3 py-2 text-sm font-medium text-[var(--text)]' href='/app/subscription'>Subscription</Link>
      </nav>
      {children}
    </div>
  );
}
