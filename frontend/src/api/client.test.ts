import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError, Session } from './client';
const session = (token = 'old'): Session => ({ accessToken: token, accessTokenExpiresAt: '2099-01-01', user: { id: 'user', username: 'owner', email: 'test@example.test', role: 'User' } });
const json = (data: unknown, status = 200, headers: Record<string, string> = {}) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json', ...headers } });
afterEach(() => vi.useRealTimers());
describe('gateway API boundary', () => {
  it('refreshes once for concurrent 401s, repeats with the new token and preserves Idempotency-Key', async () => {
    let refreshes = 0; const keys: string[] = [];
    const transport = vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => {
      if (String(url).endsWith('/refresh')) { refreshes++; await new Promise(resolve => setTimeout(resolve, 5)); return json(session('new')); }
      const headers = new Headers(init?.headers); keys.push(headers.get('Idempotency-Key')!);
      return headers.get('Authorization') === 'Bearer new' ? json({ saved: true }) : json({}, 401);
    });
    const api = new ApiClient('http://localhost:5000', transport as typeof fetch); api.setSession(session());
    const init = { method: 'POST', body: '{}', headers: { 'Idempotency-Key': 'one-user-attempt' } };
    const results = await Promise.all([api.request('/api/ratings', init), api.request('/api/ratings', init)]);
    expect(results).toEqual([{ saved: true }, { saved: true }]); expect(refreshes).toBe(1);
    expect(keys).toEqual(Array(4).fill('one-user-attempt'));
  });
  it('does not loop when refresh is rejected and clears the session', async () => {
    const transport = vi.fn(async () => json({}, 401));
    const api = new ApiClient('http://localhost:5000', transport as typeof fetch); api.setSession(session());
    await expect(api.request('/api/ratings')).rejects.toMatchObject({ status: 401 });
    expect(transport).toHaveBeenCalledTimes(2); expect(api.getSession()).toBeNull();
  });
  it('blocks network calls until numeric Retry-After expires', async () => {
    vi.useFakeTimers(); const transport = vi.fn(async () => json({}, 429, { 'Retry-After': '3' }));
    const api = new ApiClient('http://localhost:5000', transport as typeof fetch);
    await expect(api.request('/api/auth/login')).rejects.toMatchObject({ status: 429 });
    await expect(api.request('/api/auth/login')).rejects.toMatchObject({ status: 429 });
    expect(transport).toHaveBeenCalledTimes(1); expect(api.remaining('/api/auth/login')).toBe(3);
    await vi.advanceTimersByTimeAsync(3001); expect(api.remaining('/api/auth/login')).toBe(0);
  });
  it('understands HTTP-date Retry-After', async () => {
    const date = new Date(Date.now() + 5000).toUTCString();
    const api = new ApiClient('http://localhost:5000', (async () => json({}, 429, { 'Retry-After': date })) as typeof fetch);
    await expect(api.request('/api/ratings')).rejects.toMatchObject({ status: 429 });
    expect(api.remaining('/api/ratings')).toBeGreaterThanOrEqual(3);
  });
  it('places server validation errors by field and hides stack traces', async () => {
    const transport = vi.fn().mockResolvedValueOnce(json({ errors: { Score: ['Оберіть оцінку.'] } }, 400)).mockResolvedValueOnce(json({ title: 'InternalException', detail: 'SECRET STACK TRACE' }, 500));
    const api = new ApiClient('http://localhost:5000', transport);
    await expect(api.request('/api/ratings')).rejects.toMatchObject({ fields: { score: ['Оберіть оцінку.'] } });
    await expect(api.request('/api/ratings')).rejects.toThrow('Сервіс тимчасово недоступний. Спробуйте ще раз.');
  });
  it('rejects direct service addresses without making a request', async () => {
    const transport = vi.fn(); const api = new ApiClient('http://localhost:5000', transport);
    await expect(api.request('http://localhost:5002/api/movies')).rejects.toBeInstanceOf(ApiError); expect(transport).not.toHaveBeenCalled();
  });
  it('finishes stalled requests with a safe timeout error', async () => {
    const transport = ((_url: unknown, init?: RequestInit) => new Promise<Response>((_resolve, reject) => init?.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError'))))) as typeof fetch;
    const api = new ApiClient('http://localhost:5000', transport, 15);
    await expect(api.request('/api/ratings')).rejects.toThrow('Сервер не відповів вчасно. Спробуйте ще раз.');
  });
});
