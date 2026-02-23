'use client';

import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { usePathname } from 'next/navigation';
import { API_URL } from '@/lib/api';
import { Locale, messages } from '@/lib/i18n/messages';

type I18nContextType = {
  locale: Locale;
  t: (key: string) => string;
  setPlatformLocale: (locale: Locale) => void;
  setTenantPreviewLocale: (locale: Locale) => void;
};

const I18nContext = createContext<I18nContextType | null>(null);

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const [locale, setLocale] = useState<Locale>('it');

  useEffect(() => {
    let active = true;
    const applyBranding = (primary?: string, secondary?: string) => {
      const root = document.documentElement;
      const p = normalizeHexColor(primary) ?? '#22c55e';
      const s = normalizeHexColor(secondary) ?? '#f97316';
      root.style.setProperty('--primary', p);
      root.style.setProperty('--accent', s);
      root.style.setProperty('--ring', p);
      window.dispatchEvent(new Event('tenant-theme-changed'));
    };

    const resetBranding = () => applyBranding('#22c55e', '#f97316');

    const applyLocale = async () => {
      const isPlatform = pathname.startsWith('/platform');
      if (isPlatform) {
        const pref = (localStorage.getItem('platformUiLocale') || 'it') as Locale;
        if (active) setLocale(pref === 'es' ? 'es' : 'it');
        resetBranding();
        return;
      }

      const token = localStorage.getItem('accessToken');
      if (!token) {
        if (active) setLocale('it');
        resetBranding();
        return;
      }

      try {
        const res = await fetch(`${API_URL}/tenant/settings`, {
          headers: { Authorization: `Bearer ${token}` }
        });
        if (!res.ok) throw new Error('tenant settings not available');
        const data = await res.json();
        const next = data?.defaultLocale === 'es' ? 'es' : 'it';
        if (active) setLocale(next);
        applyBranding(data?.primaryColor, data?.secondaryColor);
      } catch {
        if (active) setLocale('it');
        resetBranding();
      }
    };

    applyLocale();
    const onAuthChanged = () => applyLocale();
    window.addEventListener('auth-changed', onAuthChanged);
    return () => {
      active = false;
      window.removeEventListener('auth-changed', onAuthChanged);
    };
  }, [pathname]);

  const value = useMemo<I18nContextType>(
    () => ({
      locale,
      t: (key: string) => messages[locale][key] ?? key,
      setPlatformLocale: (next: Locale) => {
        localStorage.setItem('platformUiLocale', next);
        setLocale(next);
      },
      setTenantPreviewLocale: (next: Locale) => {
        setLocale(next);
      }
    }),
    [locale]
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

function normalizeHexColor(value?: string) {
  if (!value) return null;
  const v = value.trim();
  return /^#([0-9a-fA-F]{6})$/.test(v) ? v : null;
}

export function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error('useI18n must be used inside I18nProvider');
  return ctx;
}
