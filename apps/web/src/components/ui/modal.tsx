import { PropsWithChildren } from 'react';
import { cn } from '@/lib/cn';

export function Modal({
  open,
  title,
  onClose,
  children,
  className
}: PropsWithChildren<{ open: boolean; title: string; onClose: () => void; className?: string }>) {
  if (!open) return null;
  return (
    <div className='fixed inset-0 z-50 flex items-center justify-center bg-black/55 p-4 backdrop-blur-sm'>
      <div className={cn('w-full max-w-2xl rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow-md)]', className)}>
        <div className='mb-4 flex items-center justify-between'>
          <h3 className='text-lg font-semibold text-[var(--text)]'>{title}</h3>
          <button className='rounded p-1 text-[var(--muted)] hover:bg-[var(--surface-3)]' onClick={onClose} aria-label='Chiudi'>
            ✕
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
