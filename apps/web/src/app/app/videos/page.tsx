'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { useToast } from '@/components/ui/toast';
import { EmptyState } from '@/components/ui/empty-state';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';

type Video = {
  id: string;
  title: string;
  category: string;
  videoUrl: string;
  thumbnailUrl?: string | null;
  description: string;
  provider: 'YOUTUBE' | 'VIMEO';
  durationSeconds: number;
  watchedSeconds: number;
  progressPercent: number;
  completed: boolean;
};

export default function MemberVideosPage() {
  const router = useRouter();
  const { push } = useToast();
  const [authorized, setAuthorized] = useState(false);
  const [loading, setLoading] = useState(true);
  const [items, setItems] = useState<Video[]>([]);
  const [category, setCategory] = useState('ALL');
  const [openId, setOpenId] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    const path = category === 'ALL' ? '/app/videos' : `/app/videos?category=${encodeURIComponent(category)}`;
    const data = await apiFetch(path);
    setItems(data);
    setLoading(false);
  }

  useEffect(() => {
    const token = localStorage.getItem('accessToken');
    const role = localStorage.getItem('role');
    if (!token) {
      push('Effettua login membro', 'warning');
      router.replace('/app');
      return;
    }
    if (role !== 'MEMBER') {
      push('Questa pagina e solo per membri', 'warning');
      router.replace('/dashboard');
      return;
    }
    setAuthorized(true);
  }, [push, router]);

  useEffect(() => {
    if (!authorized) return;
    load().catch((e) => push(String(e), 'danger'));
  }, [authorized, category, push]);

  const categories = useMemo(() => ['ALL', ...Array.from(new Set(items.map((x) => x.category))).sort()], [items]);

  async function trackProgress(video: Video, completed = false) {
    const seconds = completed ? video.durationSeconds || video.watchedSeconds : Math.min((video.watchedSeconds || 0) + 60, video.durationSeconds || 3600);
    await apiFetch(`/videos/${video.id}/progress`, {
      method: 'POST',
      body: JSON.stringify({ watchedSeconds: seconds, completed })
    });
    push(completed ? 'Allenamento completato' : 'Progresso aggiornato', completed ? 'success' : 'info');
    await load();
  }

  return (
    <div className='space-y-4 pb-20'>
      <Card>
        <CardTitle>Video Training</CardTitle>
        <p className='text-sm text-[var(--muted)]'>Scegli una categoria e allenati quando vuoi.</p>
      </Card>

      <Card>
        <div className='flex items-center justify-between gap-2'>
          <span className='text-sm font-medium text-[var(--text)]'>Categoria</span>
          <Select className='max-w-52' value={category} onChange={(e) => setCategory(e.target.value)}>
            {categories.map((c) => (
              <option key={c} value={c}>
                {c === 'ALL' ? 'Tutte' : c}
              </option>
            ))}
          </Select>
        </div>
      </Card>

      {loading && (
        <Card>
          <div className='space-y-2'>
            <Skeleton className='h-24 w-full' />
            <Skeleton className='h-24 w-full' />
          </div>
        </Card>
      )}

      {!loading && items.length === 0 ? (
        <EmptyState title='Nessun video disponibile' description='Contatta la reception per nuovi contenuti training.' />
      ) : (
        <div className='space-y-3'>
          {items.map((v) => {
            const embed = toEmbedUrl(v.videoUrl, v.provider);
            const open = openId === v.id;
            return (
              <Card key={v.id} className='bg-[var(--surface-2)]'>
                <div className='flex flex-wrap items-start justify-between gap-2'>
                  <div>
                    <p className='font-semibold'>{v.title}</p>
                    <p className='text-xs text-[var(--muted)]'>
                      {v.category} · {Math.floor((v.durationSeconds || 0) / 60)} min
                    </p>
                  </div>
                  <Badge tone={v.completed ? 'success' : 'info'}>{v.completed ? 'Completato' : 'In corso'}</Badge>
                </div>
                <p className='mt-2 text-sm text-[var(--muted)]'>{v.description || 'Workout guidato.'}</p>
                <div className='mt-3'>
                  <Progress value={v.progressPercent || 0} max={100} />
                </div>
                <div className='mt-3 flex flex-wrap gap-2'>
                  <Button variant='secondary' onClick={() => setOpenId((prev) => (prev === v.id ? null : v.id))}>
                    {open ? 'Chiudi player' : 'Apri player'}
                  </Button>
                  <Button variant='ghost' onClick={() => trackProgress(v, false)}>
                    +1 min
                  </Button>
                  <Button onClick={() => trackProgress(v, true)}>Segna completato</Button>
                </div>
                {open && (
                  <div className='mt-3 overflow-hidden rounded-[var(--radius-md)] border border-[var(--border)]'>
                    <iframe
                      src={embed}
                      title={v.title}
                      className='h-56 w-full md:h-80'
                      allow='autoplay; fullscreen; picture-in-picture'
                      allowFullScreen
                    />
                  </div>
                )}
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}

function toEmbedUrl(videoUrl: string, provider: 'YOUTUBE' | 'VIMEO') {
  if (provider === 'VIMEO') {
    const match = videoUrl.match(/vimeo\.com\/(\d+)/i);
    const id = match?.[1];
    return id ? `https://player.vimeo.com/video/${id}` : videoUrl;
  }

  const ytId =
    videoUrl.match(/[?&]v=([^&]+)/i)?.[1] ||
    videoUrl.match(/youtu\.be\/([^?&]+)/i)?.[1] ||
    videoUrl.match(/youtube\.com\/embed\/([^?&]+)/i)?.[1];
  return ytId ? `https://www.youtube.com/embed/${ytId}` : videoUrl;
}
