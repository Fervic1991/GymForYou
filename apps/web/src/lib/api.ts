export const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

export type LoginPayload = {
  tenantId?: string;
  tenantSlug?: string;
  email: string;
  password: string;
  role: 'OWNER' | 'MANAGER' | 'TRAINER' | 'MEMBER' | 'PLATFORM_ADMIN';
};

export type ResolveJoinResponse = {
  tenantName: string;
  defaultLocale: 'it' | 'es';
  status: string;
};

export async function apiFetch(path: string, options: RequestInit = {}) {
  const token = typeof window !== 'undefined' ? localStorage.getItem('accessToken') : null;
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string> || {}),
  };
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${API_URL}${path}`, { ...options, headers });
  if (!res.ok) {
    const raw = await res.text();
    let errorMessage = raw || `HTTP ${res.status}`;
    try {
      const parsed = raw ? JSON.parse(raw) : null;
      if (parsed?.title) errorMessage = parsed.title;
      if (res.status === 403 && parsed?.title === 'Tenant suspended' && typeof window !== 'undefined') {
        window.dispatchEvent(new Event('tenant-suspended'));
      }
    } catch {
      if (res.status === 403 && raw.includes('Tenant suspended') && typeof window !== 'undefined') {
        window.dispatchEvent(new Event('tenant-suspended'));
      }
    }
    throw new Error(errorMessage);
  }
  if (res.status === 204) return null;
  return res.json();
}

export async function login(payload: LoginPayload) {
  const data = await apiFetch('/auth/login', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
  if (typeof window !== 'undefined') {
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    localStorage.setItem('tenantId', data.tenantId);
    localStorage.setItem('role', data.role);
    localStorage.setItem('userId', data.userId);
    window.dispatchEvent(new Event('auth-changed'));
  }
  return data;
}

export async function platformLogin(payload: { email: string; password: string }) {
  const data = await apiFetch('/auth/platform/login', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
  if (typeof window !== 'undefined') {
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.removeItem('refreshToken');
    localStorage.setItem('tenantId', '');
    localStorage.setItem('role', data.role);
    localStorage.setItem('userId', 'platform-admin');
    window.dispatchEvent(new Event('auth-changed'));
  }
  return data;
}

export async function resolveJoin(code: string): Promise<ResolveJoinResponse> {
  const res = await fetch(`${API_URL}/join/${encodeURIComponent(code)}`);
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

export async function registerMember(payload: {
  joinCode: string;
  fullName: string;
  email: string;
  phone?: string;
  password: string;
}) {
  const data = await apiFetch('/auth/register-member', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
  if (typeof window !== 'undefined') {
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    localStorage.setItem('tenantId', data.tenantId);
    localStorage.setItem('role', data.role);
    localStorage.setItem('userId', data.userId);
    window.dispatchEvent(new Event('auth-changed'));
  }
  return data;
}
