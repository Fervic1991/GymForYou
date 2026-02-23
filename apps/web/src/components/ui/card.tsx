import { PropsWithChildren } from 'react';
import { cn } from '@/lib/cn';

export function Card({ children, className }: PropsWithChildren<{ className?: string }>) {
  return <section className={cn('rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--surface)] p-4 shadow-[var(--shadow-sm)]', className)}>{children}</section>;
}

export function CardTitle({ children, className }: PropsWithChildren<{ className?: string }>) {
  return <h2 className={cn('text-base font-semibold tracking-tight text-[var(--text)]', className)}>{children}</h2>;
}
