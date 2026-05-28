import React from 'react';
import ReactDOM from 'react-dom/client';
import './styles.css';

const root = ReactDOM.createRoot(document.getElementById('root')!);

root.render(
  <React.StrictMode>
    <main className="app-shell">
      <section className="hero">
        <p className="eyebrow">CinemaCity</p>
        <h1>Каталог фильмов и сериалов</h1>
        <p>
          Базовая frontend-структура готова для подключения страниц каталога,
          профиля, авторизации и админ-панели.
        </p>
      </section>
    </main>
  </React.StrictMode>
);
