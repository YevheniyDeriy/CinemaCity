> Архів матеріалів моноліту до виділення Auth/Catalog. Актуальний запуск, архітектура та перевірки: [кореневий README](../README.md) і [docs/lab-verification.md](../docs/lab-verification.md).

# CinemaCity — система оцінки фільмів та серіалів

> Клієнт-серверний застосунок на базі **ASP.NET Core 10** (бекенд) та **React + Vite** (фронтенд).  
> Аналог IMDb з оцінками, рецензіями, watchlist та адмін-панеллю.

---

## Зміст

1. [Стек технологій](#стек-технологій)
2. [Архітектура](#архітектура)
3. [Актори системи](#актори-системи)
4. [Use Cases (сценарії використання)](#use-cases)
   - [UC-01 — UC-03: Автентифікація](#uc-01--uc-03-автентифікація)
   - [UC-04 — UC-11: Каталог фільмів](#uc-04--uc-11-каталог-фільмів)
   - [UC-12 — UC-14: Оцінювання](#uc-12--uc-14-оцінювання)
   - [UC-15 — UC-19: Рецензії та коментарі](#uc-15--uc-19-рецензії-та-коментарі)
   - [UC-20 — UC-22: Профіль користувача](#uc-20--uc-22-профіль-користувача)
   - [UC-23 — UC-28: Адмін-панель](#uc-23--uc-28-адмін-панель)
   - [UC-29 — UC-30: Сповіщення та UX](#uc-29--uc-30-сповіщення-та-ux)
5. [Модулі та функції](#модулі-та-функції)
6. [Структура проєкту](#структура-проєкту)
7. [API ендпоінти (огляд)](#api-ендпоінти-огляд)
8. [Моделі даних](#моделі-даних)
9. [Запуск проєкту](#запуск-проєкту)

---

## Стек технологій

| Рівень | Технологія | Призначення |
|---|---|---|
| Фронтенд | React 18 + Vite | SPA, UI |
| Стан | Zustand | Глобальний стан (auth, movies) |
| HTTP-клієнт | Axios | Запити до API + interceptors |
| Маршрутизація | React Router v6 | Навігація між сторінками |
| Стилі | Tailwind CSS | Утилітарні класи, темна тема |
| Бекенд | ASP.NET Core 10 | REST API |
| ORM | Entity Framework Core 10 | Робота з БД |
| БД | PostgreSQL | Основне сховище |
| Кеш | Не використовується | Дані читаються напряму з PostgreSQL |
| Автентифікація | JWT (access + refresh) | Захищені ендпоінти |
| OAuth | Google OAuth 2.0 | Вхід через Google |
| Поштові листи | MailKit / SendGrid | Підтвердження, сповіщення |
| Контейнеризація | Docker + docker-compose | Локальне середовище |

---

## Архітектура

```
┌─────────────────────────────────────────────┐
│           Клієнт (React + Vite)             │
│  Pages │ Components │ Stores │ API Client   │
└──────────────────┬──────────────────────────┘
                   │ HTTP / REST JSON
┌──────────────────▼──────────────────────────┐
│         Сервер (ASP.NET Core 10)             │
│  Controllers → Services → Repositories      │
│  Middleware: JWT · CORS · Logging · Rate    │
│  Entity Framework Core (ORM)                │
└──────────────────┬──────────────────────────┘
                   │
            ┌──────▼──────┐
            │ PostgreSQL  │
            │ (основна БД)│
            └─────────────┘
```

### Шари бекенду

- **Controllers** — приймають HTTP-запити, повертають відповіді (не містять бізнес-логіки)
- **Services** — бізнес-логіка (розрахунок рейтингу, перевірки, надсилання листів)
- **Repositories** — абстракція над EF Core, CRUD операції
- **DTOs** — об'єкти для передачі даних між шарами та клієнтом
- **Middleware** — JWT-валідація, глобальна обробка помилок, CORS

---

## Актори системи

| Актор | Опис | Права |
|---|---|---|
| **Гість** | Не авторизований відвідувач | Перегляд каталогу, пошук, читання рецензій |
| **Користувач** | Зареєстрований   та авторизований | Усі права гостя + оцінки, рецензії, watchlist |
| **Адміністратор** | Персонал платформи | Усі права + управління контентом і модерація |

---

## Use Cases

---

### UC-01 — UC-03: Автентифікація

---

#### UC-01: Реєстрація через email

**Актор:** Гість  
**Передумова:** Користувач не має акаунту  
**Результат:** Створено акаунт, надіслано підтверджувальний лист

**Основний сценарій:**
1. Гість відкриває форму реєстрації (`/register`)
2. Вводить `username`, `email`, `password`, `confirmPassword`
3. Клієнт валідує поля (довжина, формат email, збіг паролів)
4. POST `/api/auth/register` → сервер перевіряє унікальність email
5. Сервер хешує пароль (BCrypt) і зберігає нового `User` зі статусом `Unverified`
6. Сервер надсилає лист із посиланням для підтвердження (JWT-токен у query string)
7. Користувач переходить за посиланням → GET `/api/auth/confirm-email?token=...`
8. Статус змінюється на `Active`, користувач перенаправляється на `/login`

**Альтернативний сценарій (email зайнятий):**
- Крок 4: сервер повертає `400 Bad Request` з повідомленням "Email вже використовується"
- Клієнт показує toast-помилку під полем email

**Валідація:**
- `username`: 3–30 символів, латиниця/цифри/підкреслення
- `email`: коректний формат
- `password`: мінімум 8 символів, хоча б одна цифра

---

#### UC-02: Вхід через email / пароль

**Актор:** Зареєстрований користувач (статус `Active`)  
**Передумова:** Акаунт підтверджений  
**Результат:** Видано access + refresh токени; користувач авторизований

**Основний сценарій:**
1. Користувач відкриває форму входу (`/login`)
2. Вводить `email` та `password`
3. POST `/api/auth/login`
4. Сервер шукає `User` за email, перевіряє хеш паролю (BCrypt.Verify)
5. Генерує **access token** (JWT, TTL 15 хв) та **refresh token** (GUID, TTL 7 днів)
6. Refresh token зберігається в БД (`RefreshTokens`), access token — у пам'яті клієнта (Zustand)
7. Refresh token передається у httpOnly cookie
8. Клієнт зберігає access token в пам'яті (не в localStorage), оновлює стан авторизації
9. Перенаправлення на головну сторінку або `returnUrl`

**Альтернативний сценарій (невірний пароль):**
- Крок 4: сервер повертає `401 Unauthorized`
- Лічильник невдалих спроб +1; після 5 спроб — блокування на 15 хв

**Вихід (logout):**
1. POST `/api/auth/logout`
2. Сервер інвалідує refresh token у БД
3. Клієнт очищає стан Zustand, перенаправляє на `/`

---

#### UC-03: Вхід через Google OAuth 2.0

**Актор:** Гість або існуючий користувач  
**Передумова:** Google-акаунт користувача  
**Результат:** Користувач авторизований; при першому вході — автоматично зареєстрований

**Основний сценарій:**
1. Користувач натискає «Увійти через Google»
2. GET `/api/auth/google` → сервер формує URL авторизації Google з `state` (CSRF-захист) та `redirect_uri`
3. Браузер перенаправляється на сторінку вибору Google-акаунту
4. Google повертає `code` на GET `/api/auth/google/callback?code=...&state=...`
5. Сервер обмінює `code` на `access_token` Google (POST на `https://oauth2.googleapis.com/token`)
6. Сервер запитує профіль користувача: email, ім'я, аватар (`https://www.googleapis.com/userinfo/v2/me`)
7. Якщо `User` з таким `GoogleId` або `email` існує → вхід; якщо ні → реєстрація (без пароля)
8. Видаються власні JWT-токени системи (такі самі, як у UC-02)

**Примітки щодо реалізації (C#):**
```csharp
// Program.cs
builder.Services.AddAuthentication()
    .AddGoogle(options => {
        options.ClientId     = builder.Configuration["Google:ClientId"];
        options.ClientSecret = builder.Configuration["Google:ClientSecret"];
        options.CallbackPath = "/api/auth/google/callback";
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });
```

**Примітки щодо реалізації (React):**
```tsx
// Просте перенаправлення — бекенд бере на себе весь OAuth флоу
const handleGoogleLogin = () => {
  window.location.href = `${import.meta.env.VITE_API_URL}/api/auth/google`;
};
```

---

### UC-04 — UC-11: Каталог фільмів

---

#### UC-04: Перегляд списку фільмів

**Актор:** Гість / Користувач  
**Передумова:** —  
**Результат:** Відображено список фільмів з пагінацією

**Основний сценарій:**
1. GET `/api/movies?page=1&pageSize=20&sort=rating`
2. Сервер повертає `PagedResult<MovieCardDto>`:
   - `id`, `title`, `year`, `posterUrl`, `averageRating`, `ratingsCount`, `genres[]`
3. Клієнт відображає картки фільмів у сітці
4. Пагінація: кнопки «попередня / наступна сторінка» або infinite scroll

**Параметри сортування (`sort`):** `rating` | `year` | `title` | `newest`

---

#### UC-05: Сторінка фільму / серіалу

**Актор:** Гість / Користувач  
**Передумова:** Фільм існує в БД  
**Результат:** Відображено повну інформацію про фільм

**Основний сценарій:**
1. GET `/api/movies/{id}`
2. Відповідь містить:
   - Заголовок, оригінальна назва, рік, тривалість, опис, трейлер (YouTube ID)
   - `posterUrl`, `backdropUrl`
   - `averageRating`, `ratingsCount`, `ratingsDistribution` (масив 1–10)
   - `genres[]`, `countries[]`, `languages[]`
   - `cast[]` (перші 10): `personId`, `name`, `photoUrl`, `roleName`, `creditType`
   - `contentType`: `Movie` або `Series`
3. Якщо `contentType == Series` → додатково GET `/api/movies/{id}/seasons`
   - Відповідь: `seasons[]` → `episodes[]` з `episodeNumber`, `title`, `airDate`, `averageRating`
4. Якщо користувач авторизований → показати його поточну оцінку (якщо є) та кнопку watchlist

---

#### UC-06: Пошук фільмів

**Актор:** Гість / Користувач  
**Передумова:** —  
**Результат:** Список фільмів, що відповідають запиту

**Основний сценарій:**
1. Користувач вводить текст у поле пошуку (debounce 300 мс)
2. GET `/api/movies/search?q=inception&page=1`
3. Сервер шукає по полях `Title`, `OriginalTitle` (ILIKE `%q%`)
4. Відображаються результати з підсвічуванням збігів

**Примітка:** Мінімум 2 символи для запиту. При порожньому рядку — повертається до каталогу.

---

#### UC-07: Фільтрація каталогу

**Актор:** Гість / Користувач  
**Передумова:** —  
**Результат:** Відфільтрований та відсортований список

**Параметри фільтрації:**

| Параметр | Тип | Приклад |
|---|---|---|
| `genre` | string (slug) | `action`, `drama` |
| `yearFrom` | int | `2000` |
| `yearTo` | int | `2024` |
| `ratingMin` | decimal | `7.0` |
| `contentType` | enum | `Movie` / `Series` |
| `sort` | enum | `rating` / `year` / `title` |

**GET** `/api/movies?genre=drama&yearFrom=2010&ratingMin=7.5&sort=rating`

Клієнт зберігає фільтри в URL query string (`useSearchParams`) — посилання можна копіювати.

---

#### UC-08: Серіали — перегляд сезонів та серій

**Актор:** Гість / Користувач  
**Передумова:** Контент має `contentType == Series`  
**Результат:** Ієрархічний перегляд сезон → серія

**Основний сценарій:**
1. На сторінці серіалу відображається акордеон сезонів
2. GET `/api/movies/{seriesId}/seasons/{seasonNumber}/episodes`
3. Кожна серія показує: номер, назву, дату виходу, тривалість, `averageRating`
4. Авторизований користувач може виставити оцінку конкретній серії
5. Рейтинг серіалу = середнє по всіх оцінених серіях (або по сезонах — налаштовується)

---

#### UC-09: Рекомендації (схожі фільми)

**Актор:** Гість / Користувач  
**Передумова:** Відкрита сторінка фільму  
**Результат:** Блок «Схожі фільми» (до 8 позицій)

**Алгоритм (простий, достатній для курсової):**
1. GET `/api/movies/{id}/similar`
2. Сервер знаходить фільми з ≥2 спільних жанрів, сортує за `averageRating` DESC
3. Виключає сам фільм із результатів
4. Повертає до 8 `MovieCardDto`

---

#### UC-10: Сторінка особи (актор / режисер)

**Актор:** Гість / Користувач  
**Передумова:** Особа існує в БД  
**Результат:** Відображено профіль особи та фільмографія

**Основний сценарій:**
1. GET `/api/persons/{id}`
2. Відповідь: `fullName`, `birthDate`, `birthPlace`, `bio`, `photoUrl`
3. GET `/api/persons/{id}/filmography` → список фільмів з роллю (`Actor` / `Director` / `Writer`)
4. Фільмографія сортується за роком (нові → старі)
5. Картки фільмів ідентичні каталогу

---

#### UC-11: Топ-рейтинг

**Актор:** Гість / Користувач  
**Передумова:** —  
**Результат:** Список 250 найвище оцінених фільмів

**GET** `/api/movies/top?limit=250`  
Додаткова умова: мінімум 50 оцінок (щоб уникнути флуку з 1–2 оцінками на рівні 10/10).

---

### UC-12 — UC-14: Оцінювання

---

#### UC-12: Виставлення оцінки фільму

**Актор:** Авторизований користувач  
**Передумова:** Користувач не виставляв оцінку цьому фільму  
**Результат:** Оцінка збережена; середній рейтинг фільму оновлено

**Основний сценарій:**
1. На сторінці фільму користувач натискає на зірочку (1–10)
2. POST `/api/ratings` з тілом `{ movieId, score }`
3. Сервер перевіряє: чи немає вже оцінки від цього `userId` для цього `movieId`
4. Зберігає `Rating` у БД
5. Тригер або service-метод перераховує `Movie.AverageRating` та `Movie.RatingsCount`
6. Клієнт отримує оновлений `{ averageRating, ratingsCount }` і оновлює UI без перезавантаження

**Розрахунок середнього:**
```
AverageRating = SUM(score) / COUNT(ratings) WHERE movieId = X
```

---

#### UC-13: Зміна або видалення оцінки

**Актор:** Авторизований користувач  
**Передумова:** Оцінка від цього користувача вже існує  
**Результат:** Оцінка оновлена / видалена; середній рейтинг перераховано

**Зміна:** PUT `/api/ratings/{id}` з новим `score`  
**Видалення:** DELETE `/api/ratings/{id}`

Після обох операцій сервер перераховує `AverageRating`.

---

#### UC-14: Розподіл оцінок

**Актор:** Гість / Користувач  
**Передумова:** Фільм має хоча б одну оцінку  
**Результат:** Гістограма розподілу оцінок (1–10)

**GET** `/api/movies/{id}/ratings/distribution`  
Відповідь: `[{ score: 1, count: 5 }, { score: 2, count: 12 }, ..., { score: 10, count: 87 }]`  
Відображається горизонтальна гістограма на сторінці фільму.

---

### UC-15 — UC-19: Рецензії та коментарі

---

#### UC-15: Написання рецензії

**Актор:** Авторизований користувач  
**Передумова:** Користувач не писав рецензію на цей фільм (одна рецензія на фільм)  
**Результат:** Рецензія збережена зі статусом `Pending`; очікує модерації

**Основний сценарій:**
1. POST `/api/reviews` з тілом `{ movieId, content, isSpoiler }`
2. `content`: мінімум 50, максимум 5000 символів
3. `isSpoiler`: bool — якщо `true`, рецензія приховується за попередженням
4. Статус: `Pending` → видима лише автору до схвалення адміністратором
5. Після схвалення — `Approved`; лист на email автора

---

#### UC-16: Редагування та видалення рецензії

**Актор:** Автор рецензії  
**Передумова:** Рецензія існує, автор — поточний користувач  
**Результат:** Рецензія оновлена / видалена

**Редагування:** PUT `/api/reviews/{id}`  
- Після редагування статус скидається на `Pending` (потребує повторної модерації)

**Видалення:** DELETE `/api/reviews/{id}`  
- М'яке видалення: поле `DeletedAt` заповнюється, `IsDeleted = true`
- Коментарі до видаленої рецензії також приховуються

---

#### UC-17: Коментарі до рецензій

**Актор:** Авторизований користувач  
**Передумова:** Рецензія має статус `Approved`  
**Результат:** Коментар доданий під рецензією

**Основний сценарій:**
1. POST `/api/reviews/{reviewId}/comments` з тілом `{ content }`
2. `content`: 1–1000 символів
3. Коментарі відображаються під рецензією, відсортовані за датою (старіші → новіші)
4. GET `/api/reviews/{reviewId}/comments?page=1&pageSize=20`
5. Автор коментаря може видалити свій коментар: DELETE `/api/comments/{id}`

---

#### UC-18: Модерація рецензій (адмін)

**Актор:** Адміністратор  
**Передумова:** Є рецензії зі статусом `Pending`  
**Результат:** Рецензія схвалена або відхилена

**Основний сценарій:**
1. GET `/api/admin/reviews?status=Pending&page=1` → черга на модерацію
2. Адмін переглядає текст рецензії та профіль автора
3. **Схвалення:** PUT `/api/admin/reviews/{id}/approve`
   - Статус → `Approved`
   - Email автору: «Ваша рецензія схвалена»
4. **Відхилення:** PUT `/api/admin/reviews/{id}/reject` з тілом `{ reason }`
   - Статус → `Rejected`
   - Email автору з причиною відхилення

---

#### UC-19: Лайки рецензій

**Актор:** Авторизований користувач  
**Передумова:** Рецензія `Approved`; користувач не є автором  
**Результат:** Лічильник лайків оновлено

POST `/api/reviews/{id}/like` — toggle: перший запит ставить лайк, повторний — знімає.  
Відповідь: `{ likesCount: 42, isLiked: true }`

---

### UC-20 — UC-22: Профіль користувача

---

#### UC-20: Watchlist

**Актор:** Авторизований користувач  
**Передумова:** —  
**Результат:** Фільм доданий до / видалений зі списку «хочу подивитись»

**Додати:** POST `/api/watchlist` з `{ movieId }`  
**Видалити:** DELETE `/api/watchlist/{movieId}`  
**Переглянути:** GET `/api/users/{id}/watchlist?page=1&pageSize=20`

На сторінці watchlist доступне сортування (за датою додавання, за рейтингом, за назвою) та фільтр за жанром.

---

#### UC-21: Список «переглянутих»

**Актор:** Авторизований користувач  
**Передумова:** —  
**Результат:** Фільм позначено як переглянутий

**Логіка:** Фільм автоматично переміщується зі статусу `Watchlist` → `Watched` після виставлення оцінки. Але користувач може також вручну позначити фільм як переглянутий без оцінки.

**Позначити вручну:** PUT `/api/watchlist/{movieId}/status` з `{ status: "Watched" }`  
**Переглянути:** GET `/api/users/{id}/watched?page=1`

---

#### UC-22: Статистика профілю

**Актор:** Авторизований користувач  
**Передумова:** Є хоча б одна активність  
**Результат:** Сторінка зі статистикою

**GET** `/api/users/{id}/stats`

Відповідь містить:
- `totalRatings` — скільки фільмів оцінено
- `totalReviews` — кількість рецензій
- `averageGivenRating` — середня оцінка, яку ставить користувач
- `ratingDistribution` — масив `[{score, count}]` власних оцінок
- `favoriteGenres` — топ-3 жанри за кількістю переглянутих
- `watchlistCount` — у watchlist зараз
- `watchedCount` — переглянуто всього

---

### UC-23 — UC-28: Адмін-панель

---

#### UC-23: Додавання фільму / серіалу

**Актор:** Адміністратор  
**Передумова:** Авторизований з роллю `Admin`  
**Результат:** Новий запис `Movie` / `Series` у БД

**POST** `/api/admin/movies`

Тіло запиту:
```json
{
  "title": "Inception",
  "originalTitle": "Inception",
  "contentType": "Movie",
  "releaseYear": 2010,
  "duration": 148,
  "description": "...",
  "trailerYoutubeId": "YoHD9XEInc0",
  "genreIds": [1, 3],
  "countryIds": [1],
  "castEntries": [
    { "personId": "guid", "creditType": "Director" },
    { "personId": "guid", "creditType": "Actor", "roleName": "Dom Cobb" }
  ]
}
```

Постер завантажується окремим запитом: POST `/api/admin/movies/{id}/poster` (multipart/form-data).

---

#### UC-24: Редагування фільму

**Актор:** Адміністратор  
**Передумова:** Фільм існує  
**Результат:** Метадані фільму оновлені

**PUT** `/api/admin/movies/{id}` — повне оновлення (або PATCH для часткового).  
Зміна жанрів: PUT `/api/admin/movies/{id}/genres` з масивом `genreIds`.  
Зміна касту: PUT `/api/admin/movies/{id}/cast` з масивом `castEntries`.

---

#### UC-25: Видалення фільму

**Актор:** Адміністратор  
**Передумова:** Фільм існує  
**Результат:** Фільм прихований з каталогу (м'яке видалення)

**DELETE** `/api/admin/movies/{id}`  
- Поле `IsDeleted = true`, `DeletedAt = DateTime.UtcNow`
- Оцінки та рецензії зберігаються в БД (для можливого відновлення)
- Відновлення: PUT `/api/admin/movies/{id}/restore`

---

#### UC-26: Управління жанрами

**Актор:** Адміністратор  
**Передумова:** —  
**Результат:** Жанр створений / оновлений / видалений

| Дія | Метод | Ендпоінт |
|---|---|---|
| Список | GET | `/api/genres` |
| Створити | POST | `/api/admin/genres` |
| Оновити | PUT | `/api/admin/genres/{id}` |
| Видалити | DELETE | `/api/admin/genres/{id}` |

Тіло: `{ "name": "Thriller", "slug": "thriller" }`

---

#### UC-27: Управління особами (актори / режисери)

**Актор:** Адміністратор  
**Передумова:** —  
**Результат:** Особа створена / оновлена / видалена

| Дія | Метод | Ендпоінт |
|---|---|---|
| Список | GET | `/api/persons?q=nolan` |
| Створити | POST | `/api/admin/persons` |
| Оновити | PUT | `/api/admin/persons/{id}` |
| Видалити | DELETE | `/api/admin/persons/{id}` |
| Фото | POST | `/api/admin/persons/{id}/photo` |

Тіло: `{ "fullName": "Christopher Nolan", "birthDate": "1970-07-30", "birthPlace": "London", "bio": "..." }`

---

#### UC-28: Модерація рецензій в адмін-панелі

Детально описано в **UC-18**.  
Додатково в адмін-панелі: фільтр за статусом (`Pending` / `Approved` / `Rejected`), пошук за автором, сортування за датою.

---

### UC-29 — UC-30: Сповіщення та UX

---

#### UC-29: Toast-повідомлення

**Актор:** Будь-який  
**Передумова:** Виконана будь-яка дія користувача  
**Результат:** Короткочасне сповіщення у куті екрану

**Типи:**
- `success` — зелений: «Оцінку збережено», «Рецензію надіслано»
- `error` — червоний: «Помилка сервера», «Email вже зайнятий»
- `info` — синій: «Рецензія очікує модерації»
- `warning` — жовтий: «Введіть мінімум 50 символів»

**Реалізація (React):** Бібліотека `react-hot-toast` або власний `ToastContext`.  
Тривалість відображення: 3 секунди (помилки — 5 секунд). Позиція: нижній правий кут.

**Axios interceptor** перехоплює всі відповіді з `status >= 400` і автоматично показує toast з текстом з поля `error.response.data.message`.

---

#### UC-30: Темна / світла тема

**Актор:** Будь-який  
**Передумова:** —  
**Результат:** Тема змінена та збережена між сесіями

**Реалізація:**
1. Tailwind CSS: режим `darkMode: 'class'` у `tailwind.config.js`
2. Кнопка перемикача у navbar
3. Стан теми зберігається в `localStorage` під ключем `theme`
4. При першому завантаженні — зчитується `localStorage`; якщо відсутнє — `prefers-color-scheme` системи
5. Клас `dark` додається/знімається з `<html>` елемента

```tsx
// useTheme.ts
const toggleTheme = () => {
  const next = theme === 'light' ? 'dark' : 'light';
  setTheme(next);
  localStorage.setItem('theme', next);
  document.documentElement.classList.toggle('dark', next === 'dark');
};
```

---

## Модулі та функції

### Модуль 1: Автентифікація
| # | Функція | Пріоритет |
|---|---|---|
| 1 | Реєстрація через email | must |
| 2 | Вхід / вихід (JWT) | must |
| 3 | OAuth через Google | should |

### Модуль 2: Каталог фільмів
| # | Функція | Пріоритет |
|---|---|---|
| 4 | Список фільмів (пагінація, сортування) | must |
| 5 | Сторінка фільму / серіалу | must |
| 6 | Пошук (full-text) | must |
| 7 | Фільтри (жанр, рік, рейтинг, тип) | must |
| 8 | Серіали: сезони та серії | should |
| 9 | Рекомендації (схожі фільми) | should |
| 10 | Сторінка особи + фільмографія | should |
| 11 | Топ-250 | should |

### Модуль 3: Оцінювання
| # | Функція | Пріоритет |
|---|---|---|
| 12 | Виставити оцінку (1–10) | must |
| 13 | Змінити / видалити оцінку | must |
| 14 | Розподіл оцінок (гістограма) | should |

### Модуль 4: Рецензії та коментарі
| # | Функція | Пріоритет |
|---|---|---|
| 15 | Написати рецензію (з позначкою спойлера) | must |
| 16 | Редагувати / видалити рецензію | must |
| 17 | Коментарі до рецензій | could |
| 18 | Модерація рецензій | must |
| 19 | Лайки рецензій | should |

### Модуль 5: Профіль користувача
| # | Функція | Пріоритет |
|---|---|---|
| 20 | Watchlist | must |
| 21 | Переглянуті (при наявності оцінки) | should |
| 22 | Статистика профілю | should |

### Модуль 6: Адмін-панель
| # | Функція | Пріоритет |
|---|---|---|
| 23 | Додати фільм / серіал | must |
| 24 | Редагувати фільм | must |
| 25 | Видалити фільм (soft delete) | must |
| 26 | Управління жанрами | must |
| 27 | Управління особами | must |
| 28 | Модерація рецензій | must |

### Модуль 7: Сповіщення та UX
| # | Функція | Пріоритет |
|---|---|---|
| 29 | Toast-повідомлення | must |
| 30 | Темна / світла тема | could |

---

## Структура проєкту

```
cinerate/
├── backend/                          # ASP.NET Core 10
│   ├── CineRate.API/
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   ├── MoviesController.cs
│   │   │   ├── RatingsController.cs
│   │   │   ├── ReviewsController.cs
│   │   │   ├── PersonsController.cs
│   │   │   ├── GenresController.cs
│   │   │   ├── UsersController.cs
│   │   │   └── Admin/
│   │   │       └── AdminMoviesController.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   └── RateLimitingMiddleware.cs
│   │   └── Program.cs
│   ├── CineRate.Application/
│   │   ├── Services/
│   │   │   ├── AuthService.cs
│   │   │   ├── MovieService.cs
│   │   │   ├── RatingService.cs
│   │   │   ├── ReviewService.cs
│   │   │   └── EmailService.cs
│   │   └── DTOs/
│   │       ├── MovieCardDto.cs
│   │       ├── MovieDetailsDto.cs
│   │       └── ...
│   ├── CineRate.Domain/
│   │   ├── Entities/
│   │   │   ├── User.cs
│   │   │   ├── Movie.cs
│   │   │   ├── Rating.cs
│   │   │   ├── Review.cs
│   │   │   ├── Comment.cs
│   │   │   ├── Genre.cs
│   │   │   ├── Person.cs
│   │   │   ├── MovieCast.cs
│   │   │   └── WatchlistItem.cs
│   │   └── Enums/
│   │       ├── UserRole.cs
│   │       ├── ReviewStatus.cs
│   │       ├── ContentType.cs
│   │       └── CreditType.cs
│   └── CineRate.Infrastructure/
│       ├── Data/
│       │   ├── AppDbContext.cs
│       │   └── Migrations/
│       └── Repositories/
│           ├── MovieRepository.cs
│           └── ...
│
├── frontend/                         # React + Vite
│   ├── src/
│   │   ├── pages/
│   │   │   ├── HomePage.tsx
│   │   │   ├── MoviePage.tsx
│   │   │   ├── PersonPage.tsx
│   │   │   ├── ProfilePage.tsx
│   │   │   ├── WatchlistPage.tsx
│   │   │   ├── LoginPage.tsx
│   │   │   ├── RegisterPage.tsx
│   │   │   └── admin/
│   │   │       └── AdminMoviesPage.tsx
│   │   ├── components/
│   │   │   ├── MovieCard.tsx
│   │   │   ├── RatingStars.tsx
│   │   │   ├── ReviewCard.tsx
│   │   │   ├── SearchBar.tsx
│   │   │   └── Toast.tsx
│   │   ├── stores/
│   │   │   ├── authStore.ts
│   │   │   └── themeStore.ts
│   │   ├── api/
│   │   │   ├── axios.ts
│   │   │   ├── movies.ts
│   │   │   └── auth.ts
│   │   └── hooks/
│   │       ├── useMovies.ts
│   │       └── useTheme.ts
│   ├── index.html
│   ├── vite.config.ts
│   └── tailwind.config.js
│
├── docker-compose.yml
└── README.md
```

---

## API ендпоінти (огляд)

### Автентифікація
| Метод | Шлях | Опис |
|---|---|---|
| POST | `/api/auth/register` | Реєстрація |
| POST | `/api/auth/login` | Вхід |
| POST | `/api/auth/logout` | Вихід |
| POST | `/api/auth/refresh` | Оновлення access token |
| GET | `/api/auth/google` | OAuth редирект |
| GET | `/api/auth/google/callback` | OAuth callback |
| GET | `/api/auth/confirm-email` | Підтвердження email |

### Фільми
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/api/movies` | Список з фільтрами |
| GET | `/api/movies/{id}` | Деталі фільму |
| GET | `/api/movies/search` | Пошук |
| GET | `/api/movies/top` | Топ-250 |
| GET | `/api/movies/{id}/similar` | Рекомендації |
| GET | `/api/movies/{id}/seasons` | Сезони серіалу |
| GET | `/api/movies/{id}/ratings/distribution` | Розподіл оцінок |

### Оцінки
| Метод | Шлях | Опис |
|---|---|---|
| POST | `/api/ratings` | Виставити оцінку |
| PUT | `/api/ratings/{id}` | Змінити оцінку |
| DELETE | `/api/ratings/{id}` | Видалити оцінку |

### Рецензії
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/api/movies/{id}/reviews` | Рецензії фільму |
| POST | `/api/reviews` | Написати рецензію |
| PUT | `/api/reviews/{id}` | Редагувати |
| DELETE | `/api/reviews/{id}` | Видалити |
| POST | `/api/reviews/{id}/like` | Лайк/дизлайк |
| GET | `/api/reviews/{id}/comments` | Коментарі |
| POST | `/api/reviews/{id}/comments` | Додати коментар |
| DELETE | `/api/comments/{id}` | Видалити коментар |

### Профіль
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/api/users/{id}/watchlist` | Watchlist |
| POST | `/api/watchlist` | Додати до watchlist |
| DELETE | `/api/watchlist/{movieId}` | Видалити |
| PUT | `/api/watchlist/{movieId}/status` | Змінити статус |
| GET | `/api/users/{id}/watched` | Переглянуті |
| GET | `/api/users/{id}/stats` | Статистика |

### Адмін
| Метод | Шлях | Опис |
|---|---|---|
| POST | `/api/admin/movies` | Додати фільм |
| PUT | `/api/admin/movies/{id}` | Редагувати |
| DELETE | `/api/admin/movies/{id}` | Видалити |
| POST | `/api/admin/movies/{id}/poster` | Завантажити постер |
| GET | `/api/admin/reviews` | Черга модерації |
| PUT | `/api/admin/reviews/{id}/approve` | Схвалити |
| PUT | `/api/admin/reviews/{id}/reject` | Відхилити |
| GET/POST/PUT/DELETE | `/api/admin/genres` | CRUD жанрів |
| GET/POST/PUT/DELETE | `/api/admin/persons` | CRUD осіб |

---

## Моделі даних

### User
```csharp
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string? PasswordHash { get; set; }       // null якщо OAuth
    public string? GoogleId { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsEmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Rating> Ratings { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public ICollection<WatchlistItem> WatchlistItems { get; set; }
}
```

### Movie
```csharp
public class Movie
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string OriginalTitle { get; set; }
    public ContentType ContentType { get; set; }    // Movie | Series
    public int ReleaseYear { get; set; }
    public int? Duration { get; set; }              // хвилини
    public string? Description { get; set; }
    public string? PosterUrl { get; set; }
    public string? TrailerYoutubeId { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingsCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Genre> Genres { get; set; }
    public ICollection<MovieCast> Cast { get; set; }
    public ICollection<Rating> Ratings { get; set; }
    public ICollection<Review> Reviews { get; set; }
    public ICollection<Season>? Seasons { get; set; }
}
```

### Rating
```csharp
public class Rating
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MovieId { get; set; }
    public decimal Score { get; set; }             // 1.0 – 10.0
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

### Review
```csharp
public class Review
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MovieId { get; set; }
    public string Content { get; set; }
    public bool IsSpoiler { get; set; }
    public int LikesCount { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public string? RejectionReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Comment> Comments { get; set; }
}
```

---

## Запуск проєкту

### Вимоги
- .NET 10 SDK
- Node.js 20+
- Docker & docker-compose

### Локальний запуск

```bash
# 1. Клонувати репозиторій
git clone https://github.com/your-username/cinerate.git
cd cinerate

# 2. Запустити PostgreSQL через Docker
docker-compose up -d postgres

# 3. Бекенд
cd backend/CineRate.API
cp appsettings.Development.example.json appsettings.Development.json
# заповнити Google:ClientId, Google:ClientSecret, ConnectionStrings:Default
dotnet ef database update
dotnet run

# 4. Фронтенд
cd ../../frontend
cp .env.example .env
# VITE_API_URL=http://localhost:5000
npm install
npm run dev
```

### Змінні середовища (бекенд)

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=cinerate;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars",
    "AccessTokenTTLMinutes": 15,
    "RefreshTokenTTLDays": 7
  },
  "Google": {
    "ClientId": "...",
    "ClientSecret": "..."
  },
  "Email": {
    "From": "noreply@cinerate.com",
    "SmtpHost": "smtp.sendgrid.net",
    "SmtpPort": 587,
    "ApiKey": "..."
  }
}
```

---

*Версія документу: 1.0 | Стек: ASP.NET Core 10 + React 18 + Vite + PostgreSQL*
