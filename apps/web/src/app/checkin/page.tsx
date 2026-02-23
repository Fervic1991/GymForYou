'use client';

import { FormEvent, useEffect, useRef, useState } from 'react';
import { apiFetch } from '@/lib/api';
import { Card, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';

type Member = { id: string; fullName: string; email: string };

declare global {
  interface Window { BarcodeDetector?: any; }
}

export default function CheckInPage() {
  const [members, setMembers] = useState<Member[]>([]);
  const [memberId, setMemberId] = useState('');
  const [code, setCode] = useState('');
  const [generated, setGenerated] = useState('');
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const detectorRef = useRef<any>(null);
  const { push } = useToast();

  useEffect(() => {
    apiFetch('/members').then(setMembers).catch((e) => push(String(e), 'danger'));
    return () => streamRef.current?.getTracks().forEach((t) => t.stop());
  }, [push]);

  async function generateQr(e: FormEvent) {
    e.preventDefault();
    const data = await apiFetch(`/members/${memberId}/checkin-qr`, { method: 'POST', body: JSON.stringify({ memberUserId: memberId, rotate: true, expiresInMinutes: 60 }) });
    setGenerated(data.code);
    setCode(data.code);
    push('Token QR generato', 'success');
  }

  async function doCheckIn(useCode?: string) {
    try {
      const data = await apiFetch('/members/checkin/qr', { method: 'POST', body: JSON.stringify({ code: useCode || code, source: 'qr' }) });
      push(`Check-in OK (${data.id})`, 'success');
    } catch (err) {
      push(String(err), 'danger');
    }
  }

  async function startScanner() {
    try {
      if (!window.BarcodeDetector) return push('BarcodeDetector non supportato, usa fallback codice', 'warning');
      detectorRef.current = new window.BarcodeDetector({ formats: ['qr_code'] });
      const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
      streamRef.current = stream;
      if (videoRef.current) videoRef.current.srcObject = stream;
      push('Scanner attivo', 'info');

      const loop = async () => {
        if (!videoRef.current || !detectorRef.current) return;
        try {
          const barcodes = await detectorRef.current.detect(videoRef.current);
          if (barcodes?.length) {
            const value = barcodes[0]?.rawValue;
            if (value) {
              setCode(value);
              await doCheckIn(value);
            }
          }
        } catch {}
        requestAnimationFrame(loop);
      };
      requestAnimationFrame(loop);
    } catch (err) {
      push(String(err), 'danger');
    }
  }

  return (
    <div className='space-y-4'>
      <Card>
        <CardTitle>Check-in rapido</CardTitle>
        <p className='text-sm text-[var(--text-muted)]'>Genera token, scansiona QR dalla camera o inserisci codice manualmente.</p>
      </Card>

      <div className='grid gap-4 lg:grid-cols-2'>
        <Card>
          <CardTitle>Genera token QR</CardTitle>
          <form onSubmit={generateQr} className='mt-3 space-y-2'>
            <Select value={memberId} onChange={(e) => setMemberId(e.target.value)} required>
              <option value=''>Seleziona membro</option>
              {members.map((m) => <option key={m.id} value={m.id}>{m.fullName} ({m.email})</option>)}
            </Select>
            <Button block>Genera token</Button>
          </form>
          {generated && <pre className='mt-3 rounded-[var(--radius-md)] bg-slate-100 p-2 text-xs'>{generated}</pre>}
        </Card>

        <Card>
          <CardTitle>Scanner + fallback</CardTitle>
          <video ref={videoRef} autoPlay playsInline className='mt-3 h-56 w-full rounded-[var(--radius-md)] border bg-black' />
          <div className='mt-3 flex flex-wrap gap-2'>
            <Button onClick={startScanner}>Avvia scanner</Button>
            <Button variant='success' onClick={() => doCheckIn()}>Check-in da codice</Button>
          </div>
          <Input className='mt-2' placeholder='Inserisci codice QR' value={code} onChange={(e) => setCode(e.target.value)} />
        </Card>
      </div>
    </div>
  );
}
