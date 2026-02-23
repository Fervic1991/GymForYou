import { cn } from '@/lib/cn';

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('animate-pulse rounded bg-[color-mix(in_srgb,var(--muted)_24%,transparent)]', className)} />;
}
