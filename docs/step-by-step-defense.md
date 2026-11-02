# Пошаговая проверка и защита ЛР0 и ЛР1

Инструкция для текущего проекта CinemaCity в Windows PowerShell. Откройте PowerShell в корне репозитория `D:\GitHub\CinemaCity`. В текущей копии локальные базы уже подготовлены; повторную инициализацию выполнять не нужно.

## 1. Запуск ЛР0: gateway перед монолитом

Для классического запуска откройте два окна PowerShell для API и gateway, затем третье для frontend. Если предыдущие приложения ещё работают, остановите их `Ctrl+C` в соответствующих окнах.

**Окно 1 — монолит в учебном режиме:**

```powershell
cd D:\GitHub\CinemaCity\backend\CinemaCity.API
$env:ConnectionStrings__DefaultConnection = '<строка подключения к cinemacity_lab0 из .env>'
$env:Jwt__Secret = '<LAB0_JWT_SECRET из .env>'
$env:Services__Enabled = 'false'
dotnet run
```

**Окно 2 — Gateway в режиме ЛР0:**

```powershell
cd D:\GitHub\CinemaCity\backend\CinemaCity.ApiGateway
$env:Gateway__Mode = 'Monolith'
dotnet run
```

**Окно 3 — frontend:**

```powershell
cd D:\GitHub\CinemaCity\frontend
npm run dev -- --host 127.0.0.1 --port 5173
```

В этом режиме Auth и Catalog обслуживает монолит; запросы через gateway сохраняют исходные пути. Не запускайте одновременно другие процессы на портах `5100`, `5000` и `5173`.

Проверьте приложение в браузере:

- CinemaCity: http://localhost:5173
- Swagger монолита: http://localhost:5100/swagger
- Health монолита: http://localhost:5100/health
- Health gateway: http://localhost:5000/health

В PowerShell проверьте статусы и получение каталога:

```powershell
Invoke-RestMethod 'http://localhost:5000/health'
$direct = Invoke-RestMethod 'http://localhost:5100/api/movies?page=1&pageSize=5'
$throughGateway = Invoke-RestMethod 'http://localhost:5000/api/movies?page=1&pageSize=5'
$direct | ConvertTo-Json -Depth 30 -Compress
$throughGateway | ConvertTo-Json -Depth 30 -Compress
```

Оба запроса каталога должны вернуть одни и те же данные. Для наглядной проверки маршрутизации добавьте correlation ID:

```powershell
Invoke-WebRequest 'http://localhost:5000/api/movies?page=1&pageSize=2' -Headers @{ 'X-Correlation-Id' = 'lab0-check-01' } | Select-Object StatusCode, Headers
```

Ожидайте HTTP 200 и заголовок `X-Correlation-Id: lab0-check-01` в ответе. В `.logs/local/CinemaCity.ApiGateway.log` найдите `lab0-check-01`: запись показывает путь, получателя, статус и длительность. Не ищите там пароль или тело запроса — middleware их не записывает.

В Swagger монолита (`http://localhost:5100/swagger`) можно показать исходные операции CinemaCity. Внешний клиент обращается к `5000`, а gateway перенаправляет запрос на `5100`. Для демонстрации записи используйте только учебную базу `cinemacity_lab0`; не удаляйте и не меняйте данные рабочей базы.

## 2. Запуск ЛР1: Auth и Catalog выделены из CinemaCity

Откройте **четыре отдельных окна PowerShell**. В каждом перейдите в папку приложения и выполните `dotnet run`. При локальном запуске с профилем Development приложение само читает настройки из игнорируемого `.env` в корне репозитория; отдельный скрипт и ручное копирование ключей не нужны.

**Окно 1 — Auth:**

```powershell
cd D:\GitHub\CinemaCity\backend\CinemaCity.AuthService
dotnet run
```

**Окно 2 — Catalog:**

```powershell
cd D:\GitHub\CinemaCity\backend\CinemaCity.CatalogService
dotnet run
```

**Окно 3 — CinemaCity API (монолит):**

```powershell
cd D:\GitHub\CinemaCity\backend\CinemaCity.API
dotnet run
```

**Окно 4 — Ocelot Gateway:**

```powershell
cd D:\GitHub\CinemaCity\backend\CinemaCity.ApiGateway
dotnet run
```

Оставьте все четыре окна открытыми. Запускайте их именно на портах 5001, 5002, 5100 и 5000 соответственно. Затем откройте новое окно PowerShell для frontend:

```powershell
cd D:\GitHub\CinemaCity\frontend
npm run dev -- --host 127.0.0.1 --port 5173
```

Если появится `Failed to bind ... address already in use`, этот порт уже занят другим запуском. Остановите приложение сочетанием `Ctrl+C` в его терминале; не запускайте второй процесс на том же порту. Если появится ошибка о `Jwt:Secret`, проверьте, что в корне репозитория есть локальный `.env` и запускается профиль `Development` из `Properties/launchSettings.json`.

Перед переходом к ЛР1 остановите окна ЛР0 сочетанием `Ctrl+C`. Затем запустите четыре API-команды из предыдущих блоков и frontend в отдельном окне. Теперь работают gateway `5000`, Auth `5001`, Catalog `5002`, исходный CinemaCity API/монолит `5100` и frontend `5173`. Это тот же CinemaCity: `Movie/Genre` — исходные фильмы и жанры приложения, а оценки, рецензии и списки остаются в монолите.

Проверьте health напрямую и через gateway:

```powershell
foreach ($port in 5000, 5001, 5002, 5100) {
    try { "${port}: $((Invoke-WebRequest "http://localhost:$port/health").StatusCode)" }
    catch { "${port}: ERROR" }
}
```

Ожидаемый результат — `200` для всех четырёх портов. Откройте документацию API:

- Auth Swagger: http://localhost:5001/swagger
- Catalog Swagger: http://localhost:5002/swagger
- Swagger исходного монолита CinemaCity API: http://localhost:5100/swagger
- Единый внешний адрес API: http://localhost:5000
- Frontend: http://localhost:5173

Новые внешние маршруты gateway — `/api/v1/auth/...`, `/api/v1/users...` и `/api/v1/catalog/...`. Например, сравните новый версионный маршрут каталога со старым адресом интерфейса:

```powershell
$catalog = Invoke-RestMethod 'http://localhost:5000/api/v1/catalog/items?page=1&pageSize=5&sortBy=title&desc=false'
$legacy = Invoke-RestMethod 'http://localhost:5000/api/movies?page=1&pageSize=5'
$catalog | ConvertTo-Json -Depth 20
$legacy | ConvertTo-Json -Depth 20
```

Оба маршрута должны возвращать фильмы/сериалы CinemaCity. В текущей перенесённой базе активны 220 позиций; при изменении локальных данных число может отличаться. Жанры проверяются так:

```powershell
Invoke-RestMethod 'http://localhost:5000/api/v1/catalog/categories'
```

## 3. Проверка регистрации, JWT и ролей

На Swagger Auth найдите `POST /api/v1/auth/login` или `POST /api/v1/auth/register`. Для существующей учётной записи выполните вход. Если регистрируете нового пользователя для демонстрации, помните: запись останется в локальной базе после проверки.

Формат регистрации:

```json
{
  "username": "student-demo",
  "email": "student-demo@example.test",
  "password": "DemoPass123!",
  "confirmPassword": "DemoPass123!"
}
```

Формат входа:

```json
{
  "email": "student-demo@example.test",
  "password": "DemoPass123!"
}
```

Успешный вход возвращает HTTP 200, пользователя, access token и refresh token со сроками действия. Скопируйте `accessToken`, нажмите **Authorize** в Swagger и введите `Bearer <accessToken>` (если Swagger уже добавляет префикс Bearer, введите только сам токен).

Покажите разницу между аутентификацией и правами:

- `GET http://localhost:5000/api/v1/users/me` без токена → HTTP 401.
- Тот же запрос с токеном → HTTP 200 и данные текущего пользователя.
- `GET /api/v1/users` под ролью `User` → HTTP 403; под ролью `Admin` → HTTP 200 и страница пользователей.
- `POST /api/v1/catalog/items` без токена → HTTP 401; с токеном `User` → HTTP 403; с токеном `Admin` и корректным DTO → HTTP 201.
- Неверные данные регистрации или пагинации → HTTP 400 с описанием ошибок.

Не меняйте роль существующей учётной записи во время демонстрации без необходимости. Для сценария Admin используйте известную вам исходную администраторскую учётную запись. `PUT /api/v1/users/{id}/roles` доступен только Admin.

В Swagger Auth также можно показать `POST /api/v1/auth/refresh`: передайте refresh token из входа. Первый запрос должен вернуть новую пару токенов; повторное использование старого refresh token должно завершиться HTTP 401. `POST /api/v1/auth/logout` требует access token и при успехе возвращает HTTP 204.

## 4. Показать, что сервисы выделены из монолита

На защите откройте эти файлы в редакторе:

- `docs/inventory.md` — какие модули и таблицы принадлежат Auth, Catalog и оставшемуся монолиту CinemaCity API.
- `docs/extraction-origin.md` — список исходных файлов CinemaCity и их SHA-256.
- `docs/adr/0001-extract-auth-and-catalog.md` — почему выделены именно эти границы.
- `docs/adr/0002-ocelot-gateway.md` — решение по Ocelot, явным маршрутам и catch-all.
- `docs/diagrams/01-containers.md` — приложения, порты и базы.
- `docs/diagrams/02-sequence-lab0.md` и `docs/diagrams/03-sequence-extracted.md` — один сценарий до и после выделения.
- `docs/control-questions.md` — ответы на контрольные вопросы.

Покажите конфигурацию маршрутов `backend/CinemaCity.ApiGateway/ocelot.json`: у конкретных маршрутов приоритет выше, чем у `/api/{everything}`. Исходный frontend остался CinemaCity и использует базовый адрес gateway; проверить его можно на `http://localhost:5173`.

## 5. Показать разделение данных

В pgAdmin или `psql` проверьте три базы:

- `cinemacity_auth` — пользователи, роли и refresh-сессии; владелец Auth на `5001`.
- `cinemacity_catalog` — фильмы/сериалы и связанные справочники; владелец Catalog на `5002`.
- `cinemacity` — оценки, рецензии, комментарии и списки; оставшийся монолит CinemaCity API на `5100`.

В проекте каждому владельцу соответствуют собственные `DbContext`, connection string и миграции. Auth и Catalog во время обычной работы не подключаются к чужим базам. Offline-утилита переноса используется только для миграции данных. Подробная таблица перенесённых записей находится в `docs/lab-verification.md`.

## 6. Проверка ошибок и логов

Проверьте анонимный запрос к личному профилю и неправильный параметр страницы:

```powershell
try { Invoke-RestMethod 'http://localhost:5000/api/v1/users/me' }
catch { "Ожидаемая ошибка без токена: HTTP $([int]$_.Exception.Response.StatusCode)" }

try { Invoke-RestMethod 'http://localhost:5000/api/v1/catalog/items?page=0&pageSize=1000' }
catch { "Ожидаемая ошибка валидации: HTTP $([int]$_.Exception.Response.StatusCode)" }
```

Для логов откройте `.logs/local/CinemaCity.ApiGateway.log`, `.logs/local/CinemaCity.AuthService.log`, `.logs/local/CinemaCity.CatalogService.log` и `.logs/local/CinemaCity.API.log`. Ищите один и тот же `X-Correlation-Id`. Секреты, парольные тела и refresh/access-токены в логах показывать не нужно.

## 7. Завершение демонстрации

Остановите созданные проектом процессы:

```powershell
.\scripts\Stop-Local.ps1
```

Чтобы позже вернуться к рабочему режиму ЛР1, выполните `Start-Local.ps1 -Mode Services`; для показа этапа ЛР0 — `Start-Local.ps1 -Mode Monolith`. Перед переключением всегда сначала запускайте `Stop-Local.ps1`.

## Что честно сказать преподавателю

Auth и Catalog выделены из исходного CinemaCity, а не из стороннего примера: это подтверждают модели Movie/Genre, исходные сценарии, снимок данных и перечень исходных файлов. В исходном монолите остались рейтинги, рецензии, комментарии, профили и списки, связанные с фильмами и пользователями.

В исходном CinemaCity нет заказов и цены товара. Поэтому `Movie/Genre` сохранены как доменные аналоги лабораторных `Item/Category`; правило фиксации цены при создании заказа описано как общее контрольное объяснение. Если преподаватель требует именно поле цены и сценарий заказа, это отличие нужно согласовать отдельно: добавление магазина изменит предметную область проекта.

Текущий код монолита уже включает интеграцию ЛР1. Режим `Monolith` позволяет проверить маршрутизацию и поведение gateway этапа ЛР0 на отдельной учебной базе, но не означает, что файлы монолита после выполнения ЛР1 остались побайтно неизменными. Docker Compose подготовлен, однако запуск контейнеров на этой машине фактически не проверен из-за недоступного Docker Engine; native-запуск проверен отдельно.
