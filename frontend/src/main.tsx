import React, { FormEvent, useEffect, useMemo, useState } from 'react';
import ReactDOM from 'react-dom/client';
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
    title: 'Премьеры недели',
    meta: 'Новинки, трейлеры, первые оценки',
    description: 'Собирайте фильмы, которые хочется посмотреть, и возвращайтесь к ним перед походом в кино.',
    imageUrl: 'https://image.tmdb.org/t/p/original/8rpDcsfLJypbO6vREc0547VKqEv.jpg'
  },
  {
    title: 'Фантастика на большом экране',
    meta: 'Космос, неон, громкие релизы',
    description: 'Лента CinemaCity помогает держать рядом яркие истории и не терять новые трейлеры.',
    imageUrl: 'https://image.tmdb.org/t/p/original/p1F51Lvj3sMopG948F5HsBbl43C.jpg'
  },
  {
    title: 'Вечерние сеансы',
    meta: 'Драмы, триллеры, авторское кино',
    description: 'Отмечайте ожидания, делитесь впечатлениями и находите настроение для следующего просмотра.',
    imageUrl: 'https://image.tmdb.org/t/p/original/628Dep6AxEtDxjZoGP78TsOxYbK.jpg'
  },
  {
    title: 'Большие истории',
    meta: 'Биографии, события, разговоры после титров',
    description: 'Сохраняйте громкие фильмы в профиль и возвращайтесь к ним, когда появится настроение.',
    imageUrl: 'https://image.tmdb.org/t/p/original/rLb2cwF3Pazuxaj0sRXQ037tGI1.jpg'
  }
];

function App() {
  const [mode, setMode] = useState<'register' | 'login'>('register');
  const [activeSlide, setActiveSlide] = useState(0);
  const [username, setUsername] = useState('cinefan');
  const [email, setEmail] = useState('cinefan@example.com');
  const [password, setPassword] = useState('Password123');
  const [confirmPassword, setConfirmPassword] = useState('Password123');
  const [confirmationToken, setConfirmationToken] = useState('');
  const [accessToken, setAccessToken] = useState('');
  const [user, setUser] = useState<AuthUser | null>(null);
  const [message, setMessage] = useState('Выберите фильм на вечер и войдите в свой зал.');
  const [isBusy, setIsBusy] = useState(false);

  const currentSlide = spotlightSlides[activeSlide];
  const googleLoginUrl = useMemo(() => {
    const returnUrl =
      typeof window === 'undefined' ? '/' : `${window.location.origin}/?google=success`;

    return `${apiUrl}/api/auth/google?returnUrl=${encodeURIComponent(returnUrl)}`;
  }, []);

  const status = useMemo(() => {
    if (user) {
      return `С возвращением, ${user.username}`;
    }

    if (confirmationToken) {
      return 'Осталось подтвердить почту';
    }

    return 'Гость CinemaCity';
  }, [confirmationToken, user]);

  useEffect(() => {
    const timer = window.setInterval(() => {
      setActiveSlide((slide) => (slide + 1) % spotlightSlides.length);
    }, 5200);

    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);

    if (params.get('google') !== 'success') {
      return;
    }

    window.history.replaceState({}, '', window.location.pathname);
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
          throw new Error(data?.message ?? data?.detail ?? 'Не удалось открыть ваш профиль.');
        }

        return data as AuthResponse;
      })
      .then((result) => {
        setAccessToken(result.accessToken);
        setUser(result.user);
        setMessage('Вход через Google завершен. Приятного просмотра.');
      })
      .catch(() => {
        setMessage('Google вернул вас в CinemaCity. Попробуйте войти еще раз.');
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
      setMessage('Аккаунт создан. Подтвердите почту кнопкой ниже и входите в свой зал.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Не получилось создать аккаунт.');
    } finally {
      setIsBusy(false);
    }
  }

  async function handleConfirmEmail() {
    if (!confirmationToken) {
      setMessage('Сначала создайте аккаунт.');
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
      setMessage('Почта подтверждена. Теперь можно войти.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Не получилось подтвердить почту.');
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
      setMessage('Вы вошли в CinemaCity. Самое время выбрать фильм.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Не получилось войти.');
    } finally {
      setIsBusy(false);
    }
  }

  async function handleRefresh() {
    setIsBusy(true);

    try {
      const result = await request<AuthResponse>('/api/auth/refresh', {
        method: 'POST',
        body: JSON.stringify({})
      });

      setAccessToken(result.accessToken);
      setUser(result.user);
      setMessage('Ваш сеанс продлен. Можно продолжать смотреть.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Не получилось продлить сеанс.');
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
      setMessage('Вы вышли из аккаунта. Возвращайтесь за новым фильмом.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Не получилось выйти.');
    } finally {
      setIsBusy(false);
    }
  }

  function goToSlide(slide: number) {
    setActiveSlide((slide + spotlightSlides.length) % spotlightSlides.length);
  }

  return (
    <main className="page-shell">
      <header className="site-header" aria-label="Главное меню">
        <a className="brand-row" href="/">
          <span className="brand-mark">CC</span>
          <span>CinemaCity</span>
        </a>
        <nav>
          <a href="#spotlight">Премьеры</a>
          <a href="#auth">Вход</a>
        </nav>
      </header>

      <section className="home-grid" id="spotlight" aria-label="Главная страница CinemaCity">
        <div className="spotlight" style={{ backgroundImage: `url(${currentSlide.imageUrl})` }}>
          <div className="spotlight-overlay">
            <p className="kicker">Кинолента CinemaCity</p>
            <h1>{currentSlide.title}</h1>
            <p className="spotlight-meta">{currentSlide.meta}</p>
            <p className="spotlight-text">{currentSlide.description}</p>

            <div className="spotlight-controls" aria-label="Управление баннером">
              <button type="button" aria-label="Предыдущий баннер" onClick={() => goToSlide(activeSlide - 1)}>
                ‹
              </button>
              <button type="button" aria-label="Следующий баннер" onClick={() => goToSlide(activeSlide + 1)}>
                ›
              </button>
            </div>
          </div>

          <div className="poster-rail" aria-label="Кадры фильмов">
            {spotlightSlides.map((slide, index) => (
              <button
                className={index === activeSlide ? 'active' : ''}
                key={slide.title}
                type="button"
                onClick={() => goToSlide(index)}
                aria-label={`Показать: ${slide.title}`}
              >
                <img src={slide.imageUrl} alt="" loading={index === 0 ? 'eager' : 'lazy'} />
              </button>
            ))}
          </div>
        </div>

        <section className="auth-surface" id="auth" aria-label="Вход в CinemaCity">
          <div>
            <p className="eyebrow">Ваш профиль</p>
            <h2>Войдите, чтобы сохранить фильмы</h2>
            <p className="auth-lead">{status}</p>
          </div>

          <div className="tabs" role="tablist" aria-label="Режим входа">
            <button
              className={mode === 'register' ? 'active' : ''}
              type="button"
              onClick={() => setMode('register')}
            >
              Регистрация
            </button>
            <button
              className={mode === 'login' ? 'active' : ''}
              type="button"
              onClick={() => setMode('login')}
            >
              Вход
            </button>
          </div>

          {mode === 'register' ? (
            <form className="auth-form" onSubmit={handleRegister}>
              <label>
                Имя в CinemaCity
                <input value={username} onChange={(event) => setUsername(event.target.value)} />
              </label>
              <label>
                Почта
                <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" />
              </label>
              <label>
                Пароль
                <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" />
              </label>
              <label>
                Повторите пароль
                <input
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  type="password"
                />
              </label>
              <button className="primary-action" disabled={isBusy} type="submit">
                Создать аккаунт
              </button>
            </form>
          ) : (
            <form className="auth-form" onSubmit={handleLogin}>
              <label>
                Почта
                <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" />
              </label>
              <label>
                Пароль
                <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" />
              </label>
              <button className="primary-action" disabled={isBusy} type="submit">
                Войти
              </button>
            </form>
          )}

          <div className="action-row">
            <button disabled={isBusy || !confirmationToken} type="button" onClick={handleConfirmEmail}>
              Подтвердить почту
            </button>
            <button disabled={isBusy || !user} type="button" onClick={handleRefresh}>
              Продлить сеанс
            </button>
            <button disabled={isBusy || !user} type="button" onClick={handleLogout}>
              Выйти
            </button>
          </div>

          <a className="google-link" href={googleLoginUrl}>
            Войти через Google
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
            </div>
          ) : null}
        </section>
      </section>
    </main>
  );
}

function toFriendlyMessage(message?: string) {
  switch (message) {
    case 'Username must be between 3 and 30 characters.':
      return 'Имя должно быть от 3 до 30 символов.';
    case 'Email format is invalid.':
      return 'Проверьте почту: адрес выглядит неверно.';
    case 'Password must contain at least 8 characters.':
      return 'Пароль должен быть не короче 8 символов.';
    case 'Password confirmation does not match.':
      return 'Пароли не совпадают.';
    case 'Username is already taken.':
      return 'Это имя уже занято.';
    case 'Email is already registered.':
      return 'Эта почта уже зарегистрирована.';
    case 'Email confirmation token is invalid or expired.':
    case 'Email confirmation token is invalid.':
      return 'Подтверждение почты устарело. Создайте аккаунт еще раз.';
    case 'User was not found.':
      return 'Аккаунт не найден.';
    case 'Account is temporarily locked.':
      return 'Аккаунт временно закрыт после нескольких попыток входа.';
    case 'Email must be confirmed before login.':
      return 'Сначала подтвердите почту.';
    default:
      return message || 'Что-то пошло не так. Попробуйте еще раз.';
  }
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
