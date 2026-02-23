'use client';

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/lib/i18n/provider';

type TourMode = 'staff' | 'member' | null;
type Role = 'OWNER' | 'MANAGER' | 'TRAINER' | 'MEMBER' | 'PLATFORM_ADMIN' | '';

type TourStep = {
  route: string;
  titleKey: string;
  descKey: string;
};

type TourContextType = {
  startTour: () => void;
  canShowRestart: boolean;
};

const TourContext = createContext<TourContextType | null>(null);

const STAFF_STEPS: TourStep[] = [
  { route: '/dashboard', titleKey: 'tour.staff.dashboard.title', descKey: 'tour.staff.dashboard.desc' },
  { route: '/members', titleKey: 'tour.staff.members.title', descKey: 'tour.staff.members.desc' },
  { route: '/classes', titleKey: 'tour.staff.classes.title', descKey: 'tour.staff.classes.desc' },
  { route: '/bookings', titleKey: 'tour.staff.bookings.title', descKey: 'tour.staff.bookings.desc' },
  { route: '/billing', titleKey: 'tour.staff.billing.title', descKey: 'tour.staff.billing.desc' }
];

const MEMBER_STEPS: TourStep[] = [
  { route: '/app/schedule', titleKey: 'tour.member.schedule.title', descKey: 'tour.member.schedule.desc' },
  { route: '/app/my-bookings', titleKey: 'tour.member.myBookings.title', descKey: 'tour.member.myBookings.desc' },
  { route: '/app/subscription', titleKey: 'tour.member.subscription.title', descKey: 'tour.member.subscription.desc' },
  { route: '/app/calendar', titleKey: 'tour.member.calendar.title', descKey: 'tour.member.calendar.desc' }
];

const SEEN_KEY_STAFF = 'tour_seen_staff_v1';
const SEEN_KEY_MEMBER = 'tour_seen_member_v1';

export function TourProvider({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { t } = useI18n();
  const [mode, setMode] = useState<TourMode>(null);
  const [open, setOpen] = useState(false);
  const [index, setIndex] = useState(0);
  const [initialized, setInitialized] = useState(false);

  const steps = useMemo(() => (mode === 'member' ? MEMBER_STEPS : mode === 'staff' ? STAFF_STEPS : []), [mode]);
  const current = steps[index];

  const getRole = useCallback((): Role => {
    if (typeof window === 'undefined') return '';
    return (localStorage.getItem('role') ?? '') as Role;
  }, []);

  const getModeByRole = useCallback((role: Role): TourMode => {
    if (role === 'MEMBER') return 'member';
    if (role === 'OWNER' || role === 'MANAGER' || role === 'TRAINER') return 'staff';
    return null;
  }, []);

  const getSeenKey = useCallback((tourMode: TourMode) => (tourMode === 'member' ? SEEN_KEY_MEMBER : SEEN_KEY_STAFF), []);

  const startTour = useCallback(() => {
    const role = getRole();
    const nextMode = getModeByRole(role);
    if (!nextMode) return;
    setMode(nextMode);
    setIndex(0);
    setOpen(true);
    const first = (nextMode === 'member' ? MEMBER_STEPS : STAFF_STEPS)[0];
    if (first && pathname !== first.route) router.push(first.route);
  }, [getModeByRole, getRole, pathname, router]);

  useEffect(() => {
    const init = () => {
      const role = getRole();
      const nextMode = getModeByRole(role);
      setMode(nextMode);
      if (!nextMode) {
        setOpen(false);
        setInitialized(true);
        return;
      }

      const token = localStorage.getItem('accessToken');
      if (!token) {
        setOpen(false);
        setInitialized(true);
        return;
      }

      const seen = localStorage.getItem(getSeenKey(nextMode)) === '1';
      if (!seen) {
        setIndex(0);
        setOpen(true);
      }
      setInitialized(true);
    };

    init();
    window.addEventListener('auth-changed', init);
    return () => window.removeEventListener('auth-changed', init);
  }, [getModeByRole, getRole, getSeenKey]);

  const closeAndMarkSeen = useCallback(() => {
    if (mode) localStorage.setItem(getSeenKey(mode), '1');
    setOpen(false);
  }, [getSeenKey, mode]);

  const goPrev = useCallback(() => {
    if (!steps.length) return;
    const prev = Math.max(0, index - 1);
    setIndex(prev);
    if (steps[prev] && pathname !== steps[prev].route) router.push(steps[prev].route);
  }, [index, pathname, router, steps]);

  const goNext = useCallback(() => {
    if (!current || !steps.length) return;
    if (pathname !== current.route) {
      router.push(current.route);
      return;
    }

    const last = index >= steps.length - 1;
    if (last) {
      closeAndMarkSeen();
      return;
    }

    const next = index + 1;
    setIndex(next);
    if (steps[next] && pathname !== steps[next].route) router.push(steps[next].route);
  }, [closeAndMarkSeen, current, index, pathname, router, steps]);

  const canShowRestart = mode !== null && pathname !== '/login' && pathname !== '/' && !pathname.startsWith('/platform');

  return (
    <TourContext.Provider value={{ startTour, canShowRestart }}>
      {children}

      {initialized && open && current && (
        <div className='fixed inset-0 z-[120] flex items-end justify-center bg-black/60 p-4 md:items-center'>
          <div className='w-full max-w-md rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--surface)] p-4 shadow-[var(--shadow-md)]'>
            <p className='text-xs text-[var(--muted)]'>
              {t('tour.step')} {index + 1}/{steps.length}
            </p>
            <h3 className='mt-1 text-base font-semibold text-[var(--text)]'>{t(current.titleKey)}</h3>
            <p className='mt-2 text-sm text-[var(--muted)]'>{t(current.descKey)}</p>
            {pathname !== current.route && (
              <p className='mt-2 text-xs text-[var(--warning)]'>
                {t('tour.currentPage')} {pathname}
              </p>
            )}
            <div className='mt-4 flex flex-wrap gap-2'>
              <Button variant='ghost' onClick={closeAndMarkSeen}>{t('tour.skip')}</Button>
              <Button variant='secondary' onClick={goPrev} disabled={index === 0}>{t('tour.prev')}</Button>
              <Button onClick={goNext}>
                {pathname !== current.route ? t('tour.openPage') : index === steps.length - 1 ? t('tour.finish') : t('tour.next')}
              </Button>
            </div>
          </div>
        </div>
      )}
    </TourContext.Provider>
  );
}

export function useTour() {
  const ctx = useContext(TourContext);
  if (!ctx) throw new Error('useTour must be used inside TourProvider');
  return ctx;
}

