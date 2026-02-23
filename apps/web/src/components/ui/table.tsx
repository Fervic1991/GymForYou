import { PropsWithChildren } from 'react';
import { cn } from '@/lib/cn';

export function Table({ children, className }: PropsWithChildren<{ className?: string }>) {
  return (
    <div className='overflow-x-auto'>
      <table className={cn('w-full min-w-[720px] text-sm', className)}>{children}</table>
    </div>
  );
}

export function THead({ children }: PropsWithChildren) {
  return <thead className='sticky top-0 z-10 bg-[var(--surface)]'>{children}</thead>;
}

export function TH({ children, className }: PropsWithChildren<{ className?: string }>) {
  return <th className={cn('border-b border-[var(--border)] px-3 py-3 text-left text-xs font-semibold uppercase tracking-wide text-[var(--muted)]', className)}>{children}</th>;
}

export function TD({ children, className }: PropsWithChildren<{ className?: string }>) {
  return <td className={cn('border-b border-[var(--border)] px-3 py-3 align-top text-[var(--text)]', className)}>{children}</td>;
}

export function TR({ children, className }: PropsWithChildren<{ className?: string }>) {
  return <tr className={cn('transition hover:bg-[var(--surface-2)]', className)}>{children}</tr>;
}
