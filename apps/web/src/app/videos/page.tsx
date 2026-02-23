'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/components/ui/toast';
import { EmptyState } from '@/components/ui/empty-state';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Skeleton } from '@/components/ui/skeleton';

type Video = {
  id: string;
  title: string;
  category: string;
  videoUrl: string;
  thumbnailUrl?: string | null;
  description: string;
  provider: 'YOUTUBE' | 'VIMEO';
  durationSeconds: number;
  isPublished: boolean;
  updatedAtUtc: string;
};

type FormModel = {
  title: string;
  category: string;
  videoUrl: string;
  thumbnailUrl: string;
  description: string;
  provider: 'YOUTUBE' | 'VIMEO';
  durationSeconds: string;
  isPublished: boolean;
};

const INITIAL: FormModel = {
  title: '',
  category: '',
  videoUrl: '',
  thumbnailUrl: '',
  description: '',
  provider: 'YOUTUBE',
  durationSeconds: '600',
  isPublished: true
};

export default function VideosStaffPage() {
  const { push } = useToast();
  const [items, setItems] = useState<Video[]>([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState<FormModel>(INITIAL);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [filter, setFilter] = useState('ALL');

  async function load() {
    setLoading(true);
    const data = await apiFetch('/videos');
    setItems(data);
    setLoading(false);
  }

  useEffect(() => {
    load().catch((e) => push(String(e), 'danger'));
  }, [push]);

  const categories = useMemo(() => ['ALL', ...Array.from(new Set(items.map((x) => x.category))).sort()], [items]);
  const filtered = useMemo(() => items.filter((x) => filter === 'ALL' || x.category === filter), [items, filter]);

  function onChange<K extends keyof FormModel>(key: K, value: FormModel[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  function edit(video: Video) {
    setEditingId(video.id);
    setForm({
      title: video.title,
      category: video.category,
      videoUrl: video.videoUrl,
      thumbnailUrl: video.thumbnailUrl || '',
      description: video.description || '',
      provider: video.provider,
      durationSeconds: String(video.durationSeconds || 0),
      isPublished: video.isPublished
    });
  }

  async function submit(e: FormEvent) {
    e.preventDefault();
    const payload = {
      title: form.title,
      category: form.category,
      videoUrl: form.videoUrl,
      thumbnailUrl: form.thumbnailUrl || null,
      description: form.description,
      provider: form.provider,
      durationSeconds: Number(form.durationSeconds || 0),
      isPublished: form.isPublished
    };

    if (editingId) {
      await apiFetch(`/videos/${editingId}`, { method: 'PUT', body: JSON.stringify(payload) });
      push('Video aggiornato', 'success');
    } else {
      await apiFetch('/videos', { method: 'POST', body: JSON.stringify(payload) });
      push('Video creato', 'success');
    }

    setForm(INITIAL);
    setEditingId(null);
    load();
  }

  async function remove() {
    if (!deleteId) return;
    await apiFetch(`/videos/${deleteId}`, { method: 'DELETE' });
    push('Video eliminato', 'success');
    setDeleteId(null);
    load();
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Video Training</CardTitle>
        <p className='text-sm text-[var(--muted)]'>Crea e gestisci contenuti allenamento per i membri della tua palestra.</p>
      </Card>

      <Card>
        <CardTitle>{editingId ? 'Modifica video' : 'Nuovo video'}</CardTitle>
        <form onSubmit={submit} className='mt-3 space-y-2'>
          <div className='grid gap-2 md:grid-cols-2'>
            <Input placeholder='Titolo video' value={form.title} onChange={(e) => onChange('title', e.target.value)} required />
            <Input placeholder='Categoria (es. Mobility)' value={form.category} onChange={(e) => onChange('category', e.target.value)} required />
            <Input placeholder='URL video (YouTube/Vimeo)' value={form.videoUrl} onChange={(e) => onChange('videoUrl', e.target.value)} required />
            <Input placeholder='Thumbnail URL (opzionale)' value={form.thumbnailUrl} onChange={(e) => onChange('thumbnailUrl', e.target.value)} />
            <Select value={form.provider} onChange={(e) => onChange('provider', e.target.value as 'YOUTUBE' | 'VIMEO')}>
              <option value='YOUTUBE'>YouTube</option>
              <option value='VIMEO'>Vimeo</option>
            </Select>
            <Input placeholder='Durata secondi' value={form.durationSeconds} onChange={(e) => onChange('durationSeconds', e.target.value)} />
          </div>
          <Textarea placeholder='Descrizione breve' value={form.description} onChange={(e) => onChange('description', e.target.value)} />
          <label className='flex items-center gap-2 text-sm text-[var(--text)]'>
            <input type='checkbox' checked={form.isPublished} onChange={(e) => onChange('isPublished', e.target.checked)} />
            Pubblicato
          </label>
          <div className='flex flex-wrap gap-2'>
            <Button>{editingId ? 'Salva modifiche' : 'Crea video'}</Button>
            {editingId && (
              <Button
                type='button'
                variant='secondary'
                onClick={() => {
                  setEditingId(null);
                  setForm(INITIAL);
                }}
              >
                Annulla modifica
              </Button>
            )}
          </div>
        </form>
      </Card>

      <Card>
        <div className='mb-3 flex flex-wrap items-center justify-between gap-2'>
          <CardTitle>Catalogo video</CardTitle>
          <Select className='min-w-44' value={filter} onChange={(e) => setFilter(e.target.value)}>
            {categories.map((c) => (
              <option key={c} value={c}>
                {c === 'ALL' ? 'Tutte le categorie' : c}
              </option>
            ))}
          </Select>
        </div>

        {loading && (
          <div className='space-y-2'>
            <Skeleton className='h-16 w-full' />
            <Skeleton className='h-16 w-full' />
          </div>
        )}

        {!loading && filtered.length === 0 ? (
          <EmptyState title='Nessun video disponibile' description='Crea il primo video training.' />
        ) : (
          <div className='space-y-2'>
            {filtered.map((v) => (
              <div key={v.id} className='rounded-[var(--radius-md)] border border-[var(--border)] bg-[var(--surface-2)] p-3'>
                <div className='flex flex-wrap items-center justify-between gap-2'>
                  <div>
                    <p className='font-semibold'>{v.title}</p>
                    <p className='text-xs text-[var(--muted)]'>
                      {v.category} · {v.provider} · {Math.floor(v.durationSeconds / 60)} min
                    </p>
                  </div>
                  <div className='flex items-center gap-2'>
                    <Badge tone={v.isPublished ? 'success' : 'warning'}>{v.isPublished ? 'Published' : 'Draft'}</Badge>
                    <Button variant='secondary' onClick={() => edit(v)}>
                      Modifica
                    </Button>
                    <Button variant='danger' onClick={() => setDeleteId(v.id)}>
                      Elimina
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <ConfirmDialog
        open={Boolean(deleteId)}
        title='Eliminare video?'
        description='Questa azione non è reversibile.'
        onClose={() => setDeleteId(null)}
        onConfirm={remove}
      />
    </div>
  );
}
