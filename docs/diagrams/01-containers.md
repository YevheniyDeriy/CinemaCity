# Композиція CinemaCity після ЛР2

CommunityService винесено в окремий процес з власною БД. Моноліт залишається доступним окремо для режиму ЛР0. Бази можуть бути на одному PostgreSQL, але підключення й схеми належать окремим застосункам. Зовнішній порт PostgreSQL у Docker за замовчуванням 5433, усередині мережі — 5432.

```mermaid
flowchart LR
    UI["Існуючий React CinemaCity :5173"] -->|"HTTP/REST :5000"| GW["Ocelot Gateway :5000 · JWT / cache / rate limits / screens"]
    GW -->|"HTTP/REST auth/users :5001"| AUTH["AuthService :5001 · локальна JWT-перевірка"]
    GW -->|"HTTP/REST catalog/admin :5002"| CAT["CatalogService :5002 · локальна JWT-перевірка"]
    GW -->|"HTTP/REST reviews/ratings/profile/home :5003"| MONO["CommunityService :5003 · JWT"]
    AUTH -->|"SQL/PostgreSQL :5432"| ADB[(cinemacity_auth)]
    CAT -->|"SQL/PostgreSQL :5432"| CDB[(cinemacity_catalog)]
    MONO -->|"SQL/PostgreSQL :5432"| MDB[("cinemacity_community · operations + projections")]
    MONO -->|"HTTP/REST user snapshot :5001"| AUTH
    MONO -->|"HTTP/REST catalog snapshot / outbox statistics :5002"| CAT
    CAT -->|"HTTP/REST episode ratings :5003"| MONO
    CAT -->|"HTTPS/REST :443"| TMDB["TMDb · вихідний імпорт CinemaCity"]
```