export function Progress({ value, max }: { value: number; max: number }) {
  const pct = max <= 0 ? 0 : Math.min(100, Math.round((value / max) * 100));
  const tone = pct >= 100 ? 'bg-[var(--danger)]' : pct >= 80 ? 'bg-[var(--warning)]' : 'bg-[var(--primary)]';

  return (
    <div className='w-full'>
      <div className='h-2.5 w-full rounded-full bg-[var(--surface-3)]'>
        <div className={`h-2 rounded-full ${tone}`} style={{ width: `${pct}%` }} />
      </div>
      <p className='mt-1 text-xs text-[var(--muted)]'>{value}/{max}</p>
    </div>
  );
}
