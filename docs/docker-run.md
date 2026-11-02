# Запуск CinemaCity через Docker

Перевірено 2026-11-02 на цьому комп’ютері. Клієнт: http://localhost:5174/catalog, gateway readiness: http://localhost:5000/health/ready. Порт клієнта задає `FRONTEND_PORT` у локальному `.env`.

## Повторний запуск підготовленого стенда

У PowerShell з каталогу `E:\CinemaCity`, коли Docker Desktop працює:

```powershell
docker compose up -d
docker compose ps -a
```

`postgres` має бути healthy, Auth/Catalog/Community/gateway/frontend — running. `migrate` завершується з кодом 0: це одноразова утиліта, а не постійний сервіс. Повторний імпорт за маркером пропускається та зберігає подальші зміни.

Зупинка зі збереженням даних:

```powershell
docker compose stop
```

Після зміни коду образи потрібно перебудувати. На машині з обмеженою пам’яттю збирайте по черзі:

```powershell
foreach ($cinemaService in @('migrate','auth','catalog','api','gateway','frontend')) {
    docker compose build $cinemaService
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $cinemaService" }
}
docker compose up -d
```

## Дані цього стенда

На сервері PostgreSQL Windows база `cinemacity` під час запуску не була доступна. Наявну резервну копію `.local/backups/cinemacity-20261015-191210.dump` від 15 жовтня відновлено в нову базу `cinemacity_source` всередині Docker. Звідти утиліта перенесла дані у `cinemacity_auth`, `cinemacity_catalog`, `cinemacity_community`, зберігши UUID і хеші паролів. Джерело імпорту читається в read-only транзакції. Інші бази Windows не змінювалися.

Це окремий набір даних зі станом на дату резервної копії, без автоматичної синхронізації з Windows PostgreSQL. Усі чотири бази Docker зберігаються у volume `cinemacity_services_postgres_data`. Не видаляйте цей volume, якщо потрібні дані стенда. Новий клон репозиторію потребує власного `.env` та підготовленого джерела даних: резервна копія й секрети в Git не входять.

## Що перевірено

- Побудовано всі шість образів, PostgreSQL 18 Alpine проходить healthcheck.
- Імпорт завершився успішно; повторний запуск зберігає дані.
- Readiness gateway: Auth, Catalog і Community доступні.
- Через nginx клієнта перевірено реєстрацію, вхід, каталог, складені деталі, створення оцінки, повтор з тим самим ключем без дубля, власні оцінки, скасування, HttpOnly refresh cookie та вихід.
- Тимчасовий тестовий акаунт прибрано. Каталог містить 220 доступних записів із резервної копії.
- Захищені 96 файлів моноліту не змінені.

Для запуску виправлено конфлікт `appsettings.json` при публікації утиліти імпорту, додано кеш пакетів збірки та використано робочий образ `postgres:18-alpine`. Початкову збірку перервав збій Docker Desktop/WSL; після перезапуску Docker стенд запущено та перевірено. Це уточнює попередній статус Docker в аудиті лабораторних.
