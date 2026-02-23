'use client';

import { Button } from '@/components/ui/button';

export function ConfirmDialog({
  open,
  title,
  description,
  confirmText = 'Conferma',
  cancelText = 'Annulla',
  onConfirm,
  onClose
}: {
  open: boolean;
  title: string;
  description: string;
  confirmText?: string;
  cancelText?: string;
  onConfirm: () => void;
  onClose: () => void;
}) {
  if (!open) return null;

  return (
    <div className='fixed inset-0 z-50 flex items-center justify-center bg-black/55 p-4 backdrop-blur-sm'>
      <div className='w-full max-w-md rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow-md)]'>
        <h3 className='text-lg font-semibold text-[var(--text)]'>{title}</h3>
        <p className='mt-1 text-sm text-[var(--muted)]'>{description}</p>
        <div className='mt-4 flex justify-end gap-2'>
          <Button variant='secondary' onClick={onClose}>{cancelText}</Button>
          <Button variant='danger' onClick={onConfirm}>{confirmText}</Button>
        </div>
      </div>
    </div>
  );
}
