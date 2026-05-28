import React, { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Link, Navigate, Route, Routes, useParams } from 'react-router-dom';
import './styles.css';

const defaultApiUrl =
  typeof window === 'undefined' ? 'http://localhost:5029' : `${window.location.protocol}//${window.location.hostname}:5029`;
const apiUrl = import.meta.env.VITE_API_URL ?? defaultApiUrl;

type AuthUser = {
  id: string;
  username: string;
  email: string;
  role: number;
  avatarUrl?: string | null;
};

type AuthResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
};

type RegisterResponse = {
  userId: string;
  username: string;
  email: string;
  isEmailConfirmed: boolean;
  emailConfirmationToken: string;
};

type SpotlightSlide = {
  title: string;
  meta: string;
  description: string;
  imageUrl: string;
};

type Genre = {
  id: number;
  name: string;
  slug: string;
};

type MovieCard = {
  id: string;
  title: string;
  originalTitle?: string | null;
  contentType: number | string;
  releaseYear: number;
  posterUrl?: string | null;
  backdropUrl?: string | null;
  averageRating: number;
  ratingsCount: number;
  genres: Genre[];
};

type MovieDetails = MovieCard & {
  duration?: number | null;
  description?: string | null;
  trailerYoutubeId?: string | null;
  countries: Array<{ id: number; name: string; code?: string | null }>;
  languages: Array<{ id: number; name: string; code?: string | null }>;
  cast: Array<{
    id: string;
    personId: string;
    fullName: string;
    photoUrl?: string | null;
    creditType: number | string;
    roleName?: string | null;
    sortOrder: number;
  }>;
};

type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};

const spotlightSlides: SpotlightSlide[] = [
  {
    title: 'Прем’єри тижня',
    meta: 'Новинки, трейлери, перші оцінки',
    description: 'Великий екран для нових історій, гучних релізів і кадрів, які хочеться переглядати ще раз.',
    imageUrl: 'https://image.tmdb.org/t/p/original/8rpDcsfLJypbO6vREc0547VKqEv.jpg'
  },
  {
    title: 'Фантастика на великому екрані',
    meta: 'Космос, неон, далекі світи',
    description: 'Добірка атмосферних фільмів і серіалів для вечора, коли хочеться втекти за межі звичного.',
    imageUrl: 'https://image.tmdb.org/t/p/original/p1F51Lvj3sMopG948F5HsBbl43C.jpg'
  },
  {
    title: 'Вечірні сеанси',
    meta: 'Драми, трилери, авторське кіно',
    description: 'Кадри для настрою: від тихих історій до напружених прем’єр, які лишаються після титрів.',
    imageUrl: 'https://image.tmdb.org/t/p/original/628Dep6AxEtDxjZoGP78TsOxYbK.jpg'
  },
  {
    title: 'Великі історії',
    meta: 'Біографії, події, розмови після титрів',
    description: 'Фільми, які збирають зал, тримають увагу і стають приводом для довгих обговорень.',
    imageUrl: 'https://image.tmdb.org/t/p/original/rLb2cwF3Pazuxaj0sRXQ037tGI1.jpg'
  }
];

function App() {
  const [mode, setMode] = useState<'register' | 'login'>('login');
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [confirmationToken, setConfirmationToken] = useState('');
  const [accessToken, setAccessToken] = useState('');
  const [user, setUser] = useState<AuthUser | null>(null);
  const [message, setMessage] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [isBusy, setIsBusy] = useState(false);

  const googleLoginUrl = useMemo(() => {
    const returnUrl =
      typeof window === 'undefined' ? '/auth' : `${window.location.origin}/auth?google=success`;

    return `${apiUrl}/api/auth/google?returnUrl=${encodeURIComponent(returnUrl)}`;
  }, []);

  const status = useMemo(() => {
    if (user) {
      return `Ви увійшли як ${user.username}`;
    }

    if (confirmationToken) {
      return 'Залишилося підтвердити пошту';
    }

    return 'Гість CinemaCity';
  }, [confirmationToken, user]);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);

    if (params.get('google') !== 'success') {
      return;
    }

    window.history.replaceState({}, '', '/auth');
    setIsBusy(true);

    fetch(`${apiUrl}/api/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({})
    })
      .then(async (response) => {
        const text = await response.text();
        const data = text ? JSON.parse(text) : null;

        if (!response.ok) {
          throw new Error(data?.message ?? data?.detail ?? 'Не вдалося відкрити ваш профіль.');
        }

        return data as AuthResponse;
      })
      .then((result) => {
        setAccessToken(result.accessToken);
        setUser(result.user);
        setMessage('Вхід через Google завершено. Приємного перегляду.');
      })
      .catch(() => {
        setMessage('Google повернув вас у CinemaCity. Спробуйте увійти ще раз.');
      })
      .finally(() => setIsBusy(false));
  }, []);

  async function request<T>(path: string, options: RequestInit): Promise<T> {
    const response = await fetch(`${apiUrl}${path}`, {
      ...options,
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...options.headers
      }
    });

    const text = await response.text();
    const data = text ? JSON.parse(text) : null;

    if (!response.ok) {
      throw new Error(toFriendlyMessage(data?.message ?? data?.detail));
    }

    return data as T;
  }

  async function handleRegister(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsBusy(true);
    setPasswordError('');

    try {
      const result = await request<RegisterResponse>('/api/auth/register', {
        method: 'POST',
        body: JSON.stringify({ username, email, password, confirmPassword })
      });

      setConfirmationToken(result.emailConfirmationToken);
      setMessage('');
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Не вдалося створити акаунт.';

      if (errorMessage === 'Пароль має містити щонайменше 8 символів.') {
        setPasswordError(errorMessage);
        setMessage('');
        return;
      }

      setMessage(errorMessage);
    } finally {
      setIsBusy(false);
    }
  }

  async function handleConfirmEmail() {
    if (!confirmationToken) {
      setMessage('Спочатку створіть акаунт.');
      return;
    }

    setIsBusy(true);

    try {
      await request('/api/auth/confirm-email', {
        method: 'POST',
        body: JSON.stringify({ token: confirmationToken })
      });

      setConfirmationToken('');
      setMode('login');
      setMessage('Пошту підтверджено. Тепер можна увійти.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Не вдалося підтвердити пошту.');
    } finally {
      setIsBusy(false);
    }
  }

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsBusy(true);

    try {
      const result = await request<AuthResponse>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password })
      });

      setAccessToken(result.accessToken);
      setUser(result.user);
      setMessage('Ви увійшли в CinemaCity.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Не вдалося увійти.');
    } finally {
      setIsBusy(false);
    }
  }

  async function handleLogout() {
    setIsBusy(true);

    try {
      await request('/api/auth/logout', {
        method: 'POST',
        body: JSON.stringify({})
      });

      setAccessToken('');
      setUser(null);
      setMessage('Ви вийшли з акаунта.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Не вдалося вийти.');
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/catalog" element={<CatalogPage />} />
        <Route path="/movies/:id" element={<MovieDetailsPage />} />
        <Route
          path="/auth"
          element={
            <AuthPage
              confirmPassword={confirmPassword}
              confirmationToken={confirmationToken}
              email={email}
              googleLoginUrl={googleLoginUrl}
              handleConfirmEmail={handleConfirmEmail}
              handleLogin={handleLogin}
              handleLogout={handleLogout}
              handleRegister={handleRegister}
              isBusy={isBusy}
              message={message}
              mode={mode}
              password={password}
              passwordError={passwordError}
              setConfirmPassword={setConfirmPassword}
              setEmail={setEmail}
              setMode={setMode}
              setPassword={setPassword}
              setPasswordError={setPasswordError}
              setUsername={setUsername}
              status={status}
              user={user}
              username={username}
            />
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

function HomePage() {
  const [activeSlide, setActiveSlide] = useState(0);
  const currentSlide = spotlightSlides[activeSlide];

  useEffect(() => {
    const timer = window.setInterval(() => {
      setActiveSlide((slide) => (slide + 1) % spotlightSlides.length);
    }, 5200);

    return () => window.clearInterval(timer);
  }, []);

  function goToSlide(slide: number) {
    setActiveSlide((slide + spotlightSlides.length) % spotlightSlides.length);
  }

  return (
    <main className="page-shell">
      <SiteHeader />

      <section className="home-stage" id="spotlight" aria-label="Головна сторінка CinemaCity">
        <div className="spotlight" style={{ backgroundImage: `url(${currentSlide.imageUrl})` }}>
          <div className="spotlight-overlay">
            <p className="kicker">Кінолента CinemaCity</p>
            <h1>{currentSlide.title}</h1>
            <p className="spotlight-meta">{currentSlide.meta}</p>
            <p className="spotlight-text">{currentSlide.description}</p>

            <div className="spotlight-controls" aria-label="Керування банером">
              <button type="button" aria-label="Попередній банер" onClick={() => goToSlide(activeSlide - 1)}>
                ‹
              </button>
              <button type="button" aria-label="Наступний банер" onClick={() => goToSlide(activeSlide + 1)}>
                ›
              </button>
            </div>
          </div>

          <div className="poster-rail" aria-label="Кадри фільмів">
            {spotlightSlides.map((slide, index) => (
              <button
                className={index === activeSlide ? 'active' : ''}
                key={slide.title}
                type="button"
                onClick={() => goToSlide(index)}
                aria-label={`Показати: ${slide.title}`}
              >
                <img src={slide.imageUrl} alt="" loading={index === 0 ? 'eager' : 'lazy'} />
              </button>
            ))}
          </div>
        </div>
      </section>
    </main>
  );
}

type AuthPageProps = {
  confirmPassword: string;
  confirmationToken: string;
  email: string;
  googleLoginUrl: string;
  handleConfirmEmail: () => Promise<void>;
  handleLogin: (event: FormEvent<HTMLFormElement>) => Promise<void>;
  handleLogout: () => Promise<void>;
  handleRegister: (event: FormEvent<HTMLFormElement>) => Promise<void>;
  isBusy: boolean;
  message: string;
  mode: 'register' | 'login';
  password: string;
  passwordError: string;
  setConfirmPassword: (value: string) => void;
  setEmail: (value: string) => void;
  setMode: (value: 'register' | 'login') => void;
  setPassword: (value: string) => void;
  setPasswordError: (value: string) => void;
  setUsername: (value: string) => void;
  status: string;
  user: AuthUser | null;
  username: string;
};

function AuthPage({
  confirmPassword,
  confirmationToken,
  email,
  googleLoginUrl,
  handleConfirmEmail,
  handleLogin,
  handleLogout,
  handleRegister,
  isBusy,
  message,
  mode,
  password,
  passwordError,
  setConfirmPassword,
  setEmail,
  setMode,
  setPassword,
  setPasswordError,
  setUsername,
  status,
  user,
  username
}: AuthPageProps) {
  const registerPasswordRef = useRef<HTMLInputElement>(null);

  function handleRegisterSubmit(event: FormEvent<HTMLFormElement>) {
    if (password.length < 8) {
      event.preventDefault();
      setPasswordError('Пароль має містити щонайменше 8 символів.');
      registerPasswordRef.current?.focus();
      return;
    }

    handleRegister(event);
  }

  function handlePasswordChange(value: string) {
    setPassword(value);

    if (passwordError && value.length >= 8) {
      setPasswordError('');
    }
  }

  return (
    <main className="page-shell auth-page-shell">
      <SiteHeader />

      <section className="auth-page-grid" aria-label="Сторінка автентифікації CinemaCity">
        <div className="auth-poster" aria-hidden="true">
          <div>
            <p className="kicker">Особистий зал</p>
            <h1>Поверніться до своїх фільмів</h1>
            <p className="spotlight-text">Профіль збере майбутні перегляди, оцінки та списки, щойно каталог буде готовий.</p>
          </div>
        </div>

        <section className="auth-surface" aria-label="Вхід в CinemaCity">
          <div>
            <p className="eyebrow">Ваш профіль</p>
            <h2>{mode === 'register' ? 'Створіть акаунт' : 'Увійдіть в акаунт'}</h2>
            <p className="auth-lead">{status}</p>
            <p className="auth-mode-copy">
              {mode === 'register'
                ? 'Новий профіль для оцінок, списків перегляду та майбутніх рецензій.'
                : 'Коротка форма для повернення до вашого профілю.'}
            </p>
          </div>

          <div className="tabs" role="tablist" aria-label="Режим автентифікації">
            <button
              className={mode === 'login' ? 'active' : ''}
              type="button"
              onClick={() => {
                setMode('login');
                setPasswordError('');
              }}
            >
              Вхід
            </button>
            <button
              className={mode === 'register' ? 'active' : ''}
              type="button"
              onClick={() => {
                setMode('register');
                setPasswordError('');
              }}
            >
              Реєстрація
            </button>
          </div>

          {mode === 'register' ? (
            <form className="auth-form auth-form--register" onSubmit={handleRegisterSubmit}>
              <label>
                Ім’я в CinemaCity
                <input
                  value={username}
                  onChange={(event) => setUsername(event.target.value)}
                  placeholder="Ваше ім’я"
                />
              </label>
              <label>
                Електронна пошта
                <input
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="Ваша пошта"
                  type="email"
                />
              </label>
              <label>
                Пароль
                <input
                  aria-describedby={passwordError ? 'password-error' : undefined}
                  aria-invalid={passwordError ? 'true' : 'false'}
                  className={passwordError ? 'input-error' : ''}
                  ref={registerPasswordRef}
                  value={password}
                  onChange={(event) => handlePasswordChange(event.target.value)}
                  placeholder="Ваш пароль"
                  type="password"
                />
                {passwordError ? (
                  <span className="field-error" id="password-error">
                    {passwordError}
                  </span>
                ) : null}
              </label>
              <label>
                Повторіть пароль
                <input
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  placeholder="Повторіть пароль"
                  type="password"
                />
              </label>
              <button className="primary-action" disabled={isBusy} type="submit">
                Створити акаунт
              </button>
            </form>
          ) : (
            <form className="auth-form auth-form--login" onSubmit={handleLogin}>
              <label>
                Електронна пошта
                <input
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="Ваша пошта"
                  type="email"
                />
              </label>
              <label>
                Пароль
                <input
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="Ваш пароль"
                  type="password"
                />
              </label>
              <button className="primary-action" disabled={isBusy} type="submit">
                Увійти
              </button>
            </form>
          )}

          {confirmationToken ? (
            <div className="confirm-strip">
              <p>Акаунт створено. Підтвердьте пошту, щоб перейти до входу.</p>
              <button disabled={isBusy} type="button" onClick={handleConfirmEmail}>
                Підтвердити пошту
              </button>
            </div>
          ) : null}

          <a className="google-link" href={googleLoginUrl}>
            Увійти через Google
          </a>

          {message ? (
            <output className="message" aria-live="polite">
              {message}
            </output>
          ) : null}

          {user ? (
            <div className="profile-strip">
              <span>{user.username.slice(0, 2).toUpperCase()}</span>
              <div>
                <strong>{user.username}</strong>
                <small>{user.email}</small>
              </div>
              <button disabled={isBusy} type="button" onClick={handleLogout}>
                Вийти
              </button>
            </div>
          ) : null}
        </section>
      </section>
    </main>
  );
}

function SiteHeader() {
  return (
    <header className="site-header" aria-label="Головне меню">
      <Link className="brand-row" to="/">
        <span className="brand-mark">CC</span>
        <span>CinemaCity</span>
      </Link>
      <nav>
        <Link to="/">Головна</Link>
        <Link to="/catalog">Каталог</Link>
        <Link to="/auth">Увійти</Link>
      </nav>
    </header>
  );
}

function CatalogPage() {
  const [movies, setMovies] = useState<PagedResult<MovieCard> | null>(null);
  const [genres, setGenres] = useState<Genre[]>([]);
  const [query, setQuery] = useState('');
  const [genre, setGenre] = useState('');
  const [contentType, setContentType] = useState('');
  const [sort, setSort] = useState('rating');
  const [yearFrom, setYearFrom] = useState('');
  const [ratingMin, setRatingMin] = useState('');
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    fetch(`${apiUrl}/api/genres`)
      .then((response) => (response.ok ? response.json() : []))
      .then((data: Genre[]) => setGenres(data))
      .catch(() => setGenres([]));
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: '12',
      sort
    });

    if (query.trim()) params.set('q', query.trim());
    if (genre) params.set('genre', genre);
    if (contentType) params.set('contentType', contentType);
    if (yearFrom) params.set('yearFrom', yearFrom);
    if (ratingMin) params.set('ratingMin', ratingMin);

    setIsLoading(true);
    setError('');

    fetch(`${apiUrl}/api/movies?${params}`, { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error('Не вдалося завантажити каталог.');
        }

        return (await response.json()) as PagedResult<MovieCard>;
      })
      .then(setMovies)
      .catch((fetchError) => {
        if (fetchError instanceof DOMException && fetchError.name === 'AbortError') {
          return;
        }

        setError(fetchError instanceof TypeError ? 'Каталог тимчасово недоступний. Спробуйте оновити сторінку пізніше.' : fetchError instanceof Error ? fetchError.message : 'Не вдалося завантажити каталог.');
      })
      .finally(() => setIsLoading(false));

    return () => controller.abort();
  }, [contentType, genre, page, query, ratingMin, sort, yearFrom]);

  const pageCount = movies ? Math.max(Math.ceil(movies.total / movies.pageSize), 1) : 1;

  function resetPage(update: () => void) {
    setPage(1);
    update();
  }

  return (
    <main className="page-shell">
      <SiteHeader />

      <section className="catalog-shell" aria-label="Каталог фільмів">
        <div className="catalog-heading">
          <p className="kicker">Каталог CinemaCity</p>
          <h1>Фільми та серіали</h1>
          <p>Шукайте за назвою, фільтруйте за жанром, роком, рейтингом і типом контенту.</p>
        </div>

        <div className="catalog-filters" aria-label="Фільтри каталогу">
          <label>
            Пошук
            <input
              value={query}
              onChange={(event) => resetPage(() => setQuery(event.target.value))}
              placeholder="Назва фільму або серіалу"
            />
          </label>
          <label>
            Жанр
            <select value={genre} onChange={(event) => resetPage(() => setGenre(event.target.value))}>
              <option value="">Усі жанри</option>
              {genres.map((item) => (
                <option key={item.id} value={item.slug}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Тип
            <select value={contentType} onChange={(event) => resetPage(() => setContentType(event.target.value))}>
              <option value="">Усе</option>
              <option value="Movie">Фільми</option>
              <option value="Series">Серіали</option>
            </select>
          </label>
          <label>
            Рік від
            <input
              min="1888"
              value={yearFrom}
              onChange={(event) => resetPage(() => setYearFrom(event.target.value))}
              placeholder="2010"
              type="number"
            />
          </label>
          <label>
            Рейтинг від
            <input
              max="10"
              min="1"
              step="0.1"
              value={ratingMin}
              onChange={(event) => resetPage(() => setRatingMin(event.target.value))}
              placeholder="7.5"
              type="number"
            />
          </label>
          <label>
            Сортування
            <select value={sort} onChange={(event) => resetPage(() => setSort(event.target.value))}>
              <option value="rating">За рейтингом</option>
              <option value="year">За роком</option>
              <option value="title">За назвою</option>
              <option value="newest">Нові додані</option>
            </select>
          </label>
        </div>

        {error ? <p className="catalog-message">{error}</p> : null}
        {isLoading ? <p className="catalog-message">Завантаження каталогу...</p> : null}

        {!isLoading && movies?.items.length === 0 ? (
          <div className="empty-state">
            <h2>Каталог поки порожній</h2>
            <p>Додайте фільми в адмінському розділі або змініть фільтри.</p>
          </div>
        ) : null}

        <div className="movie-grid">
          {movies?.items.map((movie) => (
            <MovieCardLink key={movie.id} movie={movie} />
          ))}
        </div>

        {movies && movies.total > movies.pageSize ? (
          <div className="pagination">
            <button disabled={page <= 1} type="button" onClick={() => setPage((current) => Math.max(current - 1, 1))}>
              Назад
            </button>
            <span>
              Сторінка {page} з {pageCount}
            </span>
            <button disabled={page >= pageCount} type="button" onClick={() => setPage((current) => current + 1)}>
              Далі
            </button>
          </div>
        ) : null}
      </section>
    </main>
  );
}

function MovieDetailsPage() {
  const { id } = useParams();
  const [movie, setMovie] = useState<MovieDetails | null>(null);
  const [similar, setSimilar] = useState<MovieCard[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!id) {
      return;
    }

    const controller = new AbortController();
    setIsLoading(true);
    setError('');

    Promise.all([
      fetch(`${apiUrl}/api/movies/${id}`, { signal: controller.signal }).then(async (response) => {
        if (!response.ok) throw new Error('Фільм не знайдено.');
        return (await response.json()) as MovieDetails;
      }),
      fetch(`${apiUrl}/api/movies/${id}/similar`, { signal: controller.signal }).then(async (response) => {
        if (!response.ok) return [];
        return (await response.json()) as MovieCard[];
      })
    ])
      .then(([movieResult, similarResult]) => {
        setMovie(movieResult);
        setSimilar(similarResult);
      })
      .catch((fetchError) => {
        if (fetchError instanceof DOMException && fetchError.name === 'AbortError') {
          return;
        }

        setError(fetchError instanceof TypeError ? 'Сторінка фільму тимчасово недоступна. Спробуйте оновити її пізніше.' : fetchError instanceof Error ? fetchError.message : 'Не вдалося відкрити сторінку фільму.');
      })
      .finally(() => setIsLoading(false));

    return () => controller.abort();
  }, [id]);

  return (
    <main className="page-shell">
      <SiteHeader />

      {isLoading ? <p className="catalog-message">Завантаження фільму...</p> : null}
      {error ? <p className="catalog-message">{error}</p> : null}

      {movie ? (
        <section className="movie-details">
          <div className="movie-backdrop" style={{ backgroundImage: `url(${movie.backdropUrl || movie.posterUrl || ''})` }}>
            <div>
              <p className="kicker">{formatContentType(movie.contentType)}</p>
              <h1>{movie.title}</h1>
              <p>{movie.description || 'Опис для цього фільму ще не додано.'}</p>
              <div className="movie-facts">
                <span>{movie.releaseYear}</span>
                {movie.duration ? <span>{movie.duration} хв</span> : null}
                <span>Рейтинг {formatRating(movie.averageRating)}</span>
                <span>{movie.ratingsCount} оцінок</span>
              </div>
            </div>
          </div>

          <div className="details-grid">
            <section>
              <h2>Деталі</h2>
              <TagList title="Жанри" items={movie.genres.map((item) => item.name)} />
              <TagList title="Країни" items={movie.countries.map((item) => item.name)} />
              <TagList title="Мови" items={movie.languages.map((item) => item.name)} />
              {movie.trailerYoutubeId ? (
                <a className="trailer-link" href={`https://www.youtube.com/watch?v=${movie.trailerYoutubeId}`}>
                  Дивитися трейлер
                </a>
              ) : null}
            </section>

            <section>
              <h2>Акторський склад</h2>
              {movie.cast.length ? (
                <div className="cast-list">
                  {movie.cast.map((cast) => (
                    <div key={cast.id}>
                      <strong>{cast.fullName}</strong>
                      <span>
                        {formatCreditType(cast.creditType)}
                        {cast.roleName ? ` · ${cast.roleName}` : ''}
                      </span>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="muted-text">Склад ще не додано.</p>
              )}
            </section>
          </div>

          <section className="similar-section">
            <h2>Схожі фільми</h2>
            {similar.length ? (
              <div className="movie-grid compact">
                {similar.map((item) => (
                  <MovieCardLink key={item.id} movie={item} />
                ))}
              </div>
            ) : (
              <p className="muted-text">Рекомендації з’являться, коли в каталозі буде більше фільмів зі схожими жанрами.</p>
            )}
          </section>
        </section>
      ) : null}
    </main>
  );
}

function MovieCardLink({ movie }: { movie: MovieCard }) {
  return (
    <Link className="movie-card" to={`/movies/${movie.id}`}>
      <div className="movie-card-media">
        {movie.posterUrl || movie.backdropUrl ? (
          <img src={movie.posterUrl || movie.backdropUrl || ''} alt="" loading="lazy" />
        ) : (
          <span>{movie.title.slice(0, 2).toUpperCase()}</span>
        )}
      </div>
      <div className="movie-card-body">
        <span>{formatContentType(movie.contentType)} · {movie.releaseYear}</span>
        <h3>{movie.title}</h3>
        <p>Рейтинг {formatRating(movie.averageRating)} · {movie.ratingsCount} оцінок</p>
        <div>
          {movie.genres.slice(0, 3).map((genre) => (
            <small key={genre.id}>{genre.name}</small>
          ))}
        </div>
      </div>
    </Link>
  );
}

function TagList({ title, items }: { title: string; items: string[] }) {
  return (
    <div className="tag-row">
      <strong>{title}</strong>
      <div>
        {items.length ? items.map((item) => <span key={item}>{item}</span>) : <span>Не вказано</span>}
      </div>
    </div>
  );
}

function formatRating(value: number) {
  return Number(value || 0).toFixed(1);
}

function formatContentType(value: number | string) {
  return value === 1 || value === 'Series' ? 'Серіал' : 'Фільм';
}

function formatCreditType(value: number | string) {
  if (value === 1 || value === 'Director') return 'Режисер';
  if (value === 2 || value === 'Writer') return 'Сценарист';
  if (value === 3 || value === 'Producer') return 'Продюсер';
  return 'Актор';
}

function toFriendlyMessage(message?: string) {
  switch (message) {
    case 'Username must be between 3 and 30 characters.':
      return 'Ім’я має бути від 3 до 30 символів.';
    case 'Email format is invalid.':
      return 'Перевірте електронну пошту: адреса виглядає неправильно.';
    case 'Password must contain at least 8 characters.':
      return 'Пароль має містити щонайменше 8 символів.';
    case 'Password confirmation does not match.':
      return 'Паролі не збігаються.';
    case 'Username is already taken.':
      return 'Це ім’я вже зайняте.';
    case 'Email is already registered.':
      return 'Ця пошта вже зареєстрована.';
    case 'Email confirmation token is invalid or expired.':
    case 'Email confirmation token is invalid.':
      return 'Підтвердження пошти застаріло. Створіть акаунт ще раз.';
    case 'User was not found.':
      return 'Акаунт не знайдено.';
    case 'Account is temporarily locked.':
      return 'Акаунт тимчасово заблоковано після кількох спроб входу.';
    case 'Email must be confirmed before login.':
      return 'Спочатку підтвердьте пошту.';
    default:
      return message || 'Щось пішло не так. Спробуйте ще раз.';
  }
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
