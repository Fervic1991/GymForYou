'use client';

import { ButtonHTMLAttributes } from 'react';
import { cn } from '@/lib/cn';

type Variant = 'primary' | 'secondary' | 'success' | 'warning' | 'danger' | 'ghost';

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  block?: boolean;
};

const styles: Record<Variant, string> = {
  primary: 'border border-transparent bg-[var(--primary)] text-black hover:bg-[var(--primary-700)]',
  secondary: 'border border-[var(--border)] bg-[var(--surface-3)] text-[var(--text)] hover:bg-[var(--surface)]',
  success: 'border border-transparent bg-[var(--primary)] text-black hover:bg-[var(--primary-700)]',
  warning: 'border border-transparent bg-[var(--warning)] text-black hover:brightness-95',
  danger: 'border border-transparent bg-[var(--danger)] text-white hover:brightness-95',
  ghost: 'border border-transparent bg-transparent text-[var(--text)] hover:bg-[var(--surface-3)]'
};

export function Button({ className, variant = 'primary', block, ...props }: Props) {
  return (
    <button
      className={cn(
        'inline-flex min-h-11 items-center justify-center rounded-[var(--radius-md)] px-4 py-2 text-sm font-semibold transition duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--ring)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--bg)] disabled:cursor-not-allowed disabled:opacity-50',
        styles[variant],
        block && 'w-full',
        className
      )}
      {...props}
    />
  );
}
