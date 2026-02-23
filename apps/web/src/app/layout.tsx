import type { Metadata, Viewport } from 'next';
import './globals.css';
import AppShell from '@/components/layout/app-shell';
import { ToastProvider } from '@/components/ui/toast';
import { I18nProvider } from '@/lib/i18n/provider';
import { TourProvider } from '@/lib/tour/provider';

export const metadata: Metadata = {
  title: 'Gym SaaS MVP',
  description: 'Gym management and member app',
  manifest: '/manifest.json',
  appleWebApp: {
    capable: true,
    title: 'Gym SaaS',
    statusBarStyle: 'black-translucent'
  },
  icons: {
    icon: '/icons/icon-192.svg',
    apple: '/icons/icon-192.svg'
  }
};

export const viewport: Viewport = {
  themeColor: '#0B0F1A'
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang='it'>
      <body>
        <I18nProvider>
          <ToastProvider>
            <TourProvider>
              <AppShell>{children}</AppShell>
            </TourProvider>
          </ToastProvider>
        </I18nProvider>
      </body>
    </html>
  );
}
