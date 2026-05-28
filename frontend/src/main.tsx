import React, { FormEvent, useEffect, useMemo, useState } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Link, Navigate, Route, Routes } from 'react-router-dom';
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
  const [username, setUsername] = useState('cinefan');
  const [email, setEmail] = useState('cinefan@example.com');
  const [password, setPassword] = useState('Password123');
  const [confirmPassword, setConfirmPassword] = useState('Password123');
  const [confirmationToken, setConfirmationToken] = useState('');
  const [accessToken, setAccessToken] = useState('');
  const [user, setUser] = useState<AuthUser | null>(null);
  const [message, setMessage] = useState('Раді бачити вас у CinemaCity.');
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

    try {
      const result = await request<RegisterResponse>('/api/auth/register', {
        method: 'POST',
        body: JSON.stringify({ username, email, password, confirmPassword })
      });

      setConfirmationToken(result.emailConfirmationToken);
      setMessage('Акаунт створено. Підтвердьте пошту кнопкою нижче, а потім увійдіть.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Не вдалося створити акаунт.');
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
              setConfirmPassword={setConfirmPassword}
              setEmail={setEmail}
              setMode={setMode}
              setPassword={setPassword}
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
  setConfirmPassword: (value: string) => void;
  setEmail: (value: string) => void;
  setMode: (value: 'register' | 'login') => void;
  setPassword: (value: string) => void;
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
  setConfirmPassword,
  setEmail,
  setMode,
  setPassword,
  setUsername,
  status,
  user,
  username
}: AuthPageProps) {
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
          </div>

          <div className="tabs" role="tablist" aria-label="Режим автентифікації">
            <button
              className={mode === 'login' ? 'active' : ''}
              type="button"
              onClick={() => setMode('login')}
            >
              Вхід
            </button>
            <button
              className={mode === 'register' ? 'active' : ''}
              type="button"
              onClick={() => setMode('register')}
            >
              Реєстрація
            </button>
          </div>

          {mode === 'register' ? (
            <form className="auth-form" onSubmit={handleRegister}>
              <label>
                Ім’я в CinemaCity
                <input value={username} onChange={(event) => setUsername(event.target.value)} />
              </label>
              <label>
                Електронна пошта
                <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" />
              </label>
              <label>
                Пароль
                <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" />
              </label>
              <label>
                Повторіть пароль
                <input
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  type="password"
                />
              </label>
              <button className="primary-action" disabled={isBusy} type="submit">
                Створити акаунт
              </button>
            </form>
          ) : (
            <form className="auth-form" onSubmit={handleLogin}>
              <label>
                Електронна пошта
                <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" />
              </label>
              <label>
                Пароль
                <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" />
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

          <output className="message" aria-live="polite">
            {message}
          </output>

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
        <Link to="/auth">Увійти</Link>
      </nav>
    </header>
  );
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
