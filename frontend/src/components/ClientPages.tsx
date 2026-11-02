import React, { FormEvent, useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { api, ApiError, FieldErrors } from '../api/client';
import { PasswordField } from './PasswordField';
import './client-pages.css';

type Card = { id: string; title: string; releaseYear: number; posterUrl: string | null };
type MoviePage = { items: Card[]; totalCount: number; page: number; pageSize: number };
type Screen = { movie: Card; myState: { myScore: number | null; watchlistStatus: string | null; lists: { id: string; name: string }[] } | null; partial: boolean; communityStatus: string };
type RatingPage = { items: Rating[]; page: number; pageSize: number; totalCount: number };
type Rating = { id: string; movieId: string | null; episodeId: string | null; title: string; score: number; status: string; createdAt: string };
function useCooldown(path: string) {
  const [seconds, setSeconds] = useState(() => api.remaining(path));
  useEffect(() => { const id = window.setInterval(() => setSeconds(api.remaining(path)), 250); return () => clearInterval(id); }, [path]);
  return seconds;
}
function ErrorPanel({ error, retry, path }: { error: Error; retry: () => void; path: string }) {
  const seconds = useCooldown(path);
  return <div className="client-error" role="alert"><p>{error.message}</p><button type="button" disabled={seconds > 0} onClick={retry}>{seconds ? `Повторити через ${seconds} с` : 'Повторити'}</button></div>;
}
function useResource<T>(path: string, tick = 0) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [loading, setLoading] = useState(true);
  const [stale, setStale] = useState(false);
  const navigate = useNavigate(); const location = useLocation();
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(null); setData(null); setStale(false);
    api.request<T>(path, { signal: controller.signal }).then(result => { if (!controller.signal.aborted) {
        setData(result);
        if (path.startsWith('/api/v1/catalog/items?')) { try { localStorage.setItem('cinemacity-public-catalog', JSON.stringify({ path, data: result })); } catch { /* Storage may be unavailable. */ } }
      } }).catch(error => {
      if (controller.signal.aborted) return;
      if (error instanceof ApiError && error.status === 0 && path.startsWith('/api/v1/catalog/items?')) {
        try { const cached = JSON.parse(localStorage.getItem('cinemacity-public-catalog') || 'null');
          if (cached?.path === path && Array.isArray(cached.data?.items)) { setData(cached.data); setStale(true); return; }
        } catch { /* A bad cache never prevents a normal error message. */ }
      }
      if (error instanceof ApiError && error.status === 401) navigate(`/auth?returnTo=${encodeURIComponent(location.pathname)}`, { replace: true });
      else setError(error);
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [path, tick]);
  return { data, error, loading, stale };
}
function Fields({ errors, name }: { errors: FieldErrors; name: string }) {
  return errors[name]?.length ? <span className="client-field-error" role="alert">{errors[name].join(' ')}</span> : null;
}
function Loading() { return <div className="client-skeleton" role="status" aria-label="Завантаження"><span /><span /><span /></div>; }
function Intro({ eyebrow, title, children }: { eyebrow: string; title: string; children?: React.ReactNode }) {
  return <header className="client-intro"><p className="kicker">{eyebrow}</p><h1>{title}</h1>{children}</header>;
}
export function ClientCatalog() {
  const [query, setQuery] = useState(''); const [search, setSearch] = useState(''); const [page, setPage] = useState(1); const [tick, setTick] = useState(0);
  useEffect(() => { if (query.trim() === search) return; const id = setTimeout(() => { setSearch(query.trim()); setPage(1); }, 400); return () => clearTimeout(id); }, [query, search]);
  const path = `/api/v1/catalog/items?page=${page}&pageSize=12&search=${encodeURIComponent(search)}&sortBy=title&desc=false`;
  const { data, error, loading, stale } = useResource<MoviePage>(path, tick);
  const total = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;
  return <section className="client-page"><Intro eyebrow="CinemaCity / Каталог" title="Знайдіть свій наступний фільм"><p>Фільми й серіали, ваші оцінки та списки перегляду.</p><Link to="/my-ratings">Мої оцінки →</Link></Intro>
    {stale ? <div className="client-warning" role="status">Немає з’єднання. Показано застарілі дані каталогу. <button onClick={() => setTick(tick + 1)}>Повторити</button></div> : null}
    <label className="client-search">Пошук фільму<input type="search" value={query} onChange={e => setQuery(e.target.value)} placeholder="Введіть назву…" /></label>
    {loading ? <Loading /> : error ? <ErrorPanel error={error} path={path} retry={() => setTick(tick + 1)} /> : data && data.items.length === 0 ? <div className="client-empty"><h2>Нічого не знайдено</h2><p>Спробуйте іншу назву.</p><button onClick={() => setQuery('')}>Очистити пошук</button></div> : <>
      <div className="client-grid">{data?.items.map(movie => <Link className="client-card" key={movie.id} to={`/movies/${movie.id}`}>
        {movie.posterUrl ? <img src={movie.posterUrl} alt="" loading="lazy" /> : <div className="client-poster" aria-hidden="true">CC<span>CINEMACITY</span></div>}
        <div><p>{movie.releaseYear}</p><h2>{movie.title}</h2><span>Деталі та моя оцінка ↗</span></div>
      </Link>)}</div>
      <nav className="client-pagination" aria-label="Сторінки каталогу"><button disabled={loading || page <= 1} onClick={() => setPage(page - 1)}>Назад</button><span>Сторінка {page} з {total}</span><button disabled={loading || page >= total} onClick={() => setPage(page + 1)}>Далі</button></nav>
    </>}
  </section>;
}
export function ClientDetails() {
  const { id } = useParams(); const [tick, setTick] = useState(0); const path = `/api/v1/screens/movies/${id}`;
  const { data, error, loading } = useResource<Screen>(path, tick);
  return <section className="client-page"><Link to="/catalog">← До каталогу</Link>
    {loading ? <Loading /> : error ? <ErrorPanel error={error} retry={() => setTick(tick + 1)} path={path} /> : data ? <>
      <Intro eyebrow={`CinemaCity / ${data.movie.releaseYear}`} title={data.movie.title} />
      <div className="client-detail-grid"><div className="client-detail-poster">{data.movie.posterUrl ? <img src={data.movie.posterUrl} alt={`Постер: ${data.movie.title}`} /> : <div className="client-poster">CC<span>CINEMACITY</span></div>}</div><div className="client-surface">
        <h2>Ваш кінопростір</h2>
        {data.partial ? <div className="client-warning" role="status"><h3>Особисті дані тимчасово недоступні</h3><p>Опис фільму доступний. Зараз не вдалося перевірити вашу оцінку та списки.</p><button onClick={() => setTick(tick + 1)}>Повторити</button></div> : <><p className="client-score">{data.myState?.myScore != null ? `${data.myState.myScore} / 10` : 'Ви ще не оцінили цей фільм'}</p><p>{data.myState?.watchlistStatus === 'Watched' ? 'Переглянуто' : data.myState?.watchlistStatus === 'Watchlist' ? 'У списку перегляду' : 'Ще не додано до списку'}</p>
          {data.myState?.lists.length ? <ul>{data.myState.lists.map(list => <li key={list.id}>{list.name}</li>)}</ul> : <p>У власних списках поки немає.</p>}
          <Link className="primary-action" to={`/movies/${id}/rate`}>{data.myState?.myScore != null ? 'Змінити оцінку' : 'Оцінити фільм'}</Link></>}
        <Link to="/my-ratings">Переглянути мої оцінки →</Link>
      </div></div>
    </> : <p>Фільм не знайдено.</p>}
  </section>;
}
export function ClientRatingForm() {
  const { id } = useParams(); const navigate = useNavigate(); const [score, setScore] = useState('');
  const [busy, setBusy] = useState(false); const inFlight = useRef(false); const key = useRef<string | null>(null); const sentBody = useRef<string | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({}); const [error, setError] = useState<Error | null>(null); const [tick, setTick] = useState(0);
  const path = '/api/ratings'; const cooldown = useCooldown(path);
  const mine = useResource<RatingPage>(`/api/ratings/page?status=Active&movieId=${id}&pageSize=1`, tick);
  const existing = mine.data?.items[0];
  async function submit(event: FormEvent) {
    event.preventDefault(); if (inFlight.current || cooldown) return;
    const value = Number(score);
    if (!score || !Number.isFinite(value) || value < 1 || value > 10) { setErrors({ score: ['Вкажіть оцінку від 1 до 10.'] }); return; }
    const body = JSON.stringify(existing ? { score: value } : { movieId: id, score: value });
    if (!key.current || sentBody.current !== body) { key.current = crypto.randomUUID(); sentBody.current = body; }
    inFlight.current = true; setBusy(true); setErrors({}); setError(null);
    try {
      await api.request(existing ? `/api/ratings/${existing.id}` : path, { method: existing ? 'PUT' : 'POST', body, headers: { 'Idempotency-Key': key.current } });
      key.current = null; sentBody.current = null; navigate('/my-ratings', { replace: true });
    } catch (caught) {
      const error = caught as ApiError; setError(error); setErrors(error.fields ?? {});
      if (error.status === 401) navigate(`/auth?returnTo=${encodeURIComponent(`/movies/${id}/rate`)}`);
    } finally { inFlight.current = false; setBusy(false); }
  }
  return <section className="client-page"><Link to={`/movies/${id}`}>← До фільму</Link><Intro eyebrow="Особистий простір" title={existing ? 'Змінити оцінку' : 'Оцінити фільм'} />
    {mine.loading ? <Loading /> : mine.error ? <ErrorPanel error={mine.error} path={path} retry={() => setTick(tick + 1)} /> : <form className="client-surface client-form" onSubmit={submit} noValidate>
      <label>Оцінка від 1 до 10<input autoFocus type="number" min="1" max="10" step="0.5" value={score} placeholder={existing ? String(existing.score) : 'Наприклад, 8'} disabled={busy} aria-invalid={!!errors.score} onChange={e => { setScore(e.target.value); setErrors({}); }} /><Fields errors={errors} name="score" /></label>
      <Fields errors={errors} name="movieid" />
      {error ? <div role="alert" className="client-error"><p>{error.message}</p><p>Після мережевої помилки натисніть збереження ще раз — повтор не створить дубль.</p></div> : null}
      <button className="primary-action" disabled={busy || cooldown > 0} type="submit">{busy ? 'Зберігаємо…' : cooldown ? `Повторити через ${cooldown} с` : 'Зберегти оцінку'}</button>
    </form>}
  </section>;
}
export function ClientMyRatings() {
  const [tick, setTick] = useState(0); const [page, setPage] = useState(1); const [status, setStatus] = useState('Active');
  const path = `/api/ratings/page?page=${page}&pageSize=12&status=${status}`; const { data, error, loading } = useResource<RatingPage>(path, tick);
  const [busy, setBusy] = useState<string | null>(null); const [actionError, setActionError] = useState<Error | null>(null); const cooldown = useCooldown(path);
  async function cancel(id: string) {
    if (busy) return; setBusy(id); setActionError(null);
    try { await api.request(`/api/ratings/${id}/cancel`, { method: 'POST', body: '{}' }); setPage(1); setTick(tick + 1); } catch (error) { setActionError(error as Error); } finally { setBusy(null); }
  }
  return <section className="client-page"><Intro eyebrow="Ваш кінопростір" title="Мої оцінки"><p>Ваші опубліковані оцінки. Скасування прибирає оцінку з фільму.</p><Link to="/catalog">Обрати фільм →</Link></Intro>
    <label>Статус оцінки<select aria-label="Статус оцінки" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="Active">Опубліковані</option><option value="Cancelled">Скасовані</option><option value="All">Усі</option></select></label>
    {actionError ? <ErrorPanel error={actionError} path={path} retry={() => { setActionError(null); setTick(tick + 1); }} /> : null}
    {loading ? <Loading /> : error ? <ErrorPanel error={error} path={path} retry={() => setTick(tick + 1)} /> : !data?.items.length ? <div className="client-empty"><h2>Оцінок ще немає</h2><p>Знайдіть фільм у каталозі та поділіться враженням.</p><Link to="/catalog">Перейти до каталогу</Link></div> : <ul className="client-operations">{data.items.map(rating => <li className="client-surface" key={rating.id}><div><h2>{rating.movieId ? <Link to={`/movies/${rating.movieId}`}>{rating.title}</Link> : rating.title}</h2><span className="client-badge">{rating.status === 'Active' ? 'Опубліковано' : 'Скасовано'}</span><p>{new Date(rating.createdAt).toLocaleDateString('uk-UA')} · {rating.score} / 10</p></div><button disabled={rating.status !== 'Active' || busy !== null || cooldown > 0} onClick={() => cancel(rating.id)}>{rating.status === 'Cancelled' ? 'Скасовано' : busy === rating.id ? 'Скасовуємо…' : 'Скасувати оцінку'}</button></li>)}</ul>}
    {data && data.totalCount > 0 ? <nav className="client-pagination" aria-label="Сторінки оцінок"><button disabled={loading || page <= 1} onClick={() => setPage(page - 1)}>Назад</button><span>Сторінка {page} з {Math.max(1, Math.ceil(data.totalCount / data.pageSize))}</span><button disabled={loading || page * data.pageSize >= data.totalCount} onClick={() => setPage(page + 1)}>Далі</button></nav> : null}
  </section>;
}
export function ClientAuth() {
  const navigate = useNavigate(); const location = useLocation(); const [register, setRegister] = useState(false);
  const [email, setEmail] = useState(''); const [password, setPassword] = useState(''); const [confirm, setConfirm] = useState(''); const [username, setUsername] = useState('');
  const [busy, setBusy] = useState(false); const [error, setError] = useState<Error | null>(null); const [fields, setFields] = useState<FieldErrors>({}); const locked = useRef(false);
  const path = register ? '/api/auth/register' : '/api/auth/login'; const seconds = useCooldown(path);
  function complete() { const target = new URLSearchParams(location.search).get('returnTo'); navigate(target?.startsWith('/') && !target.startsWith('//') && !target.includes('\\') && !/[\u0000-\u001f]/.test(target) ? target : '/catalog', { replace: true }); }
  useEffect(() => { if (api.getSession()) complete(); }, []);
  async function submit(event: FormEvent) {
    event.preventDefault(); if (locked.current || seconds) return;
    const errors: FieldErrors = {};
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = ['Вкажіть коректний email.'];
    if (!password || (register && password.length < 8)) errors.password = ['Вкажіть пароль' + (register ? ' щонайменше з 8 символів.' : '.')];
    if (register && username.trim().length < 3) errors.username = ['Ім’я має містити щонайменше 3 символи.'];
    if (register && password !== confirm) errors.confirmpassword = ['Паролі не збігаються.'];
    if (Object.keys(errors).length) { setFields(errors); return; }
    locked.current = true; setBusy(true); setFields({}); setError(null);
    try { if (register) await api.register(username.trim(), email, password, confirm); else await api.login(email, password); complete(); }
    catch (error) { setError(error as Error); setFields(error instanceof ApiError ? error.fields : {}); }
    finally { locked.current = false; setBusy(false); }
  }
  return <section className="client-page"><Intro eyebrow="CinemaCity / Особистий простір" title={register ? 'Створіть акаунт' : 'З поверненням у кіно'}><p>Зберігайте враження від фільмів у власних оцінках і списках.</p></Intro><form className="client-surface client-form" onSubmit={submit} noValidate>
    {register ? <label>Ім’я користувача<input value={username} disabled={busy} onChange={e => setUsername(e.target.value)} autoComplete="username" /><Fields errors={fields} name="username" /></label> : null}
    <label>Email<input type="email" value={email} disabled={busy} onChange={e => setEmail(e.target.value)} autoComplete="email" aria-invalid={!!fields.email} /><Fields errors={fields} name="email" /></label>
    <PasswordField placeholder="Ваш пароль" label="Пароль" value={password} onChange={setPassword} error={fields.password?.join(' ')} />
    {register ? <PasswordField placeholder="Повторіть пароль" label="Повторіть пароль" value={confirm} onChange={setConfirm} error={fields.confirmpassword?.join(' ')} /> : null}
    {error ? <p role="alert" className="client-error">{error.message}</p> : null}
    <button className="primary-action" disabled={busy || seconds > 0} type="submit">{busy ? 'Зачекайте…' : seconds ? `Повторити через ${seconds} с` : register ? 'Створити акаунт' : 'Увійти'}</button>
    <button disabled={busy} type="button" onClick={() => { setRegister(!register); setFields({}); setError(null); }}>{register ? 'У мене вже є акаунт' : 'Зареєструватися'}</button>
  </form></section>;
}
