import { InputHTMLAttributes } from 'react';
import { cn } from '@/lib/cn';

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  const isPickerType = props.type === 'date' || props.type === 'time' || props.type === 'datetime-local';

  return (
    <input
      {...props}
      onFocus={(e) => {
        props.onFocus?.(e);
        if (!isPickerType) return;
        const target = e.currentTarget as HTMLInputElement & { showPicker?: () => void };
        target.showPicker?.();
      }}
      onClick={(e) => {
        props.onClick?.(e);
        if (!isPickerType) return;
        const target = e.currentTarget as HTMLInputElement & { showPicker?: () => void };
        target.showPicker?.();
      }}
      className={cn(
        'min-h-11 w-full rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] px-3 py-2 text-sm text-[var(--text)] placeholder:text-[var(--muted)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--ring)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--bg)]',
        props.className
      )}
    />
  );
}
