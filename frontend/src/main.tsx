import React, { FormEvent, createContext, useContext, useEffect, useMemo, useRef, useState } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Link, Navigate, NavLink, Route, Routes, useLocation, useNavigate, useParams } from 'react-router-dom';
import './styles.css';

const defaultApiUrl =
  typeof window === 'undefined' ? 'http://localhost:5029' : `${window.location.protocol}//${window.location.hostname}:5029`;
const apiUrl = import.meta.env.VITE_API_URL ?? defaultApiUrl;

type ThemeMode = 'dark' | 'light';

type AuthUser = {
  id: string;
  username: string;
  email: string;
  role: number;
  avatarUrl?: string | null;
};

type HeaderAuthContextValue = {
  user: AuthUser | null;
  isBusy: boolean;
  handleLogout: () => Promise<void>;
};

const HeaderAuthContext = createContext<HeaderAuthContextValue>({
  user: null,
  isBusy: false,
  handleLogout: async () => {}
});

type ToastTone = 'success' | 'error' | 'info';

type ToastMessage = {
  id: number;
  message: string;
  tone: ToastTone;
};

type ToastContextValue = {
  notify: (message: string, tone?: ToastTone) => void;
};

const ToastContext = createContext<ToastContextValue>({
  notify: () => {}
});

function useToast() {
  return useContext(ToastContext);
}

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

type AdminGenre = Genre & {
  moviesCount: number;
};

type AdminPerson = {
  id: string;
  fullName: string;
  birthDate?: string | null;
  birthPlace?: string | null;
  bio?: string | null;
  photoUrl?: string | null;
  creditsCount: number;
};

type UploadResponse = {
  path: string;
  url: string;
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

type EpisodeDetails = {
  id: string;
  seasonId: string;
  episodeNumber: number;
  title: string;
  description?: string | null;
  airDate?: string | null;
  duration?: number | null;
  averageRating: number;
  ratingsCount: number;
};

type SeasonDetails = {
  id: string;
  seriesId: string;
  seasonNumber: number;
  title?: string | null;
  description?: string | null;
  releaseYear?: number | null;
  episodes: EpisodeDetails[];
};

type RatingResponse = {
  id: string;
  movieId?: string | null;
  episodeId?: string | null;
  score: number;
  createdAt: string;
  updatedAt?: string | null;
};

type ReviewResponse = {
  id: string;
  userId: string;
  username: string;
  movieId: string;
  content: string;
  isSpoiler: boolean;
  status?: number | string;
  rejectionReason?: string | null;
  likesCount: number;
  createdAt: string;
  updatedAt?: string | null;
};

type CommentResponse = {
  id: string;
  reviewId: string;
  userId: string;
  username: string;
  content: string;
  createdAt: string;
  updatedAt?: string | null;
};

type WatchlistStatus = 'Watchlist' | 'Watched' | 0 | 1;

type MovieUserState = {
  watchlistStatus?: WatchlistStatus | null;
  addedAt?: string | null;
  rating?: {
    id: string;
    score: number;
  } | null;
  lists?: Array<{
    id: string;
    name: string;
  }>;
};

type ProfileStats = {
  watchlistCount: number;
  watchedCount: number;
  ratingsCount: number;
  averageScore: number;
  reviewsCount: number;
  approvedReviewsCount: number;
  commentsCount: number;
  likedReviewsCount: number;
  favoriteGenres: string[];
};

type WatchlistItem = MovieCard & {
  movieId: string;
  status: WatchlistStatus;
  addedAt: string;
  updatedAt?: string | null;
};

type HomeRailResponse = {
  source: string;
  items: MovieCard[];
};

type MovieListSummary = {
  id: string;
  name: string;
  description?: string | null;
  isPublic: boolean;
  itemsCount: number;
  createdAt: string;
  updatedAt?: string | null;
};

type MovieListItem = MovieCard & {
  movieId: string;
  addedAt: string;
};

type MovieListDetails = MovieListSummary & {
  items: MovieListItem[];
};

type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};

type SearchScope = 'all' | 'movie' | 'series' | 'episode' | 'person';

type SearchResponse = {
  scope: string;
  query: string;
  movies: Array<{
    id: string;
    title: string;
    originalTitle?: string | null;
    contentType: number | string;
    releaseYear: number;
    posterUrl?: string | null;
    averageRating: number;
  }>;
  episodes: Array<{
    id: string;
    title: string;
    seriesId: string;
    seriesTitle: string;
    seasonNumber: number;
    episodeNumber: number;
    airDate?: string | null;
    averageRating: number;
  }>;
  people: Array<{
    id: string;
    fullName: string;
    photoUrl?: string | null;
    birthPlace?: string | null;
  }>;
};

const spotlightSlides: SpotlightSlide[] = [
  {
    title: 'Прем’єри та добірки без зайвого шуму',
    meta: 'Фільми, серіали, рейтинги та персональні списки',
    description:
      'CinemaCity допомагає швидко знайти що подивитися сьогодні: від великих прем’єр до тихих авторських історій.',
    imageUrl: 'https://image.tmdb.org/t/p/original/8rpDcsfLJypbO6vREc0547VKqEv.jpg'
  },
  {
    title: 'Серіали, які варто продовжити',
    meta: 'Сезони, епізоди, оцінки глядачів',
    description:
      'Тримайте під рукою серіали, до яких хочеться повернутися, і відкривайте схожі історії прямо з каталогу.',
    imageUrl: 'https://image.tmdb.org/t/p/original/p1F51Lvj3sMopG948F5HsBbl43C.jpg'
  },
  {
    title: 'Вечірній сеанс за хвилину',
    meta: 'Пошук за назвою, жанром, роком і рейтингом',
    description:
      'Фільтри, пошук і зрозумілі картки прибирають хаос з вибору, коли хочеться просто натиснути Play.',
    imageUrl: 'https://image.tmdb.org/t/p/original/628Dep6AxEtDxjZoGP78TsOxYbK.jpg'
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
  const [accessTokenExpiresAt, setAccessTokenExpiresAt] = useState('');
  const [user, setUser] = useState<AuthUser | null>(null);
  const [message, setMessage] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [isAuthReady, setIsAuthReady] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const didRestoreAuthRef = useRef(false);

  const googleLoginUrl = useMemo(() => {
    const returnUrl = typeof window === 'undefined' ? '/auth' : `${window.location.origin}/auth?google=success`;

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

  function notify(message: string, tone: ToastTone = 'info') {
    const id = Date.now() + Math.floor(Math.random() * 1000);
    setToasts((current) => [...current, { id, message, tone }]);
    window.setTimeout(() => {
      setToasts((current) => current.filter((toast) => toast.id !== id));
    }, 4200);
  }

  function applyAuthSession(auth: AuthResponse) {
    setAccessToken(auth.accessToken);
    setAccessTokenExpiresAt(auth.accessTokenExpiresAt);
    setUser(auth.user);
  }

  function clearAuthSession() {
    setAccessToken('');
    setAccessTokenExpiresAt('');
    setUser(null);
  }

  async function refreshAuthSession(): Promise<AuthResponse> {
    const response = await fetch(`${apiUrl}/api/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({})
    });

    if (response.status === 401) {
      throw new Error('Unauthorized');
    }

    const data = await readResponseBody(response);

    if (!response.ok) {
      throw new Error(getApiErrorMessage(data, 'Не вдалося відкрити ваш профіль.'));
    }

    const auth = data as AuthResponse;
    applyAuthSession(auth);
    return auth;
  }

  useEffect(() => {
    if (didRestoreAuthRef.current) {
      return;
    }

    didRestoreAuthRef.current = true;

    const params = new URLSearchParams(window.location.search);
    const isGoogleReturn = params.get('google') === 'success';

    if (isGoogleReturn) {
      window.history.replaceState({}, '', '/auth');
    }

    setIsBusy(true);

    refreshAuthSession()
      .then(() => {
        if (isGoogleReturn) {
          setMessage('Вхід через Google завершено. Приємного перегляду.');
          notify('Вхід через Google завершено.', 'success');
        }
      })
      .catch((error) => {
        clearAuthSession();
        if (isGoogleReturn) {
          setMessage('Google повернув вас у CinemaCity. Спробуйте увійти ще раз.');
          notify('Не вдалося завершити Google-вхід.', 'error');
        } else if (error instanceof Error && error.message !== 'Unauthorized') {
          notify('Не вдалося відновити сесію.', 'error');
        }
      })
      .finally(() => {
        setIsBusy(false);
        setIsAuthReady(true);
      });
  }, []);

  useEffect(() => {
    if (!user || !accessTokenExpiresAt) {
      return;
    }

    const expiresAt = new Date(accessTokenExpiresAt).getTime();
    const refreshInMs = Math.max(expiresAt - Date.now() - 60_000, 5_000);

    const refreshTimer = window.setTimeout(() => {
      refreshAuthSession().catch((error) => {
        if (error instanceof Error && error.message === 'Unauthorized') {
          clearAuthSession();
          notify('Сесію завершено. Увійдіть ще раз.', 'error');
          return;
        }

        notify('Не вдалося оновити сесію.', 'error');
      });
    }, refreshInMs);

    return () => window.clearTimeout(refreshTimer);
  }, [accessTokenExpiresAt, user]);

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

    const data = await readResponseBody(response);

    if (!response.ok) {
      throw new Error(getApiErrorMessage(data));
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
      const auth = await request<AuthResponse>('/api/auth/confirm-email', {
        method: 'POST',
        body: JSON.stringify({ token: confirmationToken })
      });

      setConfirmationToken('');
      applyAuthSession(auth);
      setMode('login');
      setMessage('Пошту підтверджено. Ви увійшли в CinemaCity.');
      notify('Пошту підтверджено. Ви увійшли.', 'success');
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

      applyAuthSession(result);
      setMessage('Ви увійшли в CinemaCity.');
      notify('Ви увійшли в CinemaCity.', 'success');
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Не вдалося увійти.';
      setMessage(errorMessage);
      notify(errorMessage, 'error');
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

      clearAuthSession();
      setMessage('Ви вийшли з акаунта.');
      notify('Ви вийшли з акаунта.', 'info');
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Не вдалося вийти.';
      setMessage(errorMessage);
      notify(errorMessage, 'error');
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <ToastContext.Provider value={{ notify }}>
      <HeaderAuthContext.Provider value={{ user, isBusy: isBusy || !isAuthReady, handleLogout }}>
        <BrowserRouter>
          <Routes>
            <Route path="/" element={<HomePage accessToken={accessToken} user={user} />} />
            <Route path="/catalog" element={<CatalogPage />} />
            <Route path="/search" element={<SearchPage />} />
            <Route path="/profile" element={!isAuthReady ? <AuthGate /> : user && accessToken ? <ProfilePage accessToken={accessToken} user={user} /> : <Navigate to="/auth" replace />} />
            <Route path="/admin" element={!isAuthReady ? <AuthGate /> : user && accessToken && Number(user.role) === 1 ? <AdminPage accessToken={accessToken} /> : <Navigate to="/auth" replace />} />
            <Route path="/movies/:id" element={<MovieDetailsPage accessToken={accessToken} user={user} />} />
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
                  accessToken={accessToken}
                  user={user}
                  username={username}
                />
              }
            />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
          <ToastViewport toasts={toasts} />
        </BrowserRouter>
      </HeaderAuthContext.Provider>
    </ToastContext.Provider>
  );
}

function HomePage({ accessToken, user }: { accessToken: string; user: AuthUser | null }) {
  const [activeSlide, setActiveSlide] = useState(0);
  const [recommendations, setRecommendations] = useState<MovieCard[]>([]);
  const [popularNow, setPopularNow] = useState<MovieCard[]>([]);
  const currentSlide = spotlightSlides[activeSlide];

  useEffect(() => {
    const timer = window.setInterval(() => {
      setActiveSlide((slide) => (slide + 1) % spotlightSlides.length);
    }, 6000);

    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    const authHeaders: HeadersInit = accessToken ? { Authorization: `Bearer ${accessToken}` } : {};

    Promise.all([
      fetch(`${apiUrl}/api/home/recommendations?limit=10`, {
        signal: controller.signal,
        headers: authHeaders
      }).then(async (response) => (response.ok ? ((await response.json()) as HomeRailResponse).items : [])),
      fetch(`${apiUrl}/api/home/popular-now?days=7&limit=10`, { signal: controller.signal }).then(async (response) =>
        response.ok ? ((await response.json()) as HomeRailResponse).items : []
      )
    ])
      .then(([recommendationItems, popularItems]) => {
        setRecommendations(recommendationItems);
        setPopularNow(popularItems);
      })
      .catch((error) => {
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }

        setRecommendations([]);
        setPopularNow([]);
      });

    return () => controller.abort();
  }, [accessToken]);

  function goToSlide(slide: number) {
    setActiveSlide((slide + spotlightSlides.length) % spotlightSlides.length);
  }

  return (
    <main className="page-shell">
      <SiteHeader />

      <section className="hero" aria-label="Головна сторінка CinemaCity">
        <div className="hero-media" style={{ backgroundImage: `url(${currentSlide.imageUrl})` }}>
          <div className="hero-copy">
            <p className="kicker">Кіносервіс CinemaCity</p>
            <h1>{currentSlide.title}</h1>
            <p className="hero-meta">{currentSlide.meta}</p>
            <p className="hero-text">{currentSlide.description}</p>
            <div className="hero-actions">
              <Link className="button primary" to="/catalog">
                Відкрити каталог
              </Link>
              <Link className="button subtle" to="/search">
                Пошук
              </Link>
            </div>
          </div>

          <div className="hero-controls" aria-label="Керування добірками">
            {spotlightSlides.map((slide, index) => (
              <button
                className={index === activeSlide ? 'active' : ''}
                key={slide.title}
                type="button"
                onClick={() => goToSlide(index)}
                aria-label={`Показати добірку: ${slide.title}`}
              >
                <img src={slide.imageUrl} alt="" loading={index === 0 ? 'eager' : 'lazy'} />
                <span>{slide.meta}</span>
              </button>
            ))}
          </div>
        </div>
      </section>

      <section className="quick-grid" aria-label="Швидкі переходи">
        <Link to="/catalog?sort=rating">
          <span>01</span>
          <strong>Найкращі рейтинги</strong>
          <small>Фільми та серіали, що вже зібрали високі оцінки.</small>
        </Link>
        <Link to="/catalog?contentType=Movie">
          <span>02</span>
          <strong>Фільми</strong>
          <small>Повнометражні історії для вечора, вихідних або списку “пізніше”.</small>
        </Link>
        <Link to="/catalog?contentType=Series">
          <span>03</span>
          <strong>Серіали</strong>
          <small>Проєкти, до яких можна повертатися сезон за сезоном.</small>
        </Link>
      </section>

      <section className="home-insight-band" aria-label="Підбірки CinemaCity">
        <div>
          <p className="kicker">Підбір без хаосу</p>
          <h2>{user ? `Сьогодні для ${user.username}` : 'Сьогодні в CinemaCity'}</h2>
          <p>
            Рекомендації збираються з жанрів, які ви нещодавно дивилися або оцінювали. Акторські збіги враховуються обережно, щоб не забивати жанровий смак.
          </p>
        </div>
        <Link className="button subtle" to={user ? '/profile?tab=lists' : '/auth'}>
          {user ? 'Мої списки' : 'Увійти для персоналізації'}
        </Link>
      </section>

      <HomeMovieRail
        title={user ? 'Рекомендації для вас' : 'Рекомендації'}
        description="Фільми та серіали на основі жанрових збігів і невеликого акторського сигналу."
        items={recommendations}
      />

      <HomeMovieRail
        title="Популярно зараз"
        description="Назви, які отримали найбільше оцінок за останні 7 днів."
        items={popularNow}
      />

      <SiteFooter />
    </main>
  );
}

function HomeMovieRail({ title, description, items }: { title: string; description: string; items: MovieCard[] }) {
  if (!items.length) {
    return null;
  }

  return (
    <section className="home-rail" aria-label={title}>
      <div className="home-rail-head">
        <div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        <Link className="button subtle" to="/catalog">
          Каталог
        </Link>
      </div>
      <MovieCarousel items={items} label={title} />
    </section>
  );
}

function MovieCarousel({ items, label }: { items: MovieCard[]; label: string }) {
  const scrollerRef = useRef<HTMLDivElement | null>(null);

  function scroll(direction: -1 | 1) {
    const scroller = scrollerRef.current;
    if (!scroller) return;

    scroller.scrollBy({ left: direction * Math.max(scroller.clientWidth * 0.82, 260), behavior: 'smooth' });
  }

  return (
    <div className="movie-carousel" aria-label={label}>
      <button className="rail-arrow left" type="button" onClick={() => scroll(-1)} aria-label="Прокрутити ліворуч">
        ‹
      </button>
      <div className="home-rail-scroll" ref={scrollerRef}>
        {items.map((movie) => (
          <MovieCardLink key={movie.id} movie={movie} />
        ))}
      </div>
      <button className="rail-arrow right" type="button" onClick={() => scroll(1)} aria-label="Прокрутити праворуч">
        ›
      </button>
    </div>
  );
}

function SiteHeader() {
  const location = useLocation();
  const navigate = useNavigate();
  const { user, isBusy, handleLogout } = useContext(HeaderAuthContext);
  const params = new URLSearchParams(location.search);
  const initialScope = params.get('scope') as SearchScope | null;
  const [searchQuery, setSearchQuery] = useState(location.pathname === '/search' ? params.get('q') ?? '' : '');
  const [searchScope, setSearchScope] = useState<SearchScope>(
    initialScope && ['all', 'movie', 'series', 'episode', 'person'].includes(initialScope) ? initialScope : 'all'
  );
  const [theme, setTheme] = useState<ThemeMode>(() => getInitialTheme());
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);

  useEffect(() => {
    applyTheme(theme);
    window.localStorage.setItem('cinemacity-theme', theme);
  }, [theme]);

  useEffect(() => {
    setIsUserMenuOpen(false);

    if (location.pathname !== '/search') {
      return;
    }

    const currentParams = new URLSearchParams(location.search);
    const currentScope = currentParams.get('scope') as SearchScope | null;
    setSearchQuery(currentParams.get('q') ?? '');
    setSearchScope(currentScope && ['all', 'movie', 'series', 'episode', 'person'].includes(currentScope) ? currentScope : 'all');
  }, [location.pathname, location.search]);

  function handleHeaderSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmedQuery = searchQuery.trim();

    if (!trimmedQuery) {
      navigate('/catalog');
      return;
    }

    const searchParams = new URLSearchParams({
      q: trimmedQuery,
      scope: searchScope
    });

    navigate(`/search?${searchParams}`);
  }

  return (
    <header className="site-header" aria-label="Головне меню">
      <Link className="brand-row" to="/">
        <span className="brand-mark">CC</span>
        <span>CinemaCity</span>
      </Link>

      <form className="header-search" role="search" onSubmit={handleHeaderSearch}>
        <label className="sr-only" htmlFor="header-search-scope">
          Тип пошуку
        </label>
        <select id="header-search-scope" value={searchScope} onChange={(event) => setSearchScope(event.target.value as SearchScope)}>
          <option value="all">Усе</option>
          <option value="movie">Фільми</option>
          <option value="series">Серіали</option>
          <option value="episode">Епізоди</option>
          <option value="person">Персони</option>
        </select>
        <label className="sr-only" htmlFor="header-search-query">
          Пошук
        </label>
        <input
          id="header-search-query"
          value={searchQuery}
          onChange={(event) => setSearchQuery(event.target.value)}
          placeholder="Назва, епізод або ім’я"
        />
        <button className="icon-button" type="submit" aria-label="Пошук">
          <span aria-hidden="true">⌕</span>
        </button>
      </form>

      <nav className="header-nav" aria-label="Розділи сайту">
        <NavLink to="/catalog">Каталог</NavLink>
        {user && Number(user.role) === 1 ? <NavLink to="/admin">Адмін</NavLink> : null}
        {user ? (
          <div className="user-menu">
            <button className="user-menu-trigger" type="button" onClick={() => setIsUserMenuOpen((value) => !value)} aria-expanded={isUserMenuOpen}>
              <span>{user.avatarUrl ? <img src={user.avatarUrl} alt="" /> : getInitials(user.username)}</span>
              <strong>{user.username}</strong>
              <small aria-hidden="true">⌄</small>
            </button>
            {isUserMenuOpen ? (
              <div className="user-menu-popover">
                <Link to="/profile">Профіль</Link>
                <Link to="/profile?tab=watchlist">Хочу переглянути</Link>
                <Link to="/profile?tab=watched">Переглянуто</Link>
                <button disabled={isBusy} type="button" onClick={handleLogout}>
                  Вийти
                </button>
              </div>
            ) : null}
          </div>
        ) : isBusy ? (
          <button className="nav-status" disabled type="button">
            Перевіряємо
          </button>
        ) : (
          <NavLink to="/auth">Увійти</NavLink>
        )}
      </nav>

      <button
        className="theme-toggle"
        type="button"
        onClick={() => setTheme((current) => (current === 'dark' ? 'light' : 'dark'))}
        aria-label={theme === 'dark' ? 'Увімкнути світлу тему' : 'Увімкнути темну тему'}
      >
        <span aria-hidden="true">{theme === 'dark' ? '☾' : '☀'}</span>
      </button>
    </header>
  );
}
function SiteFooter() {
  return (
    <footer className="site-footer">
      <Link className="brand-row" to="/">
        <span className="brand-mark">CC</span>
        <span>CinemaCity</span>
      </Link>
      <nav aria-label="Нижнє меню">
        <Link to="/catalog">Каталог</Link>
        <Link to="/search">Пошук</Link>
      </nav>
    </footer>
  );
}

function ToastViewport({ toasts }: { toasts: ToastMessage[] }) {
  return (
    <div className="toast-viewport" aria-live="polite" aria-label="Сповіщення">
      {toasts.map((toast) => (
        <div className={`toast ${toast.tone}`} key={toast.id}>
          {toast.message}
        </div>
      ))}
    </div>
  );
}

type AuthPageProps = {
  accessToken: string;
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
  accessToken,
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
  const navigate = useNavigate();

  useEffect(() => {
    if (user) {
      navigate('/', { replace: true });
    }
  }, [navigate, user]);

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
            <p>
              Профіль збере оцінки, списки перегляду та майбутні рецензії, щойно каталог буде повністю наповнений.
            </p>
          </div>
        </div>

        <section className="auth-surface" aria-label="Вхід у CinemaCity">
          <div>
            <p className="eyebrow">Ваш профіль</p>
            <h2>{mode === 'register' ? 'Створити акаунт' : 'Увійти в акаунт'}</h2>
            <p className="auth-lead">{status}</p>
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

          {mode === 'login' ? (
            <form className="auth-form" onSubmit={handleLogin}>
              <label>
                Email
                <input value={email} onChange={(event) => setEmail(event.target.value)} placeholder="name@example.com" type="email" />
              </label>
              <label>
                Пароль
                <input value={password} onChange={(event) => setPassword(event.target.value)} placeholder="Ваш пароль" type="password" />
              </label>
              <button className="primary-action" disabled={isBusy} type="submit">
                Увійти
              </button>
            </form>
          ) : (
            <form className="auth-form" onSubmit={handleRegisterSubmit}>
              <label>
                Ім’я
                <input value={username} onChange={(event) => setUsername(event.target.value)} placeholder="cinema_fan" />
              </label>
              <label>
                Email
                <input value={email} onChange={(event) => setEmail(event.target.value)} placeholder="name@example.com" type="email" />
              </label>
              <label>
                Пароль
                <input
                  ref={registerPasswordRef}
                  className={passwordError ? 'input-error' : ''}
                  value={password}
                  onChange={(event) => handlePasswordChange(event.target.value)}
                  placeholder="Мінімум 8 символів"
                  type="password"
                />
                {passwordError ? <span className="field-error">{passwordError}</span> : null}
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
          )}

          <a className="google-link" href={googleLoginUrl}>
            Увійти через Google
          </a>

          {confirmationToken ? (
            <div className="confirm-strip">
              <p>Демо-токен підтвердження отримано. Натисніть кнопку, щоб активувати акаунт.</p>
              <button disabled={isBusy} type="button" onClick={handleConfirmEmail}>
                Підтвердити пошту
              </button>
            </div>
          ) : null}

          {message ? (
            <output className="message" aria-live="polite">
              {message}
            </output>
          ) : null}

        </section>
      </section>

      <SiteFooter />
    </main>
  );
}

function AdminPage({ accessToken }: { accessToken: string }) {
  const location = useLocation();
  const activeTab = new URLSearchParams(location.search).get('tab') ?? 'movies';

  return (
    <main className="page-shell admin-page-shell">
      <SiteHeader />

      <section className="content-shell admin-page" aria-label="Адмін-панель CinemaCity">
        <PageHeading
          kicker="Адмін-панель"
          title="Керування CinemaCity"
          description="Оновлюйте каталог, жанри, персони, медіа та модерацію з одного робочого простору."
        />

        <nav className="profile-tabs admin-tabs" aria-label="Розділи адмін-панелі">
          <Link className={activeTab === 'movies' ? 'active' : ''} to="/admin">
            Фільми
          </Link>
          <Link className={activeTab === 'genres' ? 'active' : ''} to="/admin?tab=genres">
            Жанри
          </Link>
          <Link className={activeTab === 'persons' ? 'active' : ''} to="/admin?tab=persons">
            Персони
          </Link>
          <Link className={activeTab === 'reviews' ? 'active' : ''} to="/admin?tab=reviews">
            Рецензії
          </Link>
        </nav>

        {activeTab === 'genres' ? <AdminGenresPanel accessToken={accessToken} /> : null}
        {activeTab === 'persons' ? <AdminPersonsPanel accessToken={accessToken} /> : null}
        {activeTab === 'reviews' ? <AdminReviewsPanel accessToken={accessToken} /> : null}
        {activeTab === 'movies' ? <AdminMoviesPanel accessToken={accessToken} /> : null}
      </section>

      <SiteFooter />
    </main>
  );
}

function AdminMoviesPanel({ accessToken }: { accessToken: string }) {
  const { notify } = useToast();
  const [movies, setMovies] = useState<MovieCard[]>([]);
  const [genres, setGenres] = useState<AdminGenre[]>([]);
  const [editingId, setEditingId] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [form, setForm] = useState({
    title: '',
    originalTitle: '',
    contentType: 'Movie',
    releaseYear: new Date().getFullYear().toString(),
    duration: '',
    description: '',
    posterUrl: '',
    backdropUrl: '',
    trailerYoutubeId: '',
    genreIds: [] as string[]
  });

  useEffect(() => {
    loadAdminMovies();
    loadAdminGenres();
  }, [accessToken]);

  async function loadAdminMovies() {
    setIsLoading(true);

    try {
      const response = await fetch(`${apiUrl}/api/movies?page=1&pageSize=50&sort=newest`);
      if (!response.ok) {
        throw new Error('Не вдалося завантажити фільми.');
      }

      const result = (await response.json()) as PagedResult<MovieCard>;
      setMovies(result.items);
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося завантажити фільми.', 'error');
    } finally {
      setIsLoading(false);
    }
  }

  async function loadAdminGenres() {
    try {
      const result = await authedRequest<AdminGenre[]>('/api/admin/genres', accessToken);
      setGenres(result);
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося завантажити жанри.', 'error');
    }
  }

  function updateField(field: keyof typeof form, value: string) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function toggleGenre(id: number) {
    const value = id.toString();
    setForm((current) => ({
      ...current,
      genreIds: current.genreIds.includes(value)
        ? current.genreIds.filter((genreId) => genreId !== value)
        : [...current.genreIds, value]
    }));
  }

  function resetMovieForm() {
    setEditingId('');
    setForm({
      title: '',
      originalTitle: '',
      contentType: 'Movie',
      releaseYear: new Date().getFullYear().toString(),
      duration: '',
      description: '',
      posterUrl: '',
      backdropUrl: '',
      trailerYoutubeId: '',
      genreIds: []
    });
  }

  async function startEditMovie(movieId: string) {
    try {
      const movie = await fetchJson<MovieDetails>(`/api/movies/${movieId}`);
      setEditingId(movie.id);
      setForm({
        title: movie.title,
        originalTitle: movie.originalTitle ?? '',
        contentType: movie.contentType === 1 || movie.contentType === 'Series' ? 'Series' : 'Movie',
        releaseYear: movie.releaseYear.toString(),
        duration: movie.duration?.toString() ?? '',
        description: movie.description ?? '',
        posterUrl: movie.posterUrl ?? '',
        backdropUrl: movie.backdropUrl ?? '',
        trailerYoutubeId: movie.trailerYoutubeId ?? '',
        genreIds: movie.genres.map((genre) => genre.id.toString())
      });
      notify('Фільм відкрито для редагування.', 'info');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося відкрити фільм.', 'error');
    }
  }

  async function submitMovie(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);

    const payload = {
      title: form.title.trim(),
      originalTitle: form.originalTitle.trim() || null,
      contentType: form.contentType,
      releaseYear: Number(form.releaseYear),
      duration: form.duration ? Number(form.duration) : null,
      description: form.description.trim() || null,
      posterUrl: form.posterUrl.trim() || null,
      backdropUrl: form.backdropUrl.trim() || null,
      trailerYoutubeId: form.trailerYoutubeId.trim() || null,
      genreIds: form.genreIds.map(Number),
      countryIds: [],
      languageIds: [],
      cast: []
    };

    try {
      if (editingId) {
        await authedRequest(`/api/admin/movies/${editingId}`, accessToken, {
          method: 'PUT',
          body: JSON.stringify(payload)
        });
        notify('Фільм оновлено.', 'success');
      } else {
        await authedRequest('/api/admin/movies', accessToken, {
          method: 'POST',
          body: JSON.stringify(payload)
        });
        notify('Фільм додано.', 'success');
      }

      resetMovieForm();
      await loadAdminMovies();
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося зберегти фільм.', 'error');
    } finally {
      setIsSaving(false);
    }
  }

  async function deleteMovie(movieId: string) {
    try {
      await authedRequest(`/api/admin/movies/${movieId}`, accessToken, { method: 'DELETE' });
      setMovies((current) => current.filter((movie) => movie.id !== movieId));
      if (editingId === movieId) {
        resetMovieForm();
      }
      notify('Фільм приховано з каталогу.', 'success');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося видалити фільм.', 'error');
    }
  }

  async function uploadMovieImage(file: File | undefined, target: 'posterUrl' | 'backdropUrl') {
    if (!file) {
      return;
    }

    try {
      const upload = await uploadImage('/api/admin/uploads/poster', accessToken, file);
      updateField(target, upload.url);
      notify(target === 'posterUrl' ? 'Постер завантажено.' : 'Фон завантажено.', 'success');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося завантажити зображення.', 'error');
    }
  }

  return (
    <section className="admin-workspace">
      <form className="admin-form" onSubmit={submitMovie}>
        <div className="admin-form-head">
          <div>
            <p className="eyebrow">{editingId ? 'Редагування' : 'Новий запис'}</p>
            <h2>{editingId ? 'Оновити фільм або серіал' : 'Додати фільм або серіал'}</h2>
          </div>
          {editingId ? (
            <button type="button" onClick={resetMovieForm}>
              Скасувати
            </button>
          ) : null}
        </div>

        <div className="admin-form-grid">
          <label>
            Назва
            <input value={form.title} onChange={(event) => updateField('title', event.target.value)} required maxLength={255} />
          </label>
          <label>
            Оригінальна назва
            <input value={form.originalTitle} onChange={(event) => updateField('originalTitle', event.target.value)} maxLength={255} />
          </label>
          <label>
            Тип
            <select value={form.contentType} onChange={(event) => updateField('contentType', event.target.value)}>
              <option value="Movie">Фільм</option>
              <option value="Series">Серіал</option>
            </select>
          </label>
          <label>
            Рік
            <input min="1888" max="2200" type="number" value={form.releaseYear} onChange={(event) => updateField('releaseYear', event.target.value)} required />
          </label>
          <label>
            Тривалість
            <input min="1" type="number" value={form.duration} onChange={(event) => updateField('duration', event.target.value)} placeholder="хвилини" />
          </label>
          <label>
            YouTube trailer ID
            <input value={form.trailerYoutubeId} onChange={(event) => updateField('trailerYoutubeId', event.target.value)} maxLength={64} />
          </label>
        </div>

        <label>
          Опис
          <textarea value={form.description} onChange={(event) => updateField('description', event.target.value)} />
        </label>

        <div className="admin-form-grid">
          <label>
            URL постера
            <input value={form.posterUrl} onChange={(event) => updateField('posterUrl', event.target.value)} />
          </label>
          <label>
            Завантажити постер
            <input type="file" accept="image/*" onChange={(event) => uploadMovieImage(event.target.files?.[0], 'posterUrl')} />
          </label>
          <label>
            URL фону
            <input value={form.backdropUrl} onChange={(event) => updateField('backdropUrl', event.target.value)} />
          </label>
          <label>
            Завантажити фон
            <input type="file" accept="image/*" onChange={(event) => uploadMovieImage(event.target.files?.[0], 'backdropUrl')} />
          </label>
        </div>

        <div className="admin-check-grid" aria-label="Жанри фільму">
          {genres.map((genre) => (
            <label className="checkbox-row" key={genre.id}>
              <input type="checkbox" checked={form.genreIds.includes(genre.id.toString())} onChange={() => toggleGenre(genre.id)} />
              {genre.name}
            </label>
          ))}
        </div>

        <button className="primary-action" type="submit" disabled={isSaving}>
          {editingId ? 'Зберегти зміни' : 'Додати в каталог'}
        </button>
      </form>

      <section className="admin-list-panel">
        <div className="admin-form-head">
          <div>
            <p className="eyebrow">Каталог</p>
            <h2>Останні записи</h2>
          </div>
          <button type="button" onClick={loadAdminMovies} disabled={isLoading}>
            Оновити
          </button>
        </div>

        <div className="admin-movie-list">
          {movies.map((movie) => (
            <article key={movie.id}>
              <div className="admin-thumb">{movie.posterUrl ? <img src={movie.posterUrl} alt="" /> : <span>{getInitials(movie.title)}</span>}</div>
              <div>
                <strong>{movie.title}</strong>
                <span>
                  {formatContentType(movie.contentType)} · {movie.releaseYear}
                </span>
              </div>
              <button type="button" onClick={() => startEditMovie(movie.id)}>
                Редагувати
              </button>
              <button type="button" onClick={() => deleteMovie(movie.id)}>
                Видалити
              </button>
            </article>
          ))}
        </div>
      </section>
    </section>
  );
}

function AdminGenresPanel({ accessToken }: { accessToken: string }) {
  const { notify } = useToast();
  const [genres, setGenres] = useState<AdminGenre[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');

  useEffect(() => {
    loadGenres();
  }, [accessToken]);

  async function loadGenres() {
    try {
      setGenres(await authedRequest<AdminGenre[]>('/api/admin/genres', accessToken));
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося завантажити жанри.', 'error');
    }
  }

  function resetGenreForm() {
    setEditingId(null);
    setName('');
    setSlug('');
  }

  async function submitGenre(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      const payload = { name, slug: slug || null };
      if (editingId) {
        await authedRequest(`/api/admin/genres/${editingId}`, accessToken, {
          method: 'PUT',
          body: JSON.stringify(payload)
        });
        notify('Жанр оновлено.', 'success');
      } else {
        await authedRequest<AdminGenre>('/api/admin/genres', accessToken, {
          method: 'POST',
          body: JSON.stringify(payload)
        });
        notify('Жанр додано.', 'success');
      }

      resetGenreForm();
      await loadGenres();
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося зберегти жанр.', 'error');
    }
  }

  async function deleteGenre(id: number) {
    try {
      await authedRequest(`/api/admin/genres/${id}`, accessToken, { method: 'DELETE' });
      setGenres((current) => current.filter((genre) => genre.id !== id));
      notify('Жанр видалено.', 'success');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося видалити жанр.', 'error');
    }
  }

  return (
    <section className="admin-workspace">
      <form className="admin-form" onSubmit={submitGenre}>
        <div className="admin-form-head">
          <div>
            <p className="eyebrow">{editingId ? 'Редагування' : 'Новий жанр'}</p>
            <h2>Жанри</h2>
          </div>
          {editingId ? <button type="button" onClick={resetGenreForm}>Скасувати</button> : null}
        </div>
        <label>
          Назва
          <input value={name} onChange={(event) => setName(event.target.value)} required maxLength={100} />
        </label>
        <label>
          Slug
          <input value={slug} onChange={(event) => setSlug(event.target.value)} placeholder="створиться автоматично" maxLength={100} />
        </label>
        <button className="primary-action" type="submit">
          {editingId ? 'Зберегти жанр' : 'Додати жанр'}
        </button>
      </form>

      <section className="admin-list-panel">
        <div className="admin-table">
          {genres.map((genre) => (
            <article key={genre.id}>
              <div>
                <strong>{genre.name}</strong>
                <span>{genre.slug} · {genre.moviesCount} назв</span>
              </div>
              <button type="button" onClick={() => { setEditingId(genre.id); setName(genre.name); setSlug(genre.slug); }}>
                Редагувати
              </button>
              <button type="button" onClick={() => deleteGenre(genre.id)}>
                Видалити
              </button>
            </article>
          ))}
        </div>
      </section>
    </section>
  );
}

function AdminPersonsPanel({ accessToken }: { accessToken: string }) {
  const { notify } = useToast();
  const [persons, setPersons] = useState<AdminPerson[]>([]);
  const [query, setQuery] = useState('');
  const [editingId, setEditingId] = useState('');
  const [form, setForm] = useState({
    fullName: '',
    birthDate: '',
    birthPlace: '',
    bio: '',
    photoUrl: ''
  });

  useEffect(() => {
    loadPersons();
  }, [accessToken]);

  async function loadPersons(search = query) {
    try {
      const params = search.trim() ? `?q=${encodeURIComponent(search.trim())}` : '';
      setPersons(await authedRequest<AdminPerson[]>(`/api/admin/persons${params}`, accessToken));
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося завантажити персон.', 'error');
    }
  }

  function updatePersonField(field: keyof typeof form, value: string) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function resetPersonForm() {
    setEditingId('');
    setForm({ fullName: '', birthDate: '', birthPlace: '', bio: '', photoUrl: '' });
  }

  async function submitPerson(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const payload = {
      fullName: form.fullName.trim(),
      birthDate: form.birthDate || null,
      birthPlace: form.birthPlace.trim() || null,
      bio: form.bio.trim() || null,
      photoUrl: form.photoUrl.trim() || null
    };

    try {
      if (editingId) {
        await authedRequest(`/api/admin/persons/${editingId}`, accessToken, {
          method: 'PUT',
          body: JSON.stringify(payload)
        });
        notify('Персону оновлено.', 'success');
      } else {
        await authedRequest<AdminPerson>('/api/admin/persons', accessToken, {
          method: 'POST',
          body: JSON.stringify(payload)
        });
        notify('Персону додано.', 'success');
      }

      resetPersonForm();
      await loadPersons();
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося зберегти персону.', 'error');
    }
  }

  async function deletePerson(personId: string) {
    try {
      await authedRequest(`/api/admin/persons/${personId}`, accessToken, { method: 'DELETE' });
      setPersons((current) => current.filter((person) => person.id !== personId));
      notify('Персону видалено.', 'success');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося видалити персону.', 'error');
    }
  }

  async function uploadPhoto(file: File | undefined) {
    if (!file) {
      return;
    }

    try {
      const upload = await uploadImage('/api/admin/uploads/photo', accessToken, file);
      updatePersonField('photoUrl', upload.url);
      notify('Фото завантажено.', 'success');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося завантажити фото.', 'error');
    }
  }

  return (
    <section className="admin-workspace">
      <form className="admin-form" onSubmit={submitPerson}>
        <div className="admin-form-head">
          <div>
            <p className="eyebrow">{editingId ? 'Редагування' : 'Нова персона'}</p>
            <h2>Персони</h2>
          </div>
          {editingId ? <button type="button" onClick={resetPersonForm}>Скасувати</button> : null}
        </div>
        <div className="admin-person-preview">
          <div className="person-photo">
            {form.photoUrl ? <img src={form.photoUrl} alt="" /> : <span>{getInitials(form.fullName || 'CC')}</span>}
          </div>
          <label>
            Завантажити фото
            <input type="file" accept="image/*" onChange={(event) => uploadPhoto(event.target.files?.[0])} />
          </label>
        </div>
        <label>
          Ім’я
          <input value={form.fullName} onChange={(event) => updatePersonField('fullName', event.target.value)} required maxLength={255} />
        </label>
        <div className="admin-form-grid">
          <label>
            Дата народження
            <input type="date" value={form.birthDate} onChange={(event) => updatePersonField('birthDate', event.target.value)} />
          </label>
          <label>
            Місце народження
            <input value={form.birthPlace} onChange={(event) => updatePersonField('birthPlace', event.target.value)} maxLength={255} />
          </label>
        </div>
        <label>
          URL фото
          <input value={form.photoUrl} onChange={(event) => updatePersonField('photoUrl', event.target.value)} />
        </label>
        <label>
          Біографія
          <textarea value={form.bio} onChange={(event) => updatePersonField('bio', event.target.value)} />
        </label>
        <button className="primary-action" type="submit">
          {editingId ? 'Зберегти персону' : 'Додати персону'}
        </button>
      </form>

      <section className="admin-list-panel">
        <div className="admin-search-row">
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Пошук персони" />
          <button type="button" onClick={() => loadPersons()}>
            Знайти
          </button>
        </div>
        <div className="admin-table">
          {persons.map((person) => (
            <article key={person.id}>
              <div className="admin-person-row">
                <div className="person-photo">
                  {person.photoUrl ? <img src={person.photoUrl} alt="" /> : <span>{getInitials(person.fullName)}</span>}
                </div>
                <div>
                  <strong>{person.fullName}</strong>
                  <span>{person.birthPlace || 'Місце не вказано'} · {person.creditsCount} робіт</span>
                </div>
              </div>
              <button type="button" onClick={() => {
                setEditingId(person.id);
                setForm({
                  fullName: person.fullName,
                  birthDate: person.birthDate ?? '',
                  birthPlace: person.birthPlace ?? '',
                  bio: person.bio ?? '',
                  photoUrl: person.photoUrl ?? ''
                });
              }}>
                Редагувати
              </button>
              <button type="button" onClick={() => deletePerson(person.id)}>
                Видалити
              </button>
            </article>
          ))}
        </div>
      </section>
    </section>
  );
}

function ProfilePage({ accessToken, user }: { accessToken: string; user: AuthUser }) {
  const location = useLocation();
  const [stats, setStats] = useState<ProfileStats | null>(null);
  const [watchlist, setWatchlist] = useState<WatchlistItem[]>([]);
  const [watched, setWatched] = useState<WatchlistItem[]>([]);
  const [movieLists, setMovieLists] = useState<MovieListSummary[]>([]);
  const [message, setMessage] = useState('');
  const activeTab = new URLSearchParams(location.search).get('tab') ?? 'profile';

  useEffect(() => {
    const controller = new AbortController();
    setMessage('');

    Promise.all([
      authedRequest<ProfileStats>('/api/profile/stats', accessToken, { signal: controller.signal }),
      authedRequest<WatchlistItem[]>('/api/profile/watchlist?status=Watchlist', accessToken, { signal: controller.signal }),
      authedRequest<WatchlistItem[]>('/api/profile/watchlist?status=Watched', accessToken, { signal: controller.signal }),
      authedRequest<MovieListSummary[]>('/api/profile/lists', accessToken, { signal: controller.signal })
    ])
      .then(([statsResult, watchlistResult, watchedResult, listsResult]) => {
        setStats(statsResult);
        setWatchlist(watchlistResult);
        setWatched(watchedResult);
        setMovieLists(listsResult);
      })
      .catch((error) => {
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }

        setMessage(error instanceof Error ? error.message : 'Не вдалося завантажити профіль.');
      });

    return () => controller.abort();
  }, [accessToken]);

  const recentItems = [...watchlist, ...watched]
    .sort((left, right) => new Date(right.updatedAt ?? right.addedAt).getTime() - new Date(left.updatedAt ?? left.addedAt).getTime())
    .slice(0, 8);

  return (
    <main className="page-shell profile-page-shell">
      <SiteHeader />

      <section className="profile-page" aria-label="Профіль користувача">
        <header className="profile-hero-card">
          <div className="profile-avatar-large">
            {user.avatarUrl ? <img src={user.avatarUrl} alt="" /> : <span>{getInitials(user.username)}</span>}
          </div>
          <div>
            <h1>{user.username}</h1>
            <p>{user.email}</p>
          </div>
          <div className="profile-top-stats">
            <article>
              <strong>{stats?.watchedCount ?? 0}</strong>
              <span>Фільмів</span>
            </article>
            <article>
              <strong>{stats?.watchlistCount ?? 0}</strong>
              <span>Хочу переглянути</span>
            </article>
            <article>
              <strong>{stats?.reviewsCount ?? 0}</strong>
              <span>Рецензій</span>
            </article>
            <article>
              <strong>{stats?.ratingsCount ?? 0}</strong>
              <span>Оцінок</span>
            </article>
          </div>
        </header>

        <nav className="profile-tabs" aria-label="Розділи профілю">
          <Link className={activeTab === 'profile' ? 'active' : ''} to="/profile">
            Профіль
          </Link>
          <Link className={activeTab === 'watchlist' ? 'active' : ''} to="/profile?tab=watchlist">
            Хочу переглянути
          </Link>
          <Link className={activeTab === 'watched' ? 'active' : ''} to="/profile?tab=watched">
            Переглянуто
          </Link>
          <Link className={activeTab === 'reviews' ? 'active' : ''} to="/profile?tab=reviews">
            Рецензії
          </Link>
          <Link className={activeTab === 'lists' ? 'active' : ''} to="/profile?tab=lists">
            Списки
          </Link>
        </nav>

        {message ? <StatusMessage tone="danger">{message}</StatusMessage> : null}

        {activeTab === 'watchlist' ? (
          <ProfileMovieSection title="Хочу переглянути" items={watchlist} emptyText="У списку “Хочу переглянути” поки нічого немає." />
        ) : null}

        {activeTab === 'watched' ? (
          <ProfileMovieSection title="Переглянуто" items={watched} emptyText="Список переглянутого поки порожній." />
        ) : null}

        {activeTab === 'reviews' ? (
          <section className="profile-panel">
            <SectionTitle title="Рецензії" />
            <div className="profile-stats-grid">
              <article>
                <strong>{stats?.reviewsCount ?? 0}</strong>
                <span>Усього</span>
              </article>
              <article>
                <strong>{stats?.approvedReviewsCount ?? 0}</strong>
                <span>Опубліковано</span>
              </article>
              <article>
                <strong>{stats?.commentsCount ?? 0}</strong>
                <span>Коментарів</span>
              </article>
            </div>
          </section>
        ) : null}

        {activeTab === 'lists' ? (
          <ProfileListsPanel accessToken={accessToken} lists={movieLists} onListsChanged={setMovieLists} />
        ) : null}

        {activeTab === 'profile' ? (
          <>
            <section className="profile-panel">
              <SectionTitle title="Статистика" />
              <div className="profile-stats-grid">
                <article>
                  <strong>{stats?.watchedCount ?? 0}</strong>
                  <span>Переглянуто</span>
                </article>
                <article>
                  <strong>{stats?.watchlistCount ?? 0}</strong>
                  <span>Хочу переглянути</span>
                </article>
                <article>
                  <strong>{stats?.ratingsCount ?? 0}</strong>
                  <span>Оцінок</span>
                </article>
                <article>
                  <strong>{formatRating(stats?.averageScore ?? 0)}</strong>
                  <span>Середня оцінка</span>
                </article>
              </div>
              {stats?.favoriteGenres.length ? (
                <div className="profile-genres">
                  {stats.favoriteGenres.map((genre) => (
                    <span key={genre}>{genre}</span>
                  ))}
                </div>
              ) : null}
            </section>

            <ProfileMovieSection title="Останні додані" items={recentItems} emptyText="Додавайте фільми у “Хочу переглянути” або переглянуте, і вони з’являться тут." />
          </>
        ) : null}

        {Number(user.role) === 1 ? <AdminReviewsPanel accessToken={accessToken} /> : null}
      </section>

      <SiteFooter />
    </main>
  );
}

function ProfileMovieSection({ title, items, emptyText }: { title: string; items: WatchlistItem[]; emptyText: string }) {
  return (
    <section className="profile-panel">
      <SectionTitle title={title} />
      {items.length ? (
        <div className="profile-poster-grid">
          {items.map((item) => (
            <MovieCardLink key={item.id} movie={{ ...item, id: item.movieId }} />
          ))}
        </div>
      ) : (
        <p className="muted-text">{emptyText}</p>
      )}
    </section>
  );
}

function ProfileListsPanel({
  accessToken,
  lists,
  onListsChanged
}: {
  accessToken: string;
  lists: MovieListSummary[];
  onListsChanged: (lists: MovieListSummary[]) => void;
}) {
  const { notify } = useToast();
  const [selectedListId, setSelectedListId] = useState(lists[0]?.id ?? '');
  const [selectedList, setSelectedList] = useState<MovieListDetails | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isPublic, setIsPublic] = useState(false);
  const [editingId, setEditingId] = useState('');

  useEffect(() => {
    if (!selectedListId && lists.length) {
      setSelectedListId(lists[0].id);
    }
  }, [lists, selectedListId]);

  useEffect(() => {
    if (!selectedListId) {
      setSelectedList(null);
      return;
    }

    const controller = new AbortController();
    authedRequest<MovieListDetails>(`/api/profile/lists/${selectedListId}`, accessToken, { signal: controller.signal })
      .then(setSelectedList)
      .catch((error) => {
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }

        notify(error instanceof Error ? error.message : 'Не вдалося завантажити список.', 'error');
      });

    return () => controller.abort();
  }, [accessToken, selectedListId]);

  async function reloadLists(nextSelectedId?: string) {
    const result = await authedRequest<MovieListSummary[]>('/api/profile/lists', accessToken);
    onListsChanged(result);
    setSelectedListId(nextSelectedId ?? result[0]?.id ?? '');
  }

  function startEditList(list: MovieListSummary) {
    setEditingId(list.id);
    setName(list.name);
    setDescription(list.description ?? '');
    setIsPublic(list.isPublic);
  }

  function resetForm() {
    setEditingId('');
    setName('');
    setDescription('');
    setIsPublic(false);
  }

  async function submitList(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      const payload = { name, description: description || null, isPublic };

      if (editingId) {
        await authedRequest(`/api/profile/lists/${editingId}`, accessToken, {
          method: 'PUT',
          body: JSON.stringify(payload)
        });
        notify('Список оновлено.', 'success');
        await reloadLists(editingId);
      } else {
        const created = await authedRequest<MovieListSummary>('/api/profile/lists', accessToken, {
          method: 'POST',
          body: JSON.stringify(payload)
        });
        notify('Список створено.', 'success');
        await reloadLists(created.id);
      }

      resetForm();
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося зберегти список.', 'error');
    }
  }

  async function deleteList(listId: string) {
    try {
      await authedRequest(`/api/profile/lists/${listId}`, accessToken, { method: 'DELETE' });
      notify('Список видалено.', 'success');
      await reloadLists();
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося видалити список.', 'error');
    }
  }

  async function removeMovieFromList(movieId: string) {
    if (!selectedListId) return;

    try {
      await authedRequest(`/api/profile/lists/${selectedListId}/items/${movieId}`, accessToken, { method: 'DELETE' });
      setSelectedList((current) => (current ? { ...current, items: current.items.filter((item) => item.movieId !== movieId) } : current));
      await reloadLists(selectedListId);
      notify('Фільм прибрано зі списку.', 'success');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Не вдалося прибрати фільм.', 'error');
    }
  }

  return (
    <section className="profile-panel list-manager">
      <form className="list-form" onSubmit={submitList}>
        <div className="admin-form-head">
          <div>
            <p className="eyebrow">{editingId ? 'Редагування' : 'Новий список'}</p>
            <h2>{editingId ? 'Оновити список' : 'Створити список'}</h2>
          </div>
          {editingId ? <button type="button" onClick={resetForm}>Скасувати</button> : null}
        </div>
        <label>
          Назва списку
          <input value={name} onChange={(event) => setName(event.target.value)} maxLength={80} required />
        </label>
        <label>
          Опис
          <textarea value={description} onChange={(event) => setDescription(event.target.value)} maxLength={500} />
        </label>
        <label className="checkbox-row">
          <input type="checkbox" checked={isPublic} onChange={(event) => setIsPublic(event.target.checked)} />
          Публічний список
        </label>
        <button className="primary-action" type="submit">
          {editingId ? 'Зберегти' : 'Створити'}
        </button>
      </form>

      <div className="list-layout">
        <aside className="list-sidebar" aria-label="Ваші списки">
          {lists.length ? (
            lists.map((list) => (
              <button className={selectedListId === list.id ? 'active' : ''} key={list.id} type="button" onClick={() => setSelectedListId(list.id)}>
                <strong>{list.name}</strong>
                <span>
                  {list.itemsCount} назв · {list.isPublic ? 'публічний' : 'приватний'}
                </span>
              </button>
            ))
          ) : (
            <p className="muted-text">Створіть перший список і додавайте в нього фільми або серіали зі сторінки назви.</p>
          )}
        </aside>

        <section className="list-details">
          {selectedList ? (
            <>
              <div className="admin-form-head">
                <div>
                  <h2>{selectedList.name}</h2>
                  {selectedList.description ? <p>{selectedList.description}</p> : null}
                  <p>{selectedList.isPublic ? 'Публічний список' : 'Приватний список'}</p>
                </div>
                <div className="list-actions">
                  <button type="button" onClick={() => startEditList(selectedList)}>
                    Редагувати
                  </button>
                  <button type="button" onClick={() => deleteList(selectedList.id)}>
                    Видалити
                  </button>
                </div>
              </div>
              {selectedList.items.length ? (
                <div className="profile-poster-grid">
                  {selectedList.items.map((item) => (
                    <article className="list-movie-card" key={item.id}>
                      <MovieCardLink movie={{ ...item, id: item.movieId }} />
                      <button type="button" onClick={() => removeMovieFromList(item.movieId)}>
                        Прибрати
                      </button>
                    </article>
                  ))}
                </div>
              ) : (
                <p className="muted-text">У цьому списку поки немає назв.</p>
              )}
            </>
          ) : (
            <p className="muted-text">Оберіть список або створіть новий.</p>
          )}
        </section>
      </div>
    </section>
  );
}

function AdminReviewsPanel({ accessToken }: { accessToken: string }) {
  const { notify } = useToast();
  const [reviews, setReviews] = useState<ReviewResponse[]>([]);
  const [statusFilter, setStatusFilter] = useState('Pending');
  const [isLoading, setIsLoading] = useState(false);
  const [message, setMessage] = useState('');

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    const controller = new AbortController();
    const query = statusFilter === 'all' ? '' : `?status=${statusFilter}`;
    setIsLoading(true);
    setMessage('');

    authedRequest<ReviewResponse[]>(`/api/admin/reviews${query}`, accessToken, { signal: controller.signal })
      .then(setReviews)
      .catch((error) => {
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }

        setMessage(error instanceof Error ? error.message : 'Не вдалося завантажити рецензії для модерації.');
      })
      .finally(() => setIsLoading(false));

    return () => controller.abort();
  }, [accessToken, statusFilter]);

  async function moderateReview(reviewId: string, action: 'approve' | 'reject') {
    setMessage('');

    try {
      await authedRequest<ReviewResponse>(`/api/admin/reviews/${reviewId}/${action}`, accessToken, {
        method: 'PUT',
        body: JSON.stringify(action === 'reject' ? { rejectionReason: 'Не відповідає правилам публікації.' } : {})
      });
      setReviews((current) => current.filter((review) => review.id !== reviewId));
      const successMessage = action === 'approve' ? 'Рецензію опубліковано.' : 'Рецензію відхилено.';
      setMessage(successMessage);
      notify(successMessage, 'success');
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Не вдалося змінити статус рецензії.';
      setMessage(errorMessage);
      notify(errorMessage, 'error');
    }
  }

  return (
    <section className="admin-reviews" aria-label="Модерація рецензій">
      <div className="admin-reviews-head">
        <div>
          <p className="eyebrow">Адмін-панель</p>
          <h3>Модерація рецензій</h3>
        </div>
        <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)} aria-label="Статус рецензій">
          <option value="Pending">Очікують</option>
          <option value="Approved">Опубліковані</option>
          <option value="Rejected">Відхилені</option>
          <option value="all">Усі</option>
        </select>
      </div>

      {isLoading ? <StatusMessage>Завантаження рецензій...</StatusMessage> : null}
      {message ? <StatusMessage>{message}</StatusMessage> : null}

      {reviews.length ? (
        <div className="admin-review-list">
          {reviews.map((review) => (
            <article className="admin-review-card" key={review.id}>
              <header>
                <strong>{review.username}</strong>
                <span>{formatReviewStatus(review.status)}</span>
              </header>
              <p>{review.content}</p>
              {review.rejectionReason ? <small>{review.rejectionReason}</small> : null}
              <footer>
                <button type="button" onClick={() => moderateReview(review.id, 'approve')} disabled={review.status === 'Approved'}>
                  Схвалити
                </button>
                <button type="button" onClick={() => moderateReview(review.id, 'reject')} disabled={review.status === 'Rejected'}>
                  Відхилити
                </button>
              </footer>
            </article>
          ))}
        </div>
      ) : !isLoading ? (
        <p className="muted-text">Немає рецензій для вибраного статусу.</p>
      ) : null}
    </section>
  );
}

function ProfileStatsPanel({ accessToken }: { accessToken: string }) {
  const [stats, setStats] = useState<ProfileStats | null>(null);
  const [watchlist, setWatchlist] = useState<WatchlistItem[]>([]);
  const [watched, setWatched] = useState<WatchlistItem[]>([]);
  const [message, setMessage] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    setMessage('');

    Promise.all([
      authedRequest<ProfileStats>('/api/profile/stats', accessToken, { signal: controller.signal }),
      authedRequest<WatchlistItem[]>('/api/profile/watchlist?status=Watchlist', accessToken, { signal: controller.signal }),
      authedRequest<WatchlistItem[]>('/api/profile/watchlist?status=Watched', accessToken, { signal: controller.signal })
    ])
      .then(([statsResult, watchlistResult, watchedResult]) => {
        setStats(statsResult);
        setWatchlist(watchlistResult);
        setWatched(watchedResult);
      })
      .catch((error) => {
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }

        setMessage(error instanceof Error ? error.message : 'Не вдалося завантажити статистику профілю.');
      });

    return () => controller.abort();
  }, [accessToken]);

  return (
    <section className="profile-dashboard" aria-label="Статистика профілю">
      <div>
        <p className="eyebrow">Статистика</p>
        <h3>Ваш кіно-профіль</h3>
      </div>

      {message ? <StatusMessage tone="danger">{message}</StatusMessage> : null}

      {stats ? (
        <>
          <div className="profile-stats-grid">
            <article>
              <strong>{stats.watchlistCount}</strong>
              <span>Хочу переглянути</span>
            </article>
            <article>
              <strong>{stats.watchedCount}</strong>
              <span>Переглянуто</span>
            </article>
            <article>
              <strong>{stats.ratingsCount}</strong>
              <span>Оцінок</span>
            </article>
            <article>
              <strong>{formatRating(stats.averageScore)}</strong>
              <span>Середня оцінка</span>
            </article>
            <article>
              <strong>{stats.approvedReviewsCount}/{stats.reviewsCount}</strong>
              <span>Рецензій</span>
            </article>
            <article>
              <strong>{stats.commentsCount}</strong>
              <span>Коментарів</span>
            </article>
          </div>

          {stats.favoriteGenres.length ? (
            <div className="profile-genres">
              {stats.favoriteGenres.map((genre) => (
                <span key={genre}>{genre}</span>
              ))}
            </div>
          ) : null}
        </>
      ) : null}

      <ProfileMovieRail title="Хочу переглянути" items={watchlist} />
      <ProfileMovieRail title="Переглянуто" items={watched} />
    </section>
  );
}

function ProfileMovieRail({ title, items }: { title: string; items: WatchlistItem[] }) {
  return (
    <div className="profile-rail">
      <h4>{title}</h4>
      {items.length ? (
        <MovieCarousel items={items.slice(0, 8).map((item) => ({ ...item, id: item.movieId }))} label={title} />
      ) : (
        <p className="muted-text">Поки порожньо.</p>
      )}
    </div>
  );
}
function SearchPage() {
  const location = useLocation();
  const [results, setResults] = useState<SearchResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const params = new URLSearchParams(location.search);
  const query = params.get('q')?.trim() ?? '';
  const scope = params.get('scope') ?? 'all';

  useEffect(() => {
    if (!query) {
      setResults(null);
      setError('');
      return;
    }

    const controller = new AbortController();
    const searchParams = new URLSearchParams({ q: query, scope });
    setIsLoading(true);
    setError('');

    fetch(`${apiUrl}/api/search?${searchParams}`, { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error('Не вдалося виконати пошук.');
        }

        return (await response.json()) as SearchResponse;
      })
      .then(setResults)
      .catch((fetchError) => {
        if (fetchError instanceof DOMException && fetchError.name === 'AbortError') {
          return;
        }

        setError(
          fetchError instanceof TypeError
            ? 'Пошук тимчасово недоступний. Спробуйте оновити сторінку пізніше.'
            : fetchError instanceof Error
              ? fetchError.message
              : 'Не вдалося виконати пошук.'
        );
      })
      .finally(() => setIsLoading(false));

    return () => controller.abort();
  }, [query, scope]);

  const totalResults = (results?.movies.length ?? 0) + (results?.episodes.length ?? 0) + (results?.people.length ?? 0);

  return (
    <main className="page-shell">
      <SiteHeader />

      <section className="content-shell" aria-label="Результати пошуку">
        <PageHeading
          kicker="Пошук CinemaCity"
          title={query ? `Результати для “${query}”` : 'Почніть пошук у верхній панелі'}
          description="Знаходьте фільми, серіали, епізоди та людей з каталогу."
          stat={query && results ? `${totalResults} результатів` : undefined}
        />

        {isLoading ? <StatusMessage>Шукаємо...</StatusMessage> : null}
        {error ? <StatusMessage tone="danger">{error}</StatusMessage> : null}

        {!isLoading && query && results && totalResults === 0 ? (
          <EmptyState title="Нічого не знайдено" text="Спробуйте іншу назву або змініть тип пошуку у хедері." />
        ) : null}

        {results?.movies.length ? (
          <section className="result-section">
            <SectionTitle title="Фільми та серіали" />
            <div className="movie-grid compact">
              {results.movies.map((movie) => (
                <MovieCardLink
                  key={movie.id}
                  movie={{
                    id: movie.id,
                    title: movie.title,
                    originalTitle: movie.originalTitle,
                    contentType: movie.contentType,
                    releaseYear: movie.releaseYear,
                    posterUrl: movie.posterUrl,
                    backdropUrl: null,
                    averageRating: movie.averageRating,
                    ratingsCount: 0,
                    genres: []
                  }}
                />
              ))}
            </div>
          </section>
        ) : null}

        {results?.episodes.length ? (
          <section className="result-section">
            <SectionTitle title="Епізоди" />
            <div className="result-list">
              {results.episodes.map((episode) => (
                <Link key={episode.id} to={`/movies/${episode.seriesId}`}>
                  <strong>{episode.title}</strong>
                  <span>
                    {episode.seriesTitle} · Сезон {episode.seasonNumber}, епізод {episode.episodeNumber}
                  </span>
                  <small>Рейтинг {formatRating(episode.averageRating)}</small>
                </Link>
              ))}
            </div>
          </section>
        ) : null}

        {results?.people.length ? (
          <section className="result-section">
            <SectionTitle title="Персони" />
            <div className="people-grid">
              {results.people.map((person) => (
                <article key={person.id}>
                  <div className="person-photo">
                    {person.photoUrl ? <img src={person.photoUrl} alt="" loading="lazy" /> : <span>{getInitials(person.fullName)}</span>}
                  </div>
                  <strong>{person.fullName}</strong>
                  {person.birthPlace ? <small>{person.birthPlace}</small> : null}
                </article>
              ))}
            </div>
          </section>
        ) : null}
      </section>

      <SiteFooter />
    </main>
  );
}

function CatalogPage() {
  const location = useLocation();
  const locationParams = new URLSearchParams(location.search);
  const [movies, setMovies] = useState<PagedResult<MovieCard> | null>(null);
  const [genres, setGenres] = useState<Genre[]>([]);
  const [query, setQuery] = useState(locationParams.get('q') ?? '');
  const [genre, setGenre] = useState(locationParams.get('genre') ?? '');
  const [contentType, setContentType] = useState(locationParams.get('contentType') ?? '');
  const [sort, setSort] = useState(locationParams.get('sort') ?? 'rating');
  const [yearFrom, setYearFrom] = useState(locationParams.get('yearFrom') ?? '');
  const [ratingMin, setRatingMin] = useState(locationParams.get('ratingMin') ?? '');
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

        setError(
          fetchError instanceof TypeError
            ? 'Каталог тимчасово недоступний. Спробуйте оновити сторінку пізніше.'
            : fetchError instanceof Error
              ? fetchError.message
              : 'Не вдалося завантажити каталог.'
        );
      })
      .finally(() => setIsLoading(false));

    return () => controller.abort();
  }, [contentType, genre, page, query, ratingMin, sort, yearFrom]);

  const pageCount = movies ? Math.max(Math.ceil(movies.total / movies.pageSize), 1) : 1;
  const hasFilters = Boolean(query || genre || contentType || yearFrom || ratingMin || sort !== 'rating');

  function resetPage(update: () => void) {
    setPage(1);
    update();
  }

  function resetFilters() {
    setQuery('');
    setGenre('');
    setContentType('');
    setSort('rating');
    setYearFrom('');
    setRatingMin('');
    setPage(1);
  }

  return (
    <main className="page-shell">
      <SiteHeader />

      <section className="content-shell" aria-label="Каталог фільмів">
        <PageHeading
          kicker="Каталог CinemaCity"
          title="Фільми та серіали"
          description="Шукайте за назвою, фільтруйте за жанром, роком, рейтингом і типом контенту."
          stat={movies ? `${movies.total} назв` : undefined}
        />

        <div className="catalog-layout">
          <aside className="filter-panel" aria-label="Фільтри каталогу">
            <div className="filter-panel-head">
              <h2>Фільтри</h2>
              <button disabled={!hasFilters} type="button" onClick={resetFilters}>
                Скинути
              </button>
            </div>

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
            <div className="filter-row">
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
            </div>
            <div className="filter-row">
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
          </aside>

          <div className="catalog-results">
            {error ? <StatusMessage tone="danger">{error}</StatusMessage> : null}
            {isLoading ? <StatusMessage>Завантаження каталогу...</StatusMessage> : null}

            {!isLoading && movies?.items.length === 0 ? (
              <EmptyState title="Каталог порожній" text="Додайте фільми в адмінському розділі або змініть фільтри." />
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
          </div>
        </div>
      </section>

      <SiteFooter />
    </main>
  );
}

function MovieDetailsPage({ accessToken, user }: { accessToken: string; user: AuthUser | null }) {
  const { id } = useParams();
  const [movie, setMovie] = useState<MovieDetails | null>(null);
  const [similar, setSimilar] = useState<MovieCard[]>([]);
  const [seasons, setSeasons] = useState<SeasonDetails[]>([]);
  const [reviews, setReviews] = useState<ReviewResponse[]>([]);
  const [commentsByReview, setCommentsByReview] = useState<Record<string, CommentResponse[]>>({});
  const [commentInputs, setCommentInputs] = useState<Record<string, string>>({});
  const [userState, setUserState] = useState<MovieUserState | null>(null);
  const [customLists, setCustomLists] = useState<MovieListSummary[]>([]);
  const [isListModalOpen, setIsListModalOpen] = useState(false);
  const [selectedModalListIds, setSelectedModalListIds] = useState<string[]>([]);
  const [newModalListName, setNewModalListName] = useState('');
  const [newModalListIsPublic, setNewModalListIsPublic] = useState(true);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [refreshTick, setRefreshTick] = useState(0);
  const [error, setError] = useState('');
  const [actionMessage, setActionMessage] = useState('');
  const [movieRating, setMovieRating] = useState('8');
  const [ratingDraft, setRatingDraft] = useState('8');
  const [isRatingModalOpen, setIsRatingModalOpen] = useState(false);
  const [episodeRatings, setEpisodeRatings] = useState<Record<string, string>>({});
  const [activeSeasonId, setActiveSeasonId] = useState('');
  const [selectedEpisodeId, setSelectedEpisodeId] = useState('');
  const [reviewContent, setReviewContent] = useState('');
  const [reviewSpoiler, setReviewSpoiler] = useState(false);
  const [editingReviewId, setEditingReviewId] = useState('');
  const [editingReviewContent, setEditingReviewContent] = useState('');
  const [editingReviewSpoiler, setEditingReviewSpoiler] = useState(false);

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
      }),
      fetch(`${apiUrl}/api/movies/${id}/reviews`, { signal: controller.signal }).then(async (response) => {
        if (!response.ok) return [];
        return (await response.json()) as ReviewResponse[];
      }),
      accessToken
        ? authedRequest<MovieUserState>(`/api/profile/movies/${id}/state`, accessToken, { signal: controller.signal }).catch(() => null)
        : Promise.resolve(null)
    ])
      .then(async ([movieResult, similarResult, reviewResult, stateResult]) => {
        const seasonResult =
          formatContentType(movieResult.contentType) === 'Серіал'
            ? await fetch(`${apiUrl}/api/movies/${id}/seasons`, { signal: controller.signal }).then(async (response) => {
                if (!response.ok) return [];
                return (await response.json()) as SeasonDetails[];
              })
            : [];

        const commentsResult = await Promise.all(
          reviewResult.map(async (review) => {
            const comments = await fetch(`${apiUrl}/api/reviews/${review.id}/comments`, { signal: controller.signal }).then(async (response) => {
              if (!response.ok) return [];
              return (await response.json()) as CommentResponse[];
            });

            return [review.id, comments] as const;
          })
        );

        setMovie(movieResult);
        setSimilar(similarResult);
        setReviews(reviewResult);
        setSeasons(seasonResult);
        setCommentsByReview(Object.fromEntries(commentsResult));
        setUserState(stateResult);
        setMovieRating(stateResult?.rating?.score ? String(stateResult.rating.score) : '8');
        setRatingDraft(stateResult?.rating?.score ? String(stateResult.rating.score) : '8');
        setActiveSeasonId(seasonResult[0]?.id || '');
        setSelectedEpisodeId(seasonResult[0]?.episodes[0]?.id || '');
      })
      .catch((fetchError) => {
        if (fetchError instanceof DOMException && fetchError.name === 'AbortError') {
          return;
        }

        setError(
          fetchError instanceof TypeError
            ? 'Сторінка фільму тимчасово недоступна. Спробуйте оновити її пізніше.'
            : fetchError instanceof Error
              ? fetchError.message
              : 'Не вдалося відкрити сторінку фільму.'
        );
      })
      .finally(() => setIsLoading(false));

    return () => controller.abort();
  }, [accessToken, id, refreshTick]);

  useEffect(() => {
    if (!accessToken || !user) {
      setCustomLists([]);
      return;
    }

    const controller = new AbortController();
    authedRequest<MovieListSummary[]>('/api/profile/lists', accessToken, { signal: controller.signal })
      .then(setCustomLists)
      .catch(() => setCustomLists([]));

    return () => controller.abort();
  }, [accessToken, user]);

  async function submitRating(target: { movieId?: string; episodeId?: string }, scoreValue: string) {
    if (!accessToken) {
      setActionMessage('Щоб поставити оцінку, увійдіть в акаунт.');
      return false;
    }

    const score = Number(scoreValue);

    if (!Number.isFinite(score) || score < 1 || score > 10) {
      setActionMessage('Оцінка має бути від 1 до 10.');
      return false;
    }

    setIsSubmitting(true);
    setActionMessage('');

    try {
      const existingMovieRatingId = target.movieId ? userState?.rating?.id : undefined;
      const response = await authedRequest<RatingResponse>(existingMovieRatingId ? `/api/ratings/${existingMovieRatingId}` : '/api/ratings', accessToken, {
        method: existingMovieRatingId ? 'PUT' : 'POST',
        body: JSON.stringify(existingMovieRatingId ? { score } : { ...target, score })
      });
      setUserState((current) => (target.movieId ? { ...(current ?? {}), rating: { id: response.id, score: response.score } } : current));
      if (target.movieId) {
        setMovieRating(String(response.score));
        setRatingDraft(String(response.score));
      }
      setRefreshTick((value) => value + 1);
      setActionMessage('Оцінку збережено.');
      return true;
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося зберегти оцінку.');
      return false;
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitMovieRatingFromModal() {
    if (!movie) return;

    const saved = await submitRating({ movieId: movie.id }, ratingDraft);
    if (saved) {
      setIsRatingModalOpen(false);
    }
  }

  function openListModal() {
    if (!movie) return;

    if (!accessToken || !user) {
      setActionMessage('Щоб додати фільм у власний список, увійдіть в акаунт.');
      return;
    }

    setSelectedModalListIds(userState?.lists?.map((list) => list.id) ?? []);
    setNewModalListName('');
    setNewModalListIsPublic(true);
    setIsListModalOpen(true);
  }

  async function toggleWatchlist() {
    if (!movie) return;

    if (!accessToken || !user) {
      setActionMessage('Щоб додати фільм у “Хочу переглянути”, увійдіть в акаунт.');
      return;
    }

    setIsSubmitting(true);
    setActionMessage('');

    try {
      const currentStatus = normalizeWatchlistStatus(userState?.watchlistStatus);

      if (currentStatus === 'Watchlist') {
        await authedRequest(`/api/profile/watchlist/${movie.id}`, accessToken, { method: 'DELETE' });
        setUserState((current) => ({ ...(current ?? {}), watchlistStatus: null, addedAt: null }));
        setActionMessage('Видалено з “Хочу переглянути”.');
        return;
      }

      const watchlistItem = await authedRequest<WatchlistItem>('/api/profile/watchlist', accessToken, {
        method: 'POST',
        body: JSON.stringify({ movieId: movie.id, status: 0 })
      });
      setUserState((current) => ({ ...(current ?? {}), watchlistStatus: watchlistItem.status, addedAt: watchlistItem.addedAt }));
      setActionMessage('Додано в “Хочу переглянути”.');
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося оновити “Хочу переглянути”.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function createModalList() {
    if (!accessToken) {
      setActionMessage('Щоб створити список, увійдіть в акаунт.');
      return;
    }

    const trimmedName = newModalListName.trim();
    if (!trimmedName) {
      setActionMessage('Вкажіть назву списку.');
      return;
    }

    try {
      const created = await authedRequest<MovieListSummary>('/api/profile/lists', accessToken, {
        method: 'POST',
        body: JSON.stringify({ name: trimmedName, description: null, isPublic: newModalListIsPublic })
      });

      setCustomLists((current) => [created, ...current]);
      setSelectedModalListIds((current) => [...new Set([...current, created.id])]);
      setNewModalListName('');
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося створити список.');
    }
  }

  async function saveListModal() {
    if (!accessToken || !movie) {
      return;
    }

    setIsSubmitting(true);
    setActionMessage('');

    try {
      const existingIds = new Set(userState?.lists?.map((list) => list.id) ?? []);
      const selectedIds = new Set(selectedModalListIds);
      const selectedMemberships = customLists
        .filter((list) => selectedIds.has(list.id))
        .map((list) => ({ id: list.id, name: list.name }));

      if (!selectedMemberships.length) {
        setActionMessage('Оберіть власний список або створіть новий.');
        return;
      }

      await Promise.all(
        selectedModalListIds
          .filter((listId) => !existingIds.has(listId))
          .map((listId) =>
            authedRequest(`/api/profile/lists/${listId}/items`, accessToken, {
              method: 'POST',
              body: JSON.stringify({ movieId: movie.id })
            })
          )
      );

      await Promise.all(
        [...existingIds]
          .filter((listId) => !selectedIds.has(listId))
          .map((listId) => authedRequest(`/api/profile/lists/${listId}/items/${movie.id}`, accessToken, { method: 'DELETE' }))
      );

      setUserState((current) => ({
        ...(current ?? {}),
        lists: selectedMemberships
      }));
      setCustomLists((current) =>
        current.map((list) => {
          const wasSelected = existingIds.has(list.id);
          const isSelected = selectedIds.has(list.id);

          if (wasSelected === isSelected) return list;

          return {
            ...list,
            itemsCount: Math.max(list.itemsCount + (isSelected ? 1 : -1), 0)
          };
        })
      );
      setActionMessage(`Додано у списки: ${selectedMemberships.map((list) => `“${list.name}”`).join(', ')}.`);
      setIsListModalOpen(false);
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося оновити списки.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitReview(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!movie) return;

    if (!accessToken) {
      setActionMessage('Щоб написати рецензію, увійдіть в акаунт.');
      return;
    }

    setIsSubmitting(true);
    setActionMessage('');

    try {
      await authedRequest<ReviewResponse>('/api/reviews', accessToken, {
        method: 'POST',
        body: JSON.stringify({ movieId: movie.id, content: reviewContent, isSpoiler: reviewSpoiler })
      });
      setReviewContent('');
      setReviewSpoiler(false);
      setRefreshTick((value) => value + 1);
      setActionMessage('Рецензію відправлено на модерацію.');
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося відправити рецензію.');
    } finally {
      setIsSubmitting(false);
    }
  }

  function startEditReview(review: ReviewResponse) {
    setEditingReviewId(review.id);
    setEditingReviewContent(review.content);
    setEditingReviewSpoiler(review.isSpoiler);
  }

  async function saveReviewEdit(reviewId: string) {
    if (!accessToken) return;

    setIsSubmitting(true);
    setActionMessage('');

    try {
      await authedRequest<ReviewResponse>(`/api/reviews/${reviewId}`, accessToken, {
        method: 'PUT',
        body: JSON.stringify({ content: editingReviewContent, isSpoiler: editingReviewSpoiler })
      });
      setEditingReviewId('');
      setRefreshTick((value) => value + 1);
      setActionMessage('Рецензію оновлено.');
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося оновити рецензію.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function deleteReview(reviewId: string) {
    if (!accessToken) return;

    setIsSubmitting(true);
    setActionMessage('');

    try {
      await authedRequest(`/api/reviews/${reviewId}`, accessToken, { method: 'DELETE' });
      setRefreshTick((value) => value + 1);
      setActionMessage('Рецензію видалено.');
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося видалити рецензію.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function likeReview(reviewId: string) {
    if (!accessToken) {
      setActionMessage('Щоб лайкнути рецензію, увійдіть в акаунт.');
      return;
    }

    try {
      const response = await authedRequest<ReviewResponse>(`/api/reviews/${reviewId}/like`, accessToken, { method: 'POST' });
      setReviews((current) => current.map((review) => (review.id === reviewId ? { ...review, likesCount: response.likesCount } : review)));
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося лайкнути рецензію.');
    }
  }

  async function submitComment(reviewId: string) {
    if (!accessToken) {
      setActionMessage('Щоб коментувати, увійдіть в акаунт.');
      return;
    }

    const content = commentInputs[reviewId]?.trim() ?? '';
    if (!content) return;

    try {
      const comment = await authedRequest<CommentResponse>(`/api/reviews/${reviewId}/comments`, accessToken, {
        method: 'POST',
        body: JSON.stringify({ content })
      });
      setCommentInputs((current) => ({ ...current, [reviewId]: '' }));
      setCommentsByReview((current) => ({ ...current, [reviewId]: [...(current[reviewId] ?? []), comment] }));
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося додати коментар.');
    }
  }

  async function deleteComment(reviewId: string, commentId: string) {
    if (!accessToken) return;

    try {
      await authedRequest(`/api/comments/${commentId}`, accessToken, { method: 'DELETE' });
      setCommentsByReview((current) => ({ ...current, [reviewId]: (current[reviewId] ?? []).filter((comment) => comment.id !== commentId) }));
    } catch (requestError) {
      setActionMessage(requestError instanceof Error ? requestError.message : 'Не вдалося видалити коментар.');
    }
  }

  const currentWatchlistStatus = normalizeWatchlistStatus(userState?.watchlistStatus);
  const selectedModalLists = customLists.filter((list) => selectedModalListIds.includes(list.id));
  const activeSeason = seasons.find((season) => season.id === activeSeasonId) ?? seasons[0] ?? null;
  const selectedEpisode = activeSeason?.episodes.find((episode) => episode.id === selectedEpisodeId) ?? activeSeason?.episodes[0] ?? null;
  const seasonAverageRating = activeSeason?.episodes.length
    ? activeSeason.episodes.reduce((sum, episode) => sum + Number(episode.averageRating), 0) / activeSeason.episodes.length
    : 0;

  return (
    <main className="page-shell">
      <SiteHeader />

      {isLoading ? (
        <section className="content-shell">
          <StatusMessage>Завантаження фільму...</StatusMessage>
        </section>
      ) : null}
      {error ? (
        <section className="content-shell">
          <StatusMessage tone="danger">{error}</StatusMessage>
        </section>
      ) : null}

      {movie ? (
        <section className="movie-details" aria-label={`Сторінка “${movie.title}”`}>
          <div className="movie-page-head">
            <div>
              <h1>{movie.title}</h1>
              <p>
                {movie.releaseYear} · {formatContentType(movie.contentType)}{movie.duration ? ` · ${formatDuration(movie.duration)}` : ''}
              </p>
            </div>
            <div className="movie-scorebar" aria-label="Оцінки">
              <article>
                <span>СЕРЕДНІЙ РЕЙТИНГ</span>
                <strong>★ {formatRating(movie.averageRating)}<small>/10</small></strong>
                <small>{movie.ratingsCount} оцінок</small>
              </article>
              <article>
                <span>ВАША ОЦІНКА</span>
                {user ? (
                  <button
                    className="rating-open-button"
                    disabled={isSubmitting}
                    type="button"
                    onClick={() => {
                      setRatingDraft(movieRating);
                      setIsRatingModalOpen(true);
                    }}
                  >
                    <strong>{userState?.rating ? `★ ${formatRating(userState.rating.score)}/10` : 'Оцінити'}</strong>
                    <small>{userState?.rating ? 'Змінити оцінку' : 'Відкрити вікно оцінки'}</small>
                  </button>
                ) : (
                  <Link to="/auth">★ Увійти</Link>
                )}
              </article>
              <article>
                <span>СПИСОК</span>
                {user ? (
                  <button className="score-action-button" disabled={isSubmitting} type="button" onClick={toggleWatchlist}>
                    <strong>{currentWatchlistStatus === 'Watchlist' ? '✓ Хочу переглянути' : 'Додати'}</strong>
                    <small>{currentWatchlistStatus === 'Watchlist' ? 'Натисніть, щоб прибрати' : 'У “Хочу переглянути”'}</small>
                  </button>
                ) : (
                  <Link to="/auth">+ Увійти</Link>
                )}
              </article>
            </div>
          </div>

          <div className="movie-media-layout">
            <div className="movie-poster-large">
              <button
                className={userState?.lists?.length ? 'active' : ''}
                disabled={isSubmitting}
                type="button"
                onClick={openListModal}
                aria-label="Відкрити власні списки"
              >
                {userState?.lists?.length ? '✓' : '+'}
              </button>
              {movie.posterUrl ? <img src={movie.posterUrl} alt="" /> : <span>{getInitials(movie.title)}</span>}
            </div>

            <div className="movie-trailer-panel">
              {movie.trailerYoutubeId ? (
                <iframe
                  title={`Трейлер ${movie.title}`}
                  src={`https://www.youtube.com/embed/${movie.trailerYoutubeId}`}
                  allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
                  allowFullScreen
                />
              ) : movie.backdropUrl ? (
                <img src={movie.backdropUrl} alt="" />
              ) : (
                <div>{getInitials(movie.title)}</div>
              )}
              <div className="trailer-caption">
                <strong>{movie.trailerYoutubeId ? 'Трейлер' : 'Промо-кадр'}</strong>
                <span>{movie.description || 'Опис для цієї назви ще не додано.'}</span>
              </div>
            </div>
          </div>

          <div className="movie-genre-strip">
            {movie.genres.map((genre) => (
              <span key={genre.id}>{genre.name}</span>
            ))}
          </div>

          {actionMessage ? <StatusMessage>{actionMessage}</StatusMessage> : null}

          <div className="details-grid">
            <section>
              <SectionTitle title="Деталі" />
              <p className="movie-description-panel">{movie.description || 'Опис для цієї назви ще не додано.'}</p>
              <TagList title="Країни" items={movie.countries.map((item) => item.name)} />
              <TagList title="Мови" items={movie.languages.map((item) => item.name)} />
            </section>

            <section>
              <SectionTitle title="Акторський склад" />
              {movie.cast.length ? (
                <div className="cast-list">
                  {movie.cast.map((cast) => (
                    <article key={cast.id}>
                      <div className="cast-photo">
                        {cast.photoUrl ? <img src={cast.photoUrl} alt="" loading="lazy" /> : <span>{getInitials(cast.fullName)}</span>}
                      </div>
                      <div>
                        <strong>{cast.fullName}</strong>
                        <span>
                          {formatCreditType(cast.creditType)}
                          {cast.roleName ? ` · ${cast.roleName}` : ''}
                        </span>
                      </div>
                    </article>
                  ))}
                </div>
              ) : (
                <p className="muted-text">Склад ще не додано.</p>
              )}
            </section>
          </div>

          {formatContentType(movie.contentType) === 'Серіал' ? (
            <section className="series-section episode-browser">
              <div className="episode-browser-head">
                <SectionTitle title="Епізоди" />
                <span aria-hidden="true">›</span>
              </div>
              {seasons.length ? (
                <div className="episode-browser-body">
                  <div className="season-tabs" aria-label="Сезони">
                    <span>Сезон</span>
                    {seasons.map((season) => (
                      <button
                        className={season.id === activeSeason?.id ? 'active' : ''}
                        key={season.id}
                        type="button"
                        onClick={() => {
                          setActiveSeasonId(season.id);
                          setSelectedEpisodeId(season.episodes[0]?.id || '');
                        }}
                      >
                        S{season.seasonNumber}
                      </button>
                    ))}
                  </div>

                  {activeSeason ? (
                    <>
                      <p className="season-summary">
                        S{activeSeason.seasonNumber} Епізоди · ★ {formatRating(seasonAverageRating)} середня · з{' '}
                        {activeSeason.episodes.reduce((sum, episode) => sum + episode.ratingsCount, 0)} оцінок епізодів
                      </p>

                      {activeSeason.episodes.length ? (
                        <>
                          <div
                            className="episode-rating-chart"
                            style={{ '--season-average': String(Math.max(0, Math.min(10, seasonAverageRating))) } as React.CSSProperties}
                            aria-label={`Рейтинги епізодів сезону ${activeSeason.seasonNumber}`}
                          >
                            <div className="season-average-line" />
                            {activeSeason.episodes.map((episode) => {
                              const rating = Math.max(0, Math.min(10, Number(episode.averageRating)));

                              return (
                                <button
                                  className={episode.id === selectedEpisode?.id ? 'active' : ''}
                                  key={episode.id}
                                  style={{ '--episode-rating': String(rating) } as React.CSSProperties}
                                  type="button"
                                  onClick={() => setSelectedEpisodeId(episode.id)}
                                  aria-label={`Епізод ${episode.episodeNumber}, рейтинг ${formatRating(episode.averageRating)}`}
                                >
                                  <strong>{formatRating(episode.averageRating)}</strong>
                                  <span>E{episode.episodeNumber}</span>
                                </button>
                              );
                            })}
                          </div>

                          {selectedEpisode ? (
                            <article className="episode-detail-card">
                              <div className="episode-still">
                                {movie.backdropUrl ? <img src={movie.backdropUrl} alt="" loading="lazy" /> : <span>{getInitials(selectedEpisode.title)}</span>}
                              </div>
                              <div className="episode-detail-copy">
                                <span>
                                  S{activeSeason.seasonNumber}.E{selectedEpisode.episodeNumber} ·{' '}
                                  {selectedEpisode.airDate ? formatDate(selectedEpisode.airDate) : 'Дата не вказана'}
                                </span>
                                <h3>{selectedEpisode.title}</h3>
                                <p>{selectedEpisode.description || 'Опис серії ще не додано.'}</p>
                                <strong>★ {formatRating(selectedEpisode.averageRating)} ({selectedEpisode.ratingsCount})</strong>
                                {user ? (
                                  <div className="episode-star-rating">
                                    <span>Ваша оцінка:</span>
                                    <StarRatingInput
                                      value={Number(episodeRatings[selectedEpisode.id] ?? '8')}
                                      onChange={(value) => setEpisodeRatings((current) => ({ ...current, [selectedEpisode.id]: String(value) }))}
                                    />
                                    <button
                                      className="episode-rate-submit"
                                      disabled={isSubmitting}
                                      type="button"
                                      onClick={() => submitRating({ episodeId: selectedEpisode.id }, episodeRatings[selectedEpisode.id] ?? '8')}
                                    >
                                      Оцінити
                                    </button>
                                  </div>
                                ) : (
                                  <Link className="button subtle" to="/auth">
                                    Увійдіть, щоб оцінити серію
                                  </Link>
                                )}
                              </div>
                            </article>
                          ) : null}
                        </>
                      ) : (
                        <p className="muted-text">Епізоди ще не додано.</p>
                      )}
                    </>
                  ) : null}
                </div>
              ) : (
                <p className="muted-text">Сезони для цього серіалу ще не додано.</p>
              )}
            </section>
          ) : null}

          <section className="reviews-section">
            <SectionTitle title="Рецензії та коментарі" />
            {user ? (
              <form className="review-form" onSubmit={submitReview}>
                <label>
                  Ваша рецензія
                  <textarea
                    minLength={10}
                    maxLength={4000}
                    value={reviewContent}
                    onChange={(event) => setReviewContent(event.target.value)}
                    placeholder="Поділіться думкою про фільм"
                  />
                </label>
                <label className="checkbox-row">
                  <input type="checkbox" checked={reviewSpoiler} onChange={(event) => setReviewSpoiler(event.target.checked)} />
                  Є спойлери
                </label>
                <button className="primary-action" disabled={isSubmitting} type="submit">
                  Відправити на модерацію
                </button>
              </form>
            ) : (
              <Link className="button subtle" to="/auth">
                Увійдіть, щоб написати рецензію або коментар
              </Link>
            )}

            {reviews.length ? (
              <div className="review-list">
                {reviews.map((review) => {
                  const canEdit = user && (user.id === review.userId || Number(user.role) === 1);
                  const isEditing = editingReviewId === review.id;
                  const comments = commentsByReview[review.id] ?? [];

                  return (
                    <article className="review-card" key={review.id}>
                      <header>
                        <strong>{review.username}</strong>
                        <span>{formatDate(review.createdAt)}</span>
                      </header>
                      {review.isSpoiler ? <small>Містить спойлери</small> : null}
                      {isEditing ? (
                        <div className="review-edit">
                          <textarea value={editingReviewContent} onChange={(event) => setEditingReviewContent(event.target.value)} />
                          <label className="checkbox-row">
                            <input type="checkbox" checked={editingReviewSpoiler} onChange={(event) => setEditingReviewSpoiler(event.target.checked)} />
                            Є спойлери
                          </label>
                          <div>
                            <button disabled={isSubmitting} type="button" onClick={() => saveReviewEdit(review.id)}>
                              Зберегти
                            </button>
                            <button disabled={isSubmitting} type="button" onClick={() => setEditingReviewId('')}>
                              Скасувати
                            </button>
                          </div>
                        </div>
                      ) : (
                        <p>{review.content}</p>
                      )}
                      <footer>
                        <button type="button" onClick={() => likeReview(review.id)}>
                          ♥ {review.likesCount}
                        </button>
                        {canEdit && !isEditing ? (
                          <>
                            <button type="button" onClick={() => startEditReview(review)}>
                              Редагувати
                            </button>
                            <button type="button" onClick={() => deleteReview(review.id)}>
                              Видалити
                            </button>
                          </>
                        ) : null}
                      </footer>

                      <div className="comment-list">
                        {comments.map((comment) => (
                          <div className="comment-card" key={comment.id}>
                            <div>
                              <strong>{comment.username}</strong>
                              <span>{formatDate(comment.createdAt)}</span>
                            </div>
                            <p>{comment.content}</p>
                            {user && (user.id === comment.userId || Number(user.role) === 1) ? (
                              <button type="button" onClick={() => deleteComment(review.id, comment.id)}>
                                Видалити
                              </button>
                            ) : null}
                          </div>
                        ))}
                      </div>

                      {user ? (
                        <div className="comment-form">
                          <input
                            value={commentInputs[review.id] ?? ''}
                            onChange={(event) => setCommentInputs((current) => ({ ...current, [review.id]: event.target.value }))}
                            placeholder="Напишіть коментар"
                          />
                          <button type="button" onClick={() => submitComment(review.id)}>
                            Надіслати
                          </button>
                        </div>
                      ) : null}
                    </article>
                  );
                })}
              </div>
            ) : (
              <p className="muted-text">Опублікованих рецензій поки немає.</p>
            )}
          </section>

          <section className="similar-section">
            <SectionTitle title="Схожі фільми" />
            {similar.length ? (
              <MovieCarousel items={similar} label="Схожі фільми" />
            ) : (
              <p className="muted-text">Рекомендації з’являться, коли в каталозі буде більше назв зі схожими жанрами.</p>
            )}
          </section>
        </section>
      ) : null}

      {movie && isRatingModalOpen ? (
        <div className="rating-modal-backdrop" role="presentation" onMouseDown={() => setIsRatingModalOpen(false)}>
          <div className="rating-modal" role="dialog" aria-modal="true" aria-labelledby="rating-modal-title" onMouseDown={(event) => event.stopPropagation()}>
            <button className="rating-modal-close" type="button" onClick={() => setIsRatingModalOpen(false)} aria-label="Закрити оцінювання">
              ×
            </button>
            <div className="rating-modal-star" aria-hidden="true">
              ?
            </div>
            <p className="kicker">Оцініть це</p>
            <h2 id="rating-modal-title">{movie.title}</h2>
            <StarRatingInput value={Number(ratingDraft)} onChange={(value) => setRatingDraft(String(value))} />
            <button className="rating-modal-submit" disabled={isSubmitting} type="button" onClick={submitMovieRatingFromModal}>
              Оцінити
            </button>
          </div>
        </div>
      ) : null}

      {movie && isListModalOpen ? (
        <div className="list-modal-backdrop" role="presentation" onMouseDown={() => setIsListModalOpen(false)}>
          <div className="list-modal" role="dialog" aria-modal="true" aria-labelledby="list-modal-title" onMouseDown={(event) => event.stopPropagation()}>
            <header>
              <div>
                <p className="kicker">Списки</p>
                <h2 id="list-modal-title">Додати “{movie.title}”</h2>
              </div>
              <button type="button" onClick={() => setIsListModalOpen(false)} aria-label="Закрити списки">
                ×
              </button>
            </header>

            <section className="list-modal-create" aria-label="Створити список">
              <button type="button" onClick={createModalList} disabled={!newModalListName.trim() || isSubmitting}>
                + Новий список
              </button>
              <input value={newModalListName} onChange={(event) => setNewModalListName(event.target.value)} maxLength={80} placeholder="Назва нового списку" />
              <div className="segmented-control" aria-label="Видимість списку">
                <button className={newModalListIsPublic ? 'active' : ''} type="button" onClick={() => setNewModalListIsPublic(true)}>
                  Публічний
                </button>
                <button className={!newModalListIsPublic ? 'active' : ''} type="button" onClick={() => setNewModalListIsPublic(false)}>
                  Приватний
                </button>
              </div>
            </section>

            <div className="list-modal-options">
              {customLists.map((list) => (
                <label className="list-modal-option" key={list.id}>
                  <input
                    type="checkbox"
                    checked={selectedModalListIds.includes(list.id)}
                    onChange={(event) =>
                      setSelectedModalListIds((current) =>
                        event.target.checked ? [...current, list.id] : current.filter((selectedId) => selectedId !== list.id)
                      )
                    }
                  />
                  <span>
                    <strong>{list.name}</strong>
                    <small>
                      {list.isPublic ? 'Публічний' : 'Приватний'} · {list.itemsCount} назв
                    </small>
                  </span>
                </label>
              ))}
              {!customLists.length ? <p className="list-modal-empty">Створіть власний список, щоб додати сюди фільм.</p> : null}
            </div>

            <footer>
              <span>{selectedModalLists.length ? `Буде додано у: ${selectedModalLists.map((list) => list.name).join(', ')}` : 'Оберіть власний список'}</span>
              <button className="primary-action" disabled={isSubmitting || !selectedModalListIds.length} type="button" onClick={saveListModal}>
                Додати
              </button>
            </footer>
          </div>
        </div>
      ) : null}

      <SiteFooter />
    </main>
  );
}

function MovieCardLink({ movie }: { movie: MovieCard }) {
  const rating = formatRating(movie.averageRating);

  return (
    <Link className="movie-card" to={`/movies/${movie.id}`}>
      <div className="movie-card-media">
        <span className="movie-card-watchmark" aria-hidden="true">
          +
        </span>
        {movie.posterUrl || movie.backdropUrl ? (
          <img src={movie.posterUrl || movie.backdropUrl || ''} alt="" loading="lazy" />
        ) : (
          <span>{getInitials(movie.title)}</span>
        )}
      </div>
      <div className="movie-card-body">
        <div className="movie-card-rating" aria-label={`Рейтинг ${rating} з 10`}>
          <span>★ {rating}</span>
          <span aria-hidden="true">☆</span>
        </div>
        <h3>{movie.title}</h3>
        <p>
          {formatContentType(movie.contentType)} · {movie.releaseYear} · {movie.ratingsCount} оцінок
        </p>
      </div>
    </Link>
  );
}

function PageHeading({ kicker, title, description, stat }: { kicker: string; title: string; description: string; stat?: string }) {
  return (
    <div className="page-heading">
      <div>
        <p className="kicker">{kicker}</p>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {stat ? <strong>{stat}</strong> : null}
    </div>
  );
}

function SectionTitle({ title }: { title: string }) {
  return <h2 className="section-title">{title}</h2>;
}

function StarRatingInput({ value, onChange }: { value: number; onChange: (value: number) => void }) {
  const normalizedValue = Math.max(1, Math.min(10, Math.round(value || 0)));
  const [hoveredValue, setHoveredValue] = useState<number | null>(null);
  const displayValue = hoveredValue ?? normalizedValue;

  return (
    <div className="star-rating" aria-label="Оцінка фільму" onMouseLeave={() => setHoveredValue(null)}>
      {Array.from({ length: 10 }, (_, index) => {
        const score = index + 1;

        return (
          <button
            className={score <= displayValue ? 'active' : ''}
            key={score}
            type="button"
            onBlur={() => setHoveredValue(null)}
            onFocus={() => setHoveredValue(score)}
            onMouseEnter={() => setHoveredValue(score)}
            onClick={() => onChange(score)}
            aria-label={`${score} з 10`}
          >
            ★
          </button>
        );
      })}
      <strong>{displayValue}/10</strong>
    </div>
  );
}

function StatusMessage({ children, tone = 'info' }: { children: React.ReactNode; tone?: 'info' | 'danger' }) {
  return <p className={`status-message ${tone}`}>{children}</p>;
}

function EmptyState({ title, text }: { title: string; text: string }) {
  return (
    <div className="empty-state">
      <h2>{title}</h2>
      <p>{text}</p>
    </div>
  );
}

function AuthGate() {
  return <EmptyState title="Відновлюємо сесію" text="Зараз повернемо вас в акаунт." />;
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

function getInitialTheme(): ThemeMode {
  if (typeof window === 'undefined') {
    return 'dark';
  }

  const stored = window.localStorage.getItem('cinemacity-theme');
  if (stored === 'dark' || stored === 'light') {
    applyTheme(stored);
    return stored;
  }

  const preferred: ThemeMode = window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  applyTheme(preferred);
  return preferred;
}

function applyTheme(theme: ThemeMode) {
  if (typeof document === 'undefined') {
    return;
  }

  document.documentElement.dataset.theme = theme;
  document.documentElement.classList.toggle('dark', theme === 'dark');
  document.documentElement.classList.toggle('light', theme === 'light');
}

function getInitials(value: string) {
  return value
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');
}

function formatRating(value: number) {
  return Number(value || 0).toFixed(1);
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('uk-UA', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(value));
}

function formatReviewStatus(value?: number | string) {
  if (value === 1 || value === 'Approved') return 'Опубліковано';
  if (value === 2 || value === 'Rejected') return 'Відхилено';
  return 'Очікує';
}

function normalizeWatchlistStatus(value?: WatchlistStatus | null) {
  if (value === 1 || value === 'Watched') return 'Watched';
  if (value === 0 || value === 'Watchlist') return 'Watchlist';
  return null;
}

function formatWatchlistStatus(value?: string | null) {
  if (value === 'Watched') return 'Переглянуто';
  if (value === 'Watchlist') return 'Хочу переглянути';
  return 'Додати';
}

function formatDuration(value: number) {
  const hours = Math.floor(value / 60);
  const minutes = value % 60;

  if (!hours) {
    return `${minutes} хв`;
  }

  return `${hours} год ${minutes} хв`;
}

async function fetchJson<T>(path: string): Promise<T> {
  const response = await fetch(`${apiUrl}${path}`);
  const data = await readResponseBody(response);

  if (!response.ok) {
    throw new Error(getApiErrorMessage(data));
  }

  return data as T;
}

async function authedRequest<T>(path: string, accessToken: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`${apiUrl}${path}`, {
    ...options,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
      ...options.headers
    }
  });

  const data = await readResponseBody(response);

  if (!response.ok) {
    throw new Error(getApiErrorMessage(data));
  }

  return data as T;
}

async function uploadImage(path: string, accessToken: string, file: File): Promise<UploadResponse> {
  const body = new FormData();
  body.append('file', file);

  const response = await fetch(`${apiUrl}${path}`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      Authorization: `Bearer ${accessToken}`
    },
    body
  });

  const data = await readResponseBody(response);

  if (!response.ok) {
    throw new Error(getApiErrorMessage(data));
  }

  return data as UploadResponse;
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

async function readResponseBody(response: Response): Promise<unknown> {
  const text = await response.text();

  if (!text.trim()) {
    return null;
  }

  try {
    return JSON.parse(text);
  } catch {
    if (!response.ok) {
      const message = text.trim().startsWith('<') ? 'Сервер повернув сторінку помилки замість JSON.' : text;
      return { message };
    }

    throw new Error('Сервер повернув відповідь у неочікуваному форматі.');
  }
}

function getApiErrorMessage(data: unknown, fallback = 'Щось пішло не так. Спробуйте ще раз.') {
  if (data && typeof data === 'object') {
    const record = data as Record<string, unknown>;
    const message = record.message ?? record.Message ?? record.detail ?? record.title;

    if (typeof message === 'string') {
      return toFriendlyMessage(message);
    }
  }

  return fallback;
}

function toFriendlyMessage(message?: string) {
  if (message?.includes('relation "user_movie_lists" does not exist')) {
    return 'База даних не оновлена: застосуйте міграції або перезапустіть API.';
  }

  if (message && (message.includes('Npgsql.') || message.includes('PostgresException') || message.includes(' at Microsoft.'))) {
    return 'Сервер повернув технічну помилку. Перевірте міграції бази даних і логи API.';
  }

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
    case 'List name already exists.':
      return 'Список із такою назвою вже існує.';
    case 'List was not found.':
      return 'Список не знайдено.';
    case 'List name is required and must be at most 80 characters.':
      return 'Назва списку обов’язкова і має бути до 80 символів.';
    case 'List description must be at most 500 characters.':
      return 'Опис списку має бути до 500 символів.';
    default:
      return message || 'Щось пішло не так. Спробуйте ще раз.';
  }
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
