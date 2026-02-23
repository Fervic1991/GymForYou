'use client';

import { createContext, useContext, useMemo } from 'react';
import { Toaster, sileo } from 'sileo';

type ToastTone = 'success' | 'warning' | 'danger' | 'info';

type ToastContextType = {
  push: (message: string, tone?: ToastTone) => void;
};

const ToastContext = createContext<ToastContextType | null>(null);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const api = useMemo<ToastContextType>(() => ({
    push(message, tone = 'info') {
      const fillByTone: Record<ToastTone, string> = {
        success: '#14532d',
        warning: '#78350f',
        danger: '#7f1d1d',
        info: '#0c4a6e'
      };
      const type = tone === 'danger' ? 'error' : tone;
      sileo.show({
        title: message,
        type,
        duration: 2600,
        fill: fillByTone[tone],
        styles: {
          title: '!text-white'
        }
      });
    }
  }), []);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <Toaster
        position='top-right'
        theme='dark'
        options={{
          fill: '#111827',
          roundness: 12,
          duration: 2600
        }}
      />
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used within ToastProvider');
  return ctx;
}
