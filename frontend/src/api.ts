import { API_URL } from './config';
import type { Application, ClassInfo, CreatedHold } from './types';

/** An error from the API (status > 0) or a failure to reach it at all (status 0). */
export class ApiError extends Error {
  constructor(
    public status: number,
    public code?: string,
    message?: string,
  ) {
    super(message ?? `HTTP ${status}`);
    this.name = 'ApiError';
  }
}

// The countdown must follow the SERVER clock (expiresAt is a server time), so remember the
// difference between the server's and this browser's clock from every response.
let clockSkewMs = 0;
export const serverNow = (): number => Date.now() + clockSkewMs;

interface Options {
  studentId?: string | null;
  body?: unknown;
}

async function request<T>(method: string, path: string, opts: Options = {}): Promise<T> {
  const headers: Record<string, string> = {};
  if (opts.studentId) headers['X-Student-Id'] = opts.studentId;
  if (opts.body !== undefined) headers['Content-Type'] = 'application/json';

  let res: Response;
  try {
    res = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
    });
  } catch {
    throw new ApiError(0, 'NETWORK'); // server down, wrong URL, CORS failure, offline...
  }

  const dateHeader = res.headers.get('Date');
  if (dateHeader) {
    const t = Date.parse(dateHeader);
    if (!Number.isNaN(t)) clockSkewMs = t - Date.now();
  }

  if (res.status === 204) return undefined as T;

  let data: { error?: string; message?: string } | null = null;
  try {
    data = await res.json();
  } catch {
    /* non-JSON body (e.g. a proxy error page) */
  }

  if (!res.ok) throw new ApiError(res.status, data?.error, data?.message);
  return data as T;
}

export const api = {
  listClasses: () => request<ClassInfo[]>('GET', '/classes'),
  createClass: (name: string, totalSeats: number) =>
    request<{ id: string; name: string; totalSeats: number }>('POST', '/classes', {
      body: { name, totalSeats },
    }),
  createHold: (classId: string, studentId: string) =>
    request<CreatedHold>('POST', `/classes/${encodeURIComponent(classId)}/holds`, { studentId }),
  getHold: (holdId: string, studentId: string) =>
    request<Application>('GET', `/holds/${encodeURIComponent(holdId)}`, { studentId }),
  confirmHold: (holdId: string, studentId: string) =>
    request<{ holdId: string; status: string }>('POST', `/holds/${encodeURIComponent(holdId)}/confirm`, { studentId }),
  cancelHold: (holdId: string, studentId: string) =>
    request<void>('DELETE', `/holds/${encodeURIComponent(holdId)}`, { studentId }),
  myHolds: (studentId: string) => request<Application[]>('GET', '/me/holds', { studentId }),
};
