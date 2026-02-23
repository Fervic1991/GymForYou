'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/cn';
import { useI18n } from '@/lib/i18n/provider';
import { API_URL } from '@/lib/api';
import { useTour } from '@/lib/tour/provider';

type Role = 'OWNER' | 'MANAGER' | 'TRAINER' | 'MEMBER' | 'PLATFORM_ADMIN' | '';
type UiTheme = 'light' | 'dark';

type NavItem = { href: string; label: string; icon: string };

const staffNav: NavItem[] = [
  { href: '/dashboard', label: 'Dashboard', icon: '📊' },
  { href: '/members', label: 'Membri', icon: '👥' },
  { href: '/classes', label: 'Corsi', icon: '🗓️' },
  { href: '/calendar', label: 'Calendario', icon: '🗓️' },
  { href: '/bookings', label: 'Prenotazioni', icon: '🎟️' },
  { href: '/checkin', label: 'Check-in', icon: '📱' },
  { href: '/billing', label: 'Billing', icon: '💳' },
  { href: '/videos', label: 'Video Training', icon: '🎬' },
  { href: '/reports', label: 'Report', icon: '📈' },
  { href: '/notifications/logs', label: 'Notifiche', icon: '🔔' },
  { href: '/tenant', label: 'Palestra', icon: '🏢' }
];

const memberNav: NavItem[] = [
  { href: '/app/calendar', label: 'Calendar', icon: '📅' },
  { href: '/app/schedule', label: 'Schedule', icon: '🗓️' },
  { href: '/app/videos', label: 'Videos', icon: '🎬' },
  { href: '/app/my-bookings', label: 'My bookings', icon: '🎟️' },
  { href: '/app/subscription', label: 'Subscription', icon: '🪪' }
];

export default function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { t } = useI18n();
  const { startTour, canShowRestart } = useTour();
  const [role, setRole] = useState<Role>('');
  const [tenantName, setTenantName] = useState('');
  const [isLogged, setIsLogged] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [theme, setTheme] = useState<UiTheme>('light');

  useEffect(() => {
    const refresh = () => {
      const nextRole = (localStorage.getItem('role') ?? '') as Role;
      setRole(nextRole);
      setIsLogged(Boolean(localStorage.getItem('accessToken')));
      const key = getThemeStorageKey(nextRole);
      const saved = localStorage.getItem(key) as UiTheme | null;
      const nextTheme = saved || getDefaultTheme(nextRole);
      setTheme(nextTheme);
    };
    refresh();
    const id = setInterval(refresh, 1200);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    const onSuspended = () => {
      const role = localStorage.getItem('role');
      if (role === 'PLATFORM_ADMIN') return;
      window.location.href = '/suspended';
    };
    window.addEventListener('tenant-suspended', onSuspended);
    return () => window.removeEventListener('tenant-suspended', onSuspended);
  }, []);

  useEffect(() => {
    const token = localStorage.getItem('accessToken');
    const currentRole = (localStorage.getItem('role') ?? '') as Role;
    if (!token || currentRole === 'PLATFORM_ADMIN') {
      setTenantName('');
      return;
    }

    fetch(`${API_URL}/tenant/settings`, {
      headers: { Authorization: `Bearer ${token}` }
    })
      .then(async (r) => {
        if (!r.ok) throw new Error('settings failed');
        const data = await r.json();
        setTenantName(data?.tenantName || '');
      })
      .catch(() => setTenantName(''));
  }, [role]);

  useEffect(() => {
    if (typeof window !== 'undefined' && 'serviceWorker' in navigator) {
      navigator.serviceWorker.register('/sw.js').catch(() => {});
    }
  }, []);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) {
      meta.setAttribute('content', theme === 'dark' ? '#0B0F1A' : '#F3F6FB');
    }
  }, [theme]);

  const toggleTheme = () => {
    const nextTheme: UiTheme = theme === 'dark' ? 'light' : 'dark';
    setTheme(nextTheme);
    localStorage.setItem(getThemeStorageKey(role), nextTheme);
  };

  const onJoinPublic = /^\/join\/[^/]+$/.test(pathname);
  const onGymLoginPage = /^\/[^/]+\/login$/.test(pathname);
  const isScreenPage = pathname.startsWith('/calendar/screen');
  const onAuthPage = pathname === '/login' || pathname === '/' || pathname === '/app' || onJoinPublic || onGymLoginPage || pathname.startsWith('/platform/') || pathname === '/suspended';
  if (isScreenPage) {
    return <main className='min-h-screen'>{children}</main>;
  }

  const isMember = role === 'MEMBER';
  const nav = useMemo(() => {
    if (role === 'PLATFORM_ADMIN') return [{ href: '/platform/tenants', label: 'Platform Console', icon: '🧩' }];
    if (isMember) return memberNav;
    return staffNav.filter((item) => {
      if (item.href === '/tenant' && !(role === 'OWNER' || role === 'MANAGER')) return false;
      return true;
    });
  }, [isMember, role]);

  const logout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('tenantId');
    localStorage.removeItem('role');
    localStorage.removeItem('userId');
    window.dispatchEvent(new Event('auth-changed'));
    window.location.href = '/login';
  };

  if (onAuthPage) {
    return <main className='mx-auto w-full max-w-[1600px] p-3 md:p-5'>{children}</main>;
  }

  if (isMember) {
    return (
      <div className='min-h-screen bg-transparent pb-24'>
        <header className='sticky top-0 z-20 border-b border-[var(--border)] bg-[color-mix(in_srgb,var(--surface)_92%,transparent)] px-4 py-3 backdrop-blur'>
          <div className='flex items-center justify-between gap-2'>
            <div>
              <p className='text-sm font-semibold text-[var(--text)]'>{t('shell.memberTitle')}</p>
              <p className='text-xs text-[var(--muted)]'>{t('shell.memberSubtitle')}</p>
            </div>
            <div className='flex items-center gap-2'>
              <Button variant='ghost' onClick={toggleTheme}>{theme === 'dark' ? t('theme.lightShort') : t('theme.darkShort')}</Button>
              {canShowRestart && (
                <Button variant='ghost' onClick={startTour}>{t('tour.restart')}</Button>
              )}
            </div>
          </div>
        </header>
        <main className='mx-auto w-full max-w-4xl space-y-4 p-4'>{children}</main>
        <nav className='fixed inset-x-0 bottom-0 z-30 grid grid-cols-5 border-t border-[var(--border)] bg-[color-mix(in_srgb,var(--surface)_96%,transparent)] px-2 py-2 backdrop-blur'>
          {memberNav.map((item) => (
            <Link key={item.href} href={item.href} className={cn('flex min-h-11 flex-col items-center justify-center rounded text-[11px] font-medium', pathname === item.href || pathname.startsWith(`${item.href}/`) ? 'text-[var(--primary)]' : 'text-[var(--muted)]')}>
              <span className='text-lg leading-none'>{item.icon}</span>
              <span>{translateNavLabel(item.label, t)}</span>
            </Link>
          ))}
        </nav>
      </div>
    );
  }

  return (
    <div className='min-h-screen bg-transparent'>
      <div className='flex w-full gap-3 p-2 md:gap-4 md:p-3'>
        <aside className='hidden h-[calc(100vh-2rem)] w-72 shrink-0 rounded-[var(--radius-xl)] border border-[var(--border)] bg-[color-mix(in_srgb,var(--surface)_96%,transparent)] p-4 shadow-[var(--shadow-md)] md:flex md:flex-col'>
          <div>
            <p className='text-lg font-bold text-[var(--text)]'>Gym SaaS</p>
            <p className='text-xs text-[var(--muted)]'>{t('shell.receptionWorkspace')}</p>
          </div>

          <nav className='mt-5 space-y-1.5'>
            {nav.map((item) => (
                <Link key={item.href} href={item.href} className={navClass(pathname, item.href)}>
                  <span>{item.icon}</span>
                  <span>{translateNavLabel(item.label, t)}</span>
                </Link>
              ))}
          </nav>

          <div className='mt-auto space-y-2 border-t border-[var(--border)] pt-4'>
            <p className='text-xs text-[var(--muted)]'>{t('shell.gymLabel')}: <span className='text-[var(--text)]'>{tenantName || t('shell.gymFallback')}</span></p>
            <Button variant='ghost' block onClick={toggleTheme}>{theme === 'dark' ? t('theme.light') : t('theme.dark')}</Button>
            {canShowRestart && <Button variant='ghost' block onClick={startTour}>{t('tour.restart')}</Button>}
            {isLogged ? <Button variant='danger' block onClick={logout}>{t('btn.logout')}</Button> : <Link href='/login'><Button block>{t('btn.login')}</Button></Link>}
          </div>
        </aside>

        <div className='min-w-0 flex-1 space-y-3'>
          <header className='rounded-[var(--radius-xl)] border border-[var(--border)] bg-[color-mix(in_srgb,var(--surface)_96%,transparent)] px-3 py-3 shadow-[var(--shadow-sm)] md:px-4'>
            <div className='flex items-center justify-between gap-3'>
              <div>
                <p className='text-sm font-semibold text-[var(--text)] md:text-base'>{t('shell.staffConsole')}</p>
                <p className='text-xs text-[var(--muted)]'>{t('shell.staffSubtitle')}</p>
              </div>
              <div className='flex items-center gap-2'>
                <Button className='hidden md:inline-flex' variant='ghost' onClick={toggleTheme}>{theme === 'dark' ? t('theme.light') : t('theme.dark')}</Button>
                <button className='inline-flex min-h-11 items-center justify-center rounded-[var(--radius-md)] border border-[var(--border)] px-3 text-sm text-[var(--text)] md:hidden' onClick={() => setMobileMenuOpen((x) => !x)}>
                  {t('shell.menu')}
                </button>
              </div>
            </div>
            {mobileMenuOpen && (
              <div className='mt-3 grid grid-cols-2 gap-2 md:hidden'>
                {nav.map((item) => (
                  <Link key={item.href} href={item.href} className={cn('rounded-[var(--radius-md)] border border-[var(--border)] px-3 py-2 text-sm', pathname === item.href ? 'bg-[var(--primary)] text-black' : 'bg-[var(--surface-2)] text-[var(--text)]')} onClick={() => setMobileMenuOpen(false)}>
                    {item.icon} {translateNavLabel(item.label, t)}
                  </Link>
                ))}
              </div>
            )}
          </header>
          <main>{children}</main>
        </div>
      </div>
    </div>
  );
}

function translateNavLabel(label: string, t: (key: string) => string) {
  const map: Record<string, string> = {
    Dashboard: 'nav.dashboard',
    Membri: 'nav.members',
    Corsi: 'nav.classes',
    Prenotazioni: 'nav.bookings',
    'Check-in': 'nav.checkin',
    Billing: 'nav.billing',
    'Video Training': 'nav.videos',
    Report: 'nav.reports',
    Notifiche: 'nav.notifications',
    Impostazioni: 'nav.settings',
    Schedule: 'nav.schedule',
    Calendar: 'nav.calendar',
    'My bookings': 'nav.myBookings',
    Subscription: 'nav.subscription',
    'Platform Console': 'nav.platform',
    Palestra: 'nav.gym',
    Calendario: 'nav.calendar'
  };
  const key = map[label];
  return key ? t(key) : label;
}

function navClass(pathname: string, href: string) {
  const active = pathname === href || pathname.startsWith(`${href}/`);
  return cn(
    'flex min-h-11 items-center gap-2 rounded-[var(--radius-md)] px-3 py-2 text-sm font-medium transition',
    active ? 'bg-[var(--primary)] text-black' : 'text-[var(--text)] hover:bg-[var(--surface-3)]'
  );
}

function getDefaultTheme(role: Role): UiTheme {
  if (role === 'MEMBER' || role === 'PLATFORM_ADMIN') return 'dark';
  return 'light';
}

function getThemeStorageKey(role: Role): string {
  if (role === 'MEMBER') return 'uiThemeMember';
  if (role === 'PLATFORM_ADMIN') return 'uiThemePlatform';
  return 'uiThemeStaff';
}
