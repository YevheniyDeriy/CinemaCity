export type Session = { accessToken: string; accessTokenExpiresAt: string; user: { id: string; username: string; email: string; role: string | number; avatarUrl?: string | null } };
export type FieldErrors = Record<string, string[]>;
export class ApiError extends Error {
  constructor(public status: number, message: string, public fields: FieldErrors = {}, public retryAt = 0) { super(message); this.name = 'ApiError'; }
}
export const apiBaseUrl = (import.meta.env.VITE_API_URL ?? (typeof window === 'undefined' ? 'http://localhost:5000' : `${window.location.protocol}//${window.location.hostname}:5000`)).replace(/\/$/, '');

export class ApiClient {
  private session: Session | null = null;
  private listeners = new Set<(session: Session | null) => void>();
  private refreshFlight: Promise<Session> | null = null;
  private cooldowns = new Map<string, number>();
  constructor(private base: string, private transport: typeof fetch = (...args) => fetch(...args), private timeoutMs = 10000) {}
  getSession = () => this.session;
  subscribeSession = (listener: (session: Session | null) => void) => { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; };
  setSession(session: Session | null) {
    if (this.session === session) return;
    this.session = session;
    this.listeners.forEach(listener => listener(session));
  }
  private scope(path: string) { return /\/auth\/login\/?$/.test(path.split('?')[0]) ? 'login' : 'requests'; }
  remaining(path: string) { return Math.max(0, Math.ceil(((this.cooldowns.get(this.scope(path)) ?? 0) - Date.now()) / 1000)); }
  async restoreSession(): Promise<Session> {
    if (!this.refreshFlight) {
      this.refreshFlight = this.request<Session>('/api/auth/refresh', { method: 'POST', body: '{}' }, false)
        .then(session => { this.setSession(session); return session; })
        .catch(error => { if (error instanceof ApiError && error.status === 401) this.setSession(null); throw error; })
        .finally(() => { this.refreshFlight = null; });
    }
    return this.refreshFlight;
  }
  async login(email: string, password: string) {
    const session = await this.request<Session>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }, false);
    this.setSession(session); return session;
  }
  async register(username: string, email: string, password: string, confirmPassword: string) {
    const session = await this.request<Session>('/api/auth/register', { method: 'POST', body: JSON.stringify({ username, email, password, confirmPassword }) }, false);
    this.setSession(session); return session;
  }
  async logout() { await this.request('/api/auth/logout', { method: 'POST', body: '{}' }); this.setSession(null); }
  async request<T>(path: string, init: RequestInit = {}, allowRefresh = true): Promise<T> {
    const response = await this.rawFetch(path, init, allowRefresh);
    return response.status === 204 ? undefined as T : await response.json() as T;
  }
  async rawFetch(path: string, init: RequestInit = {}, allowRefresh = true): Promise<Response> {
    // Only configured gateway URLs are accepted, including compatibility calls in existing screens.
    const url = path.startsWith('/') ? this.base + path : path;
    const expected = new URL(this.base || '/', typeof location === 'undefined' ? 'http://localhost' : location.origin);
    const parsed = new URL(url, expected);
    if (parsed.origin !== expected.origin || !/^\/(api|uploads)(\/|$)/.test(parsed.pathname)) throw new ApiError(0, 'Некоректна адреса API.');
    const route = parsed.pathname;
    const remaining = this.remaining(route);
    if (remaining) throw new ApiError(429, `Забагато запитів. Повторіть через ${remaining} с.`, {}, Date.now() + remaining * 1000);
    const headers = new Headers(init.headers);
    const authRoute = /\/auth\/(login|register|refresh|google)(\/|$)/.test(route);
    if (!authRoute && this.session) headers.set('Authorization', `Bearer ${this.session.accessToken}`);
    if (!(init.body instanceof FormData) && init.body != null) headers.set('Content-Type', 'application/json');
    const controller = new AbortController();
    const abort = () => controller.abort();
    if (init.signal?.aborted) throw new DOMException('Aborted', 'AbortError');
    init.signal?.addEventListener('abort', abort, { once: true });
    let timedOut = false;
    const timer = setTimeout(() => { timedOut = true; controller.abort(); }, this.timeoutMs);
    let response: Response;
    try {
      response = await this.transport(url, { ...init, headers, credentials: 'include', signal: controller.signal });
      await response.clone().text(); // The deadline includes reading the body, not only response headers.
    } catch (error) {
      if (init.signal?.aborted) throw new DOMException('Aborted', 'AbortError');
      throw new ApiError(0, timedOut ? 'Сервер не відповів вчасно. Спробуйте ще раз.' : 'Немає зв’язку із сервером. Перевірте підключення та повторіть.');
    } finally { clearTimeout(timer); init.signal?.removeEventListener('abort', abort); }
    if (response.status === 401 && allowRefresh && !authRoute) {
      await this.restoreSession();
      const retried = await this.rawFetch(path, init, false);
      return retried;
    }
    if (!response.ok) {
      if (response.status === 401 && !authRoute) this.setSession(null);
      let data: { errors?: FieldErrors; message?: string } = {};
      try { data = await response.json(); } catch { /* A proxy may return a non-JSON error page. */ }
      const fields: FieldErrors = {};
      for (const [key, values] of Object.entries(data.errors ?? {})) fields[key.split('.').pop()!.toLowerCase()] = Array.isArray(values) ? values : [String(values)];
      if (data.message === 'Score must be between 1 and 10.') fields.score = ['Оцінка має бути від 1 до 10.'];
      if (data.message === 'Rating must target either a movie or an episode.') fields.movieid = ['Оберіть фільм або епізод.'];
      let retryAt = 0;
      if (response.status === 429) {
        const value = response.headers.get('Retry-After');
        const seconds = value && /^\d+$/.test(value) ? Number(value) : Math.max(1, Math.ceil(((value ? Date.parse(value) : NaN) - Date.now()) / 1000));
        retryAt = Date.now() + (Number.isFinite(seconds) ? Math.max(1, seconds) : 60) * 1000;
        this.cooldowns.set(this.scope(route), retryAt);
      }
      const message = response.status === 401 ? (route.endsWith('/login') ? 'Невірний email або пароль.' : 'Сесію завершено. Увійдіть ще раз.')
        : response.status === 429 ? `Забагато запитів. Повторіть через ${Math.ceil((retryAt - Date.now()) / 1000)} с.`
        : response.status === 400 ? 'Перевірте введені дані.' : response.status === 403 ? 'Недостатньо прав для цієї дії.'
        : response.status === 404 ? 'Запис не знайдено.' : response.status === 409 ? 'Ця операція вже існує або конфліктує зі змінами. Перевірте свої оцінки.'
        : 'Сервіс тимчасово недоступний. Спробуйте ще раз.';
      throw new ApiError(response.status, message, fields, retryAt);
    }
    return response;
  }
}
export const api = new ApiClient(apiBaseUrl);
export const apiFetch: typeof fetch = (input, init) => api.rawFetch(String(input), init);
