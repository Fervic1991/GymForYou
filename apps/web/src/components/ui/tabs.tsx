import { cn } from '@/lib/cn';

export function Tabs({
  items,
  value,
  onChange
}: {
  items: Array<{ value: string; label: string; count?: number }>;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className='inline-flex rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-1'>
      {items.map((item) => (
        <button
          key={item.value}
          className={cn(
            'min-h-10 rounded-[calc(var(--radius-md)-2px)] px-3 py-1.5 text-sm font-semibold',
            value === item.value ? 'bg-[var(--primary)] text-black' : 'text-[var(--muted)] hover:bg-[var(--surface-3)]'
          )}
          onClick={() => onChange(item.value)}
        >
          {item.label}
          {typeof item.count === 'number' ? ` (${item.count})` : ''}
        </button>
      ))}
    </div>
  );
}
