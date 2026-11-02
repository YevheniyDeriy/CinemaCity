# CinemaCity: виділення сервісів із власного моноліту
**ЛР4 (2026-11-02):** [клієнт і запуск](docs/lab4-client.md), [аудит ЛР0–ЛР4](docs/labs-0-4-audit-2026-11-02.md), [презентація на 7 слайдів](output/presentation/CinemaCity-Lab4.pptx). Нові екрани: `/catalog`, `/movies/:id`, `/movies/:id/rate`, `/my-ratings`, `/auth`.


**ЛР3:** [реалізація, запуск, перевірка та контрольні питання](docs/lab3-gateway.md). Новий екран: `GET /api/v1/screens/movies/{id}`; Swagger gateway: `http://localhost:5000/swagger`. JWT, кеш публічного каталогу, інвалідизація та ліміти працюють на шлюзі. У Docker серед backend-сервісів опублікований лише порт gateway; порти Auth/Catalog/Community закриті.

Оновлення ЛР2: Community тепер окремий процес на **5003** з БД `cinemacity_community`. Моноліт і його бібліотеки залишені без змін цієї сесії. Актуальні деталі: [стан](docs/current-status.md), [ADR-0003](docs/adr/0003-community-service.md), [порівняння контрактів](docs/baseline-diff.md). Старі інструкції нижче, які запускають Community як CinemaCity.API на 5100, стосуються попереднього етапу; для поточного використовуйте `Initialize-Local.ps1` та `Start-Local.ps1 -Mode Services`.

Це той самий CinemaCity: користувачі, фільми й серіали, сезони, акторський склад, TMDb, рецензії, оцінки, watchlist і власні списки. Auth і Catalog виділені з вихідного коду та даних. Тут немає заміни предметної області на демонстраційні товари `Items/Categories`.

## Локальний запуск на наявних даних

Потрібні .NET 10 SDK і PostgreSQL. Щоб запустити монолітний CinemaCity API звичайною командою .NET, перейдіть до його каталогу:

```powershell
cd backend/CinemaCity.API
dotnet run
```

У локальному профілі Development застосунки автоматично читають потрібні їм змінні з ігнорованого `.env` у корені репозиторію. Для повного набору Auth, Catalog, монолітного CinemaCity API, gateway і frontend відкрийте окремі термінали та виконайте [покрокові команди `dotnet run`](docs/step-by-step-defense.md).

PowerShell-скрипт запуску всього набору також доступний, якщо він потрібен. `pg_dump` потрібен один раз для резервної копії перед переходом. У поточній робочій копії після ініціалізації достатньо:

```powershell
.\scripts\Start-Local.ps1
```

Перший запуск у новій робочій копії:

```powershell
# Задайте своє підключення до вихідної БД CinemaCity, а не до чужого demo-каталогу.
$env:CINEMACITY_SOURCE_CONNECTION = 'Host=localhost;Port=5432;Database=cinemacity;Username=<user>;Password=<password>'
.\scripts\Initialize-Local.ps1 -PgDumpPath '<PostgreSQL>\bin\pg_dump.exe'
.\scripts\Start-Local.ps1
```

Ініціалізація створює backup у `.local/backups`, генерує локальні секрети в ігнорованому `.env`, застосовує міграції нових сервісів, одноразово копіює оригінальні дані та наявні uploaded assets. UUID і хеші паролів зберігаються, тому старі облікові записи не потрібно реєструвати повторно. Непорожню ціль без маркера імпорту утиліта не перезаписує. Повторний запуск не скидає вже зроблені зміни сервісів. Backup і `.env` не потрапляють у Git або Docker image.

| Компонент | URL / база |
|---|---|
| Існуючий React frontend | http://localhost:5173 |
| Єдина API-адреса Ocelot | http://localhost:5000 |
| Auth: Swagger, health | http://localhost:5001/swagger, http://localhost:5001/health; `cinemacity_auth` |
| Catalog: Swagger, health | http://localhost:5002/swagger, http://localhost:5002/health; `cinemacity_catalog` |
| CinemaCity API (моноліт) | http://localhost:5100/swagger, http://localhost:5100/health; `cinemacity` |

Зупинка тільки процесів, створених цим скриптом:

```powershell
.\scripts\Stop-Local.ps1
```

Логи — `.logs/local`. Після змін коду спочатку зупиніть локальні процеси, потім виконайте build/test і запустіть знову.

## Як це відповідає лабораторним

- [Інвентаризація модулів, таблиць і залежностей](docs/inventory.md).
- [ADR-0001: межі сервісів та володіння даними](docs/adr/0001-extract-auth-and-catalog.md).
- [ADR-0002: маршрутизація на Ocelot](docs/adr/0002-ocelot-gateway.md).
- [Контейнерна діаграма](docs/diagrams/01-containers.md).
- [Послідовність ЛР0](docs/diagrams/02-sequence-lab0.md).
- [Той самий сценарій після виділення](docs/diagrams/03-sequence-extracted.md).
- [Вихідні файли, з яких виділено сервіси, і SHA-256](docs/extraction-origin.md).
- [Сценарій захисту та результати перевірок](docs/lab-verification.md).
- [Покрокова перевірка та захист ЛР0/ЛР1](docs/step-by-step-defense.md).
- [Контрольні питання й адаптація питання про ціну](docs/control-questions.md).

Режим ЛР0 запускає gateway перед монолітом і не перемикає обробку на сервіси:

```powershell
.\scripts\Stop-Local.ps1
.\scripts\Start-Local.ps1 -Mode Monolith
```

Цей режим використовує окрему навчальну БД `cinemacity_lab0`, скопійовану під час ініціалізації, та окремий JWT-ключ. Демонстраційні записи не змінюють поточні бази сервісів. Поточний код моноліту вже містить інтеграцію ЛР1; вимога «не змінювати моноліт у ЛР0» описує початковий етап переходу, а не забороняє подальші зміни ЛР1. Для роботи з поточними даними використовуйте `-Mode Services`.

## API через gateway

```powershell
Invoke-RestMethod 'http://localhost:5000/health'
Invoke-RestMethod 'http://localhost:5000/api/v1/catalog/items?page=1&pageSize=5&search=movie&sortBy=title&desc=false'
Invoke-RestMethod 'http://localhost:5000/api/movies?page=1&pageSize=5'
```

Нові контракти: `/api/v1/auth/*`, `/api/v1/users`, `/api/v1/users/me`, `/api/v1/users/{id}/roles`, `/api/v1/catalog/items`, `/api/v1/catalog/categories`. Сутність items — оригінальний фільм/серіал CinemaCity; categories — жанри. Старі `/api/auth`, `/api/movies`, `/api/genres`, `/api/admin/...` збережені як alias на gateway для існуючого UI. Публічні профілі, оцінки, рецензії й списки обслуговує моноліт; він також приймає `/api/v1/...`.

`register/login/refresh` повертають access і refresh з термінами дії; refresh додатково встановлюється в HttpOnly cookie для існуючого UI. `logout` вимагає Bearer і повертає 204. JWT перевіряється локально в кожному застосунку. Зміни каталогу та керування користувачами дозволені Admin. Звичайний користувач отримує 403, а запит без токена — 401.

Catalog фільтрує/сортує/сторінкує в PostgreSQL. `pageSize` обмежений 100; неправильний page/pageSize повертає 400 ValidationProblemDetails. Результат має `items/page/pageSize/totalCount` і сумісний зі старим UI `total`. Невідомі пов'язані ID відхиляються. Сутності EF і хеші паролів не є відповідями API.

## Дані та автономність

У кожного сервісу свій DbContext і свої міграції. Auth/Catalog не посилаються на проєкти моноліту або один на одного. `CinemaCity.ServiceDefaults` містить тільки технічні JWT, помилки, CORS, логи та OpenAPI. Runtime не підключається до чужих БД. Міграційна утиліта — окремий offline-процес із тимчасовим доступом до джерела.

Моноліт зберігає власні reference-проєкції для вихідних персональних запитів, а вихідний стан каталогу/ідентичностей заморожено в `legacy`. Новий користувач або фільм отримується через API власника перед використанням у списках та оцінках. Оцінка та outbox записуються локально; статистика Catalog оновлюється з повторною доставкою та перевіркою версії. Ці копії не є дозволом моноліту змінювати дані Auth/Catalog.

## Перевірки

```powershell
.\scripts\Test-All.ps1
```

Тести створюють і видаляють тільки окремі бази `cinemacity_tests_<GUID>` / `cinemacity_e2e_*`. Можна задати інший тестовий PostgreSQL через `CINEMACITY_TEST_POSTGRES`; назва робочої БД завжди замінюється згенерованою. Для тестів потрібне право створювати тестові БД. Перевірка виділення піднімає реальні процеси Auth, Catalog, монолітного API CinemaCity і Ocelot на вільних портах, а не підмінює відповіді mock-сервісами.

## Docker

```powershell
# Після Initialize-Local, з доступним Docker Engine та доступом контейнерів до вихідного PostgreSQL:
docker compose up -d --build
```

Compose спочатку виконує `migrate`: копіює власні дані CinemaCity в новий Docker volume, включно з рецензіями, оцінками та списками, що залишилися в моноліті. Власні бази нових сервісів створюються утилітою. Потім запускаються API, Auth, Catalog, gateway і frontend. Старий volume не видаляється. PostgreSQL цього stack за замовчуванням публікується на 5433, щоб не конфліктувати з локальним 5432.

Перший import виконують до нових записів після cutover. Якщо ви вже тривалий час працювали в native-сервісах і переходите в інше середовище, переносіть усі три поточні бази власників та uploads; заморожений архів моноліту не містить нових користувачів/фільмів і не замінює backup усієї системи. Повторний `compose up` використовує marker і не скидає поточні Docker-дані.

Docker image build і container startup потребують доступного Docker Engine; статус фактичного запуску зафіксований у [перевірках](docs/lab-verification.md). Native-перевірки від Docker не залежать.

## Google і TMDb

Наявні `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `TMDB_API_TOKEN` з `.env` зберігаються ініціалізатором. TMDb-імпорт залишається в Catalog. Без параметрів Google застосунок запускається, але Google-вхід повертає зрозумілий 503. Для реального Google OAuth в консолі провайдера потрібен redirect URI `http://localhost:5000/api/v1/auth/google/signin`; зовнішнє налаштування й обмін реальним Google-кодом не підміняються тестом JWT. Return URL дозволено тільки локальний шлях або origin із `Cors:Origins`.
