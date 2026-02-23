'use client';

import { useState } from 'react';

export function HelpHint({ text }: { text: string }) {
  const [open, setOpen] = useState(false);

  return (
    <span className='relative inline-flex items-center'>
      <button
        type='button'
        aria-label='Help'
        className='inline-flex h-5 w-5 items-center justify-center rounded-full border border-[var(--border)] bg-[var(--surface-3)] text-[11px] font-bold text-[var(--muted)] hover:text-[var(--text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--ring)]'
        onMouseEnter={() => setOpen(true)}
        onMouseLeave={() => setOpen(false)}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onClick={() => setOpen((v) => !v)}
      >
        ?
      </button>
      {open && (
        <span className='absolute left-1/2 top-6 z-30 w-56 -translate-x-1/2 rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface)] p-2 text-xs text-[var(--text)] shadow-[var(--shadow-md)]'>
          {text}
        </span>
      )}
    </span>
  );
}

