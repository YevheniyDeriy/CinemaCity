# Інвентаризація вихідного моноліту CinemaCity

Це інвентаризація фактичних Minimal API модулів, а не приклад зі стороннім магазином. Вихідні файли та їх SHA-256 зафіксовано в [extraction-origin.md](extraction-origin.md).

| Модуль моноліту | Сутності | Таблиці | Хто ще читає / пише | Власник після ЛР1 |
|---|---|---|---|---|
| AuthEndpoints | User, RefreshToken, UserRole enum | users, refresh_tokens | Profile читає ідентичність; Reviews, Ratings, Comments, Lists посилаються на UserId | Auth; нові roles/user_roles зберігають набір ролей |
| MovieEndpoints | Movie, Genre, Country, Language, MovieCast, Person | movies, genres, countries, languages, movie_genres, movie_countries, movie_languages, movie_cast, persons | Search і Home читають метадані; Lists, Reviews та Ratings використовують MovieId | Catalog |
| SeriesEndpoints | Movie, Season, Episode | movies, seasons, episodes | Ratings пише оцінки епізоду; профіль показує стан перегляду серіалу | Catalog — метадані; Community — оцінки |
| AdminEndpoints | Genre, Person, Movie | genres, persons, movies, movie_cast; файли wwwroot/uploads | Movie/Series/Search читають довідники й зображення | Catalog |
| AdminImportEndpoints / TmdbImportService | Movie, Season, Episode, Genre, Country, Language, Person, MovieCast | увесь набір каталогу | Імпорт перевіряє наявність оцінених епізодів, щоб їх не знищити | Catalog; наявність оцінок запитується через API Community |
| SearchEndpoints | Movie, Episode, Person | movies, episodes, seasons, persons | Читання тільки каталогу | Catalog |
| HomeEndpoints | Movie, MovieCast, Rating, WatchlistItem | movies, movie_cast, movie_genres, ratings, watchlist_items | Персональні рекомендації поєднують каталог та активність користувача | Community; метадані — локальна проєкція з API Catalog |
| RatingEndpoints | Rating, Movie, Episode, WatchlistItem | ratings, movies, episodes, watchlist_items | Home використовує оцінки; Catalog показує середні значення; Profile читає власні оцінки | Community; агрегати надсилаються Catalog через outbox/API |
| ReviewEndpoints | Review, Comment, ReviewLike, User, Movie | reviews, comments, review_likes, users, movies | Profile показує схвалені рецензії, адміністратор їх модерує | Community; імена/назви — проєкції з API власників |
| ProfileEndpoints | User, Rating, Review, WatchlistItem, UserMovieList, UserMovieListItem | users, ratings, reviews, watchlist_items, user_movie_lists, user_movie_list_items | Home читає перегляди для рекомендацій | Community |

Приховані залежності, які потрібно було зберегти під час виділення:

1. Новий UserId, створений Auth, має працювати в старих списках та оцінках. Моноліт отримує ідентичність з API Auth і підтримує власний запис-посилання в `projections.users`.
2. Новий MovieId/EpisodeId із Catalog має працювати в рецензіях і переглядах. Дані надходять через версіонований snapshot API Catalog до локальних проєкцій моноліту.
3. Власна оцінка епізоду не належить Catalog. Відповідь із сезонами доповнюється викликом API Community із JWT користувача.
4. Зміна оцінки та доступність Catalog не повинні бути однією розподіленою транзакцією. Подія з агрегатом записується разом із локальною зміною і повторюється до успішної доставки; номер події захищає від повторів і застарілих оновлень.
5. Завантажені постери — частина вихідного застосунку. Ініціалізація копіює наявні файли, а gateway та frontend обслуговують ті самі `/uploads/...`.

Спільних зовнішніх ключів або JOIN між базами сервісів немає. Локальні JOIN у Community використовують його власні read-моделі, а не таблиці Auth/Catalog. Архів `legacy` більше не є джерелом поточних змін.
