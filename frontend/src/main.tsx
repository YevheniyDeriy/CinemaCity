import React, { FormEvent, useMemo, useState } from 'react';
import ReactDOM from 'react-dom/client';
import './styles.css';

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5029';

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

function App() {
  const [mode, setMode] = useState<'register' | 'login'>('register');
  const [username, setUsername] = useState('cinefan');
  const [email, setEmail] = useState('cinefan@example.com');
  const [password, setPassword] = useState('Password123');
  const [confirmPassword, setConfirmPassword] = useState('Password123');
  const [confirmationToken, setConfirmationToken] = useState('');
  const [accessToken, setAccessToken] = useState('');
  const [user, setUser] = useState<AuthUser | null>(null);
  const [message, setMessage] = useState('Готово к подключению аккаунта');
  const [isBusy, setIsBusy] = useState(false);

  const status = useMemo(() => {
    if (user) {
      return `Вы вошли как ${user.username}`;
    }

    if (confirmationToken) {
      return 'Email ожидает подтверждения';
    }

    return 'Гость';
  }, [confirmationToken, user]);

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
      throw new Error(data?.message ?? data?.detail ?? 'Запрос не выполнен');
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
      setMessage('Аккаунт создан. Подтвердите email тестовой кнопкой ниже.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Ошибка регистрации');
    } finally {
      setIsBusy(false);
    }
  }

  async function handleConfirmEmail() {
    if (!confirmationToken) {
      setMessage('Сначала зарегистрируйте пользователя');
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
      setMessage('Email подтвержден. Теперь можно войти.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Ошибка подтверждения');
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
      setMessage('Авторизация прошла успешно');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Ошибка входа');
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
      setMessage('Access token обновлен');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Ошибка refresh token');
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
      setMessage('Вы вышли из аккаунта');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Ошибка выхода');
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <main className="shell">
      <section className="hero-panel" aria-label="CinemaCity auth">
        <div className="hero-copy">
          <div className="brand-row">
            <span className="brand-mark">CC</span>
            <span>CinemaCity</span>
          </div>

          <h1>Темный кинозал для оценок, рецензий и watchlist</h1>
          <p>
            Главная страница уже подключена к backend auth API: регистрация,
            подтверждение email, вход, refresh token и выход.
          </p>

          <div className="status-grid" aria-label="Статусы">
            <div>
              <span>API</span>
              <strong>{apiUrl}</strong>
            </div>
            <div>
              <span>Статус</span>
              <strong>{status}</strong>
            </div>
          </div>
        </div>

        <section className="auth-surface" aria-label="Авторизация">
          <div className="tabs" role="tablist" aria-label="Auth mode">
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
                Username
                <input value={username} onChange={(event) => setUsername(event.target.value)} />
              </label>
              <label>
                Email
                <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" />
              </label>
              <label>
                Password
                <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" />
              </label>
              <label>
                Confirm password
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
                Email
                <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" />
              </label>
              <label>
                Password
                <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" />
              </label>
              <button className="primary-action" disabled={isBusy} type="submit">
                Войти
              </button>
            </form>
          )}

          <div className="action-row">
            <button disabled={isBusy || !confirmationToken} type="button" onClick={handleConfirmEmail}>
              Подтвердить email
            </button>
            <button disabled={isBusy || !user} type="button" onClick={handleRefresh}>
              Refresh
            </button>
            <button disabled={isBusy || !user} type="button" onClick={handleLogout}>
              Logout
            </button>
          </div>

          <a className="google-link" href={`${apiUrl}/api/auth/google`}>
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

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
