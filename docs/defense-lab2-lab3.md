# Захист ЛР2 та ЛР3: спіч, порядок показу, команди

Розрахунок: 15–20 хвилин. Якщо часу мало: БД → створення та повтор оцінки → тести ЛР2 → складений екран → кеш → часткова відповідь → 429 → тести ЛР3.

## Перед захистом

Відкрий три вікна: **A — керування стендом**, **B — запити і тести**, **C — pgAdmin/DBeaver**. Також потрібні редактор і браузер. Команди розраховані на PowerShell 7 (`pwsh`).

Поточний .env ще посилається на не підготовлені тут бази з іншого ноутбука. Для відтворюваної демонстрації додано окремий стенд: він створює навчальне джерело, справжнім offline-імпортером переносить дані у три окремі БД і запускає справжні сервіси. Це не HTTP-стаби. Не називай ці записи старими робочими даними з іншого ноутбука.

### Вікно A — запуск

```powershell
cd E:\CinemaCity
$env:DOTNET_PROCESSOR_COUNT = '2'
$pgPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Пароль PostgreSQL' -AsSecureString)).Password
$env:CINEMACITY_TEST_POSTGRES = "Host=127.0.0.1;Port=5432;Username=postgres;Password=$pgPassword"
Remove-Variable pgPassword
dotnet build tools/CinemaCity.DefenseDemo -m:1 -p:UseSharedCompilation=false
dotnet tools/CinemaCity.DefenseDemo/.build/bin/Debug/net10.0/CinemaCity.DefenseDemo.dll
```

Після `demo>` стенд готовий. Команда `info` показує назви баз і UUID. Не закривай це вікно. Якщо порт зайнятий, закрий попередній локальний запуск: стенд не зупиняє чужі процеси.

### Вікно B — підготовка сесії

```powershell
cd E:\CinemaCity
. .\scripts\Defense-Session.ps1
```

Це створить `$base`, `$movieId`, `$seriesId`, `$ownerHeaders`, `$adminHeaders`, `$defense` та функцію `Send-DefenseRequest`. JWT отримуються автоматично для двох навчальних облікових записів і не друкуються. Після перезапуску стенда повтори команду: ID змінюються.

Відкрий:

- Gateway Swagger: http://localhost:5000/swagger
- Community Swagger: http://localhost:5003/swagger — локальна діагностика.
- Catalog Swagger: http://localhost:5002/swagger — локальна діагностика.

У GUI БД підключись до localhost:5432, користувач postgres, свій пароль. Назви БД — у вікні A або `$defense | Format-List`. Вони мають префікс `cinemacity_e2e_` і випадкові суфікси.

## ЛР2: готовий спіч

> У другій лабораторній я виніс операційний модуль CinemaCity в окремий CommunityService. У моїй предметній області основні дії користувача — оцінки, рецензії, коментарі та власні списки фільмів. Тому я виносив наявну логіку, а не додавав штучне бронювання квитків.
>
> Community має окремий процес і власну базу. Користувачами володіє Auth, фільмами — Catalog. До їхніх даних Community звертається через HTTP; локальні projections потрібні для збереження наявних читальних запитів. Між базами немає SQL-з'єднань і зовнішніх ключів.
>
> Для клієнта старі маршрути через Ocelot залишилися. Я зберіг бізнес-перевірки та формати відповідей, додав дедлайни й атомарну ідемпотентність створення. Поведінку перевіряють інтеграційні тести та порівняння з незміненим у цій роботі монолітом. Сам моноліт під час цієї роботи не редагувався.

## ЛР2: що показати по кроках

### 1. Межі сервісу

Відкрий `docs/adr/0003-community-service.md`, таблицю «дані / власник».

**Скажи:** «Оцінками та списками володіє Community. Каталог змінює Catalog, користувачів — Auth. Шов проходить по HTTP API та стабільних ідентифікаторах».

### 2. БД та перенесення даних

Покажи чотири БД: source, auth, catalog, community. У **source**, потім у **community** виконай:

```sql
SELECT current_database();
SELECT id, user_id, movie_id, content, status FROM public.reviews;
```

Є одна початкова рецензія: її UUID, посилання та текст збігаються. Це доказ перенесення зі збереженням ідентифікаторів.

У **community**:

```sql
SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema IN ('public', 'projections')
ORDER BY table_schema, table_name;

SELECT * FROM public.cinemacity_imports;
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```

Покажи ratings, reviews, comments, watchlist_items, user_movie_lists, operation_replays, catalog_statistics_outbox; окремо projections.

**Скажи:** «Джерело читається offline-утилітою у read-only транзакції. Runtime підключається тільки до своєї БД. Import marker захищає дані від повторного перезапису».

Не кажи, що FK немає взагалі: у Community є FK до локальних projections у тій самій БД. Заборонених FK між окремими базами немає.

### 3. Контекст, міграції, маршрути

У редакторі відкрий:

- `backend/CinemaCity.CommunityService/Infrastructure/Data/AppDbContext.cs`;
- папку міграцій поруч, зокрема `20261029020000_OperationReplays.cs`;
- `backend/CinemaCity.ApiGateway/ocelot.json`: операційний catch-all має `Metadata.Service = Community`;
- `GatewayConfiguration.cs`: адреса з `Services:CommunityUrl`.

**Скажи:** «Community — окремий процес на 5003, але клієнт усе ще викликає `/api/ratings` через 5000».

### 4. Живі сервіси та Swagger

У B:

```powershell
Send-DefenseRequest -Path '/health'
Send-DefenseRequest -Path '/health/ready'
```

Очікуй 200. Readiness показує Auth, Catalog, Community. У Community Swagger покажи Ratings, Reviews, Profile та Bearer.

### 5. Створення та ідемпотентність

```powershell
$ratingBody = @{ movieId=$movieId; score=6 }
$createHeaders = $ownerHeaders.Clone()
$createHeaders['Idempotency-Key'] = [guid]::NewGuid().ToString()
$first = Send-DefenseRequest -Method POST -Path '/api/ratings' -Headers $createHeaders -Body $ratingBody -PassThru
$ratingId = ($first.Content | ConvertFrom-Json).id
Send-DefenseRequest -Method POST -Path '/api/ratings' -Headers $createHeaders -Body $ratingBody
```

Очікуй двічі **201**, однакові id, JSON і Location. У Community:

```sql
SELECT id, user_id, movie_id, score FROM public.ratings;
SELECT scope, status, created_at FROM public.operation_replays;
```

Оцінка одна. У source `SELECT count(*) FROM public.ratings;` досі повертає 0.

Покажи `CommunityService/Integration/IdempotencyMiddleware.cs`.

**Скажи:** «Ключ обмежений користувачем та маршрутом. SHA-256 перевіряє незмінність тіла. Advisory lock серіалізує конкурентні повтори. Операція й збережена відповідь комітяться однією PostgreSQL-транзакцією».

```powershell
Send-DefenseRequest -Method POST -Path '/api/ratings' -Headers $createHeaders -Body @{ movieId=$movieId; score=7 }
```

Очікуй **409**: той самий ключ з іншим тілом.

### 6. Помилки, редагування, видалення

```powershell
# 401 — без токена. ProblemDetails на gateway додано в ЛР3.
Send-DefenseRequest -Method POST -Path '/api/ratings' -Body $ratingBody
# 400 — порушення бізнес-правила.
Send-DefenseRequest -Method POST -Path '/api/ratings' -Headers $ownerHeaders -Body @{ movieId=$movieId; score=0 }
# 404 — неіснуючий фільм.
Send-DefenseRequest -Method POST -Path '/api/ratings' -Headers $ownerHeaders -Body @{ movieId=([guid]::NewGuid().ToString()); score=6 }
# 200 — зміна оцінки.
Send-DefenseRequest -Method PUT -Path "/api/ratings/$ratingId" -Headers $ownerHeaders -Body @{ score=9 }
# 204 — видалення.
Send-DefenseRequest -Method DELETE -Path "/api/ratings/$ratingId" -Headers $ownerHeaders
```

**Скажи:** «Бал має бути від 1 до 10, фільм повинен існувати, UserId береться з JWT, а не з тіла запиту».

### 7. Недоступний Catalog

У **A**, у запрошенні `demo>`:

```text
stop catalog
```

У B:

```powershell
Send-DefenseRequest -Method POST -Path '/api/ratings' -Headers $ownerHeaders -Body @{ movieId=$movieId; score=6 }
Send-DefenseRequest -Path '/health'
Send-DefenseRequest -Path '/health/ready'
```

Очікуй 503 для операції, 200 для liveness шлюзу та 503 для readiness.

**Скажи:** «Недоступність сервісу не плутається з відсутністю фільму. Операція не створюється. Дедлайн Community — 5 секунд; з ЛР3 власний downstream-дедлайн gateway — 3 секунди».

У A віднови:

```text
start catalog
```

### 8. Baseline та тести

Покажи `tests/baseline/` — 11 знімків; потім `docs/baseline-diff.md`.

**Скажи:** «Контрольний моноліт і нова система працюють на окремих БД. Для помилок порівнюються статуси й тіла. Для успішних операцій окремо враховуються згенеровані ID та UTC-час, а Location перевіряється щодо створеного ID».

У B окремо задай підключення до PostgreSQL для тестів:

```powershell
$env:DOTNET_PROCESSOR_COUNT = '2'
$pgPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Пароль PostgreSQL для тестів' -AsSecureString)).Password
$env:CINEMACITY_TEST_POSTGRES = "Host=127.0.0.1;Port=5432;Username=postgres;Password=$pgPassword"
Remove-Variable pgPassword
dotnet test backend/CinemaCity.Extraction.Tests -m:1 -p:UseSharedCompilation=false
```

Очікуй **14/14**. Ці тести мають випадкові порти й інші тимчасові БД та не зачіпають запущене демо.

Якщо часу мало:

```powershell
dotnet test backend/CinemaCity.Extraction.Tests --no-build --filter 'FullyQualifiedName~CommunityBaseline|FullyQualifiedName~Idempotency|FullyQualifiedName~CatalogOutage'
```

## ЛР3: готовий спіч

> У третій лабораторній я розширив Ocelot із маршрутизатора до вхідного шлюзу системи. Реалізував складений екран фільму: шлюз паралельно отримує опис із Catalog та мою оцінку і списки з Community та формує власний DTO.
>
> Catalog є обов'язковою частиною екрана. Якщо недоступний тільки Community, клієнт отримує явно часткову відповідь. Це не плутається з ситуацією, коли в користувача просто немає оцінки.
>
> На вході шлюз перевіряє JWT, обмежує частоту запитів і задає дедлайни. Кешуються тільки спільні деталі фільму. Персональний стан і повна складена відповідь не кешуються. Після успішної зміни каталогу кеш інвалідизується. Бізнес-перевірки залишилися у власників даних.

## ЛР3: що показати по кроках

### 1. Складений екран і паралельність

Після видалення оцінки в ЛР2 створи її заново:

```powershell
Send-DefenseRequest -Method POST -Path '/api/ratings' -Headers $ownerHeaders -Body @{ movieId=$movieId; score=8 }
$screenPath = "/api/v1/screens/movies/$movieId"
Send-DefenseRequest -Path $screenPath -Headers $ownerHeaders
```

Очікуй 200: movie, myState.myScore=8, partial=false, communityStatus=available, Cache-Control: no-store.

Покажи `ApiGateway/ScreenComposition.cs`: власні MovieScreen/ScreenUserState та два завдання перед Task.WhenAll.

**Скажи:** «Один запит клієнта збирає два сервіси. Час очікування приблизно дорівнює найдовшому виклику, а не сумі двох. Розрахунок рейтингу сюди не перенесено».

### 2. Кеш та його межі

Два запити поспіль, до завершення TTL:

```powershell
Send-DefenseRequest -Path $screenPath -Headers $ownerHeaders
Send-DefenseRequest -Path $screenPath -Headers $ownerHeaders
```

У A:

```text
logs catalog
logs community
```

Нових GET деталей у Catalog немає, поки спрацьовує кеш; Community отримує новий GET персонального стану щоразу. Якщо 60 секунд уже минули, додатковий Catalog GET нормальний — виконай ще два запити поспіль.

Покажи CatalogCache у ScreenComposition.cs.

**Скажи:** «Ключ містить покоління і канонічний шлях із UUID, TTL — 60 секунд. Catalog отримує запит без Authorization/Cookie. Персональне не кешую; звичайні API зі статистикою оцінок теж не кешую, щоб зберегти їхню актуальність».

### 3. Два користувачі

```powershell
$name = 'defense_' + [guid]::NewGuid().ToString('N').Substring(0,8)
$secondSession = Invoke-RestMethod "$base/api/auth/register" -Method POST -ContentType 'application/json' -Body (@{ username=$name; email="$name@example.test"; password=$demoPassword; confirmPassword=$demoPassword } | ConvertTo-Json)
$secondHeaders = @{ Authorization="Bearer $($secondSession.accessToken)" }
Send-DefenseRequest -Method POST -Path '/api/ratings' -Headers $secondHeaders -Body @{ movieId=$movieId; score=4 }
Send-DefenseRequest -Path $screenPath -Headers $ownerHeaders
Send-DefenseRequest -Path $screenPath -Headers $secondHeaders
```

Той самий movie.id, але myScore — **8** та **4**.

**Скажи:** «Спільну частину перевикористовую, персональну щоразу отримую для користувача з JWT. Дані між користувачами не змішуються».

### 4. Інвалідизація

```powershell
$movieUpdate = @{
  title='CinemaCity — захист ЛР3'; originalTitle='Original CinemaCity film'
  contentType='Movie'; releaseYear=2024; duration=100
  description='Навчальне редагування для демонстрації кешу'
  posterUrl=$null; backdropUrl=$null; trailerYoutubeId=$null
  genreIds=@(1); countryIds=@(); languageIds=@(); cast=@()
}
Send-DefenseRequest -Method PUT -Path "/api/v1/catalog/items/$movieId" -Headers $adminHeaders -Body $movieUpdate
Send-DefenseRequest -Path $screenPath -Headers $ownerHeaders
```

Очікуй 204, потім одразу 200 з новою назвою. `logs catalog` покаже новий GET.

Покажи DownstreamLoggingHandler у GatewayMiddleware.cs.

**Скажи:** «Покоління кешу змінюється після успішного запису, до повернення відповіді. GET старого покоління, який ще виконується, не зможе повернути застарілі дані в новий кеш».

### 5. Недоступний Community

У A:

```text
stop community
```

У B:

```powershell
Send-DefenseRequest -Path $screenPath -Headers $ownerHeaders
```

Очікуй 200, partial=true, myState=null, communityStatus=unavailable або timeout. Movie залишається.

**Скажи:** «Null тут не означає відсутність оцінки. Окремий статус пояснює збій отримання персональної частини».

У A поверни `start community`.

Якщо просять зупинити Catalog: у A — `stop catalog`, у B виклич **інший, ще не кешований** об'єкт:

```powershell
Send-DefenseRequest -Path "/api/v1/screens/movies/$seriesId" -Headers $ownerHeaders
```

Очікуй 503. При зависанні — 504, цей випадок детерміновано показує тест. Для теплого кешу опис може залишатися доступним до TTL. Поверни `start catalog`.

### 6. JWT до downstream

```powershell
Send-DefenseRequest -Path $screenPath
```

Очікуй 401 ProblemDetails. Покажи GatewayFeatures.cs: перевірку до композиції та видалення сторонніх X-User-Id/X-User-Roles.

**Скажи:** «Gateway передає перевірений JWT; UserId із тіла запиту не використовується. Власну перевірку токена сервіси зберегли як додатковий захист».

### 7. Ліміт і Retry-After

Виконай наприкінці, щоб раніше не витратити бюджет login:

```powershell
1..6 | ForEach-Object {
  Send-DefenseRequest -Method POST -Path '/api/auth/login' -Body @{ email='nobody@demo.invalid'; password='NoSuchAccount123!' }
}
```

Спочатку 401, після вичерпання ліміту — **429** з Retry-After та ProblemDetails. Неіснуючий обліковий запис не спричиняє блокування реального користувача.

Покажи GatewayPolicies.ConfigureLimits у GatewayFeatures.cs.

**Скажи:** «Fixed window: анонім — 120 за хвилину на IP, авторизований — 240 на sub, login — 5 на IP. До входу userId немає; після входу sub розділяє користувачів за спільною IP».

### 8. Закриті порти — не плутати режими

Покажи docker-compose.yml: у Auth/Catalog/Community є expose, але немає ports. У контейнерному режимі прямі сервісні порти не публікуються на хості.

**Поточний навчальний стенд локальний:** localhost:5002 у ньому працюватиме. Не видавай його за ізоляцію Docker. Раніше Docker тут мав помилку read-only filesystem; контейнерний запуск не підтверджений. Якщо потрібна саме жива перевірка закритих портів, спочатку треба відновити Docker і підняти Compose з доступним джерелом даних. Поточне демо цього не підміняє.

### 9. Swagger та тести

У http://localhost:5000/swagger покажи складений маршрут, Authorize, власну схему DTO, приклади повної/часткової відповіді, 401/404/429/503/504 і Retry-After.

```powershell
dotnet test backend/CinemaCity.Gateway.Tests -m:1 -p:UseSharedCompilation=false
```

Очікуй **7/7**. У GatewayTests.cs покажи назви тестів.

**Скажи:** «Gateway.Tests запускає справжній gateway через WebApplicationFactory і керовані HTTP-стаби на Kestrel: так відтворюються відмова та timeout. Extraction.Tests запускає реальні процеси сервісів і PostgreSQL».

За наявності часу:

```powershell
dotnet test CinemaCity.slnx -m:1 -p:UseSharedCompilation=false
```

Очікуй **25/25**: 14 сервісних + 7 gateway + 4 монолітних. Спільний fixture старих сценаріїв має підвищений login-ліміт через численні логіни між тестами. Політика 429 перевіряється окремо; у живому демо залишається 5/хв.

## Короткі відповіді на незручні питання

**Де історичний baseline?** «Знімки отримані з незміненого в цій сесії моноліту. Відповідей перед попередньою роботою на іншому ноутбуці немає. Це зазначено в baseline-diff; я не видаю нові знімки за старий коміт».

**Чому змінилося тіло 401?** «ЛР3 вимагає ProblemDetails на шлюзі. Цю відмінність описано окремо; бізнес-правила сервісів не переносилися».

**Чому JWT перевіряється і в сервісах?** «Gateway відсікає запит на вході. Захист сервісів залишено як defense in depth; неперевіреним identity-заголовкам ми не довіряємо».

**Це старі робочі дані?** «Це ізольований навчальний набір на моделі CinemaCity, перенесений справжньою утилітою. Робочі дані з іншого ноутбука ним не підміняються».

**Навіщо projections?** «Це локальні read-моделі, що оновлюються через API власників і підтримують наявні запити. Community не пише в чужі БД».

**Як Catalog отримує новий середній рейтинг?** «Через persisted outbox Community. Після збою доставку можна повторити; версія повідомлення захищає від повторного застосування».

**Чому не кешувати весь екран?** «Він містить персональні дані. Тому myState отримується щоразу, а вся складена відповідь має no-store».

## Завершення

> ЛР2 відокремила процес і власність операційних даних, зберігши клієнтські маршрути та бізнес-поведінку. ЛР3 додала композицію, JWT, кеш, ліміти та дедлайни на gateway, залишивши бізнес-правила у сервісах. Обидва етапи перевіряються автоматичними тестами.

У вікні A введи `exit` і дочекайся повідомлення про видалення тимчасових БД.
