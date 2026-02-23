import Link from 'next/link';
import { Card, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export default function Home() {
  return (
    <div className='mx-auto grid max-w-4xl gap-4 md:grid-cols-2'>
      <Card>
        <CardTitle>Staff</CardTitle>
        <p className='mt-2 text-sm text-[var(--text-muted)]'>Dashboard reception, membri, corsi, check-in, billing e report.</p>
        <div className='mt-4'>
          <Link href='/gym-demo/login'><Button>Vai al login staff</Button></Link>
        </div>
      </Card>
      <Card>
        <CardTitle>Area Membro</CardTitle>
        <p className='mt-2 text-sm text-[var(--text-muted)]'>Prenotazioni, subscription, annullo rapido.</p>
        <div className='mt-4'>
          <Link href='/app'><Button variant='secondary'>Vai area membro</Button></Link>
        </div>
      </Card>
      <Card className='md:col-span-2'>
        <CardTitle>Platform Console</CardTitle>
        <p className='mt-2 text-sm text-[var(--text-muted)]'>Login super admin per visione globale palestre, membri, corsi e billing tenant.</p>
        <div className='mt-4'>
          <Link href='/platform/login'><Button variant='ghost'>Login super admin</Button></Link>
        </div>
      </Card>
    </div>
  );
}
