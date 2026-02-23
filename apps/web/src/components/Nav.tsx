'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';

export default function Nav() {
  const [isLogged, setIsLogged] = useState(false);

  useEffect(() => {
    const refresh = () => setIsLogged(Boolean(localStorage.getItem('accessToken')));
    refresh();
    const timer = setInterval(refresh, 1000);
    window.addEventListener('storage', refresh);
    return () => {
      clearInterval(timer);
      window.removeEventListener('storage', refresh);
    };
  }, []);

  function onLogout() {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('tenantId');
    localStorage.removeItem('role');
    localStorage.removeItem('userId');
    setIsLogged(false);
    window.location.href = '/login';
  }

  return (
    <nav className="flex flex-wrap gap-2 border-b border-slate-800 bg-slate-950 px-4 py-3 text-sm text-white">
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/dashboard">Dashboard</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/members">Members</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/classes">Classes</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/bookings">Bookings</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/checkin">Check-in</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/billing">Billing</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/tenant/settings">Tenant Settings</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/notifications/logs">Notifications</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/reports">Reports</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/app">Area Membro</Link>
      <Link className="rounded px-3 py-1.5 hover:bg-slate-800" href="/app/my-bookings">Le mie prenotazioni</Link>
      {!isLogged && <Link className="rounded bg-blue-600 px-3 py-1.5 hover:bg-blue-700" href="/login">Login</Link>}
      {isLogged && <button className="rounded bg-rose-600 px-3 py-1.5 hover:bg-rose-700" onClick={onLogout}>Logout</button>}
    </nav>
  );
}
