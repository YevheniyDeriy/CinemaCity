# Послідовність ЛР1: той самий сценарій після виділення

Після входу Auth видає JWT. Оцінка залишається у Community, метадані епізоду належать Catalog. Нові мережеві виклики видимі явно; бази не поєднуються JOIN. Outbox дає повторну доставку, а не спільну транзакцію трьох БД.

```mermaid
sequenceDiagram
    title ЛР1 — та сама оцінка епізоду з виділеними власниками даних
    participant UI as React :5173
    participant GW as Ocelot :5000
    participant A as Auth :5001
    participant AD as cinemacity_auth :5432
    participant M as Community/моноліт :5100
    participant MD as cinemacity :5432
    participant C as Catalog :5002
    participant CD as cinemacity_catalog :5432
    UI->>GW: HTTP/REST POST /api/auth/login
    GW->>A: HTTP/REST POST /api/v1/auth/login
    A->>AD: SQL account + roles, hash verification
    AD-->>A: SQL result
    A-->>GW: HTTP 200 access + refresh
    GW-->>UI: HTTP 200 + HttpOnly refresh cookie
    UI->>GW: HTTP/REST POST /api/ratings + JWT
    GW->>M: HTTP/REST POST /api/ratings
    Note over M: Локальна перевірка JWT, без звернення до Auth за перевіркою підпису
    M->>A: HTTP/REST internal user snapshot за ID, service key
    A->>AD: SQL SELECT public identity
    AD-->>A: SQL result
    A-->>M: HTTP 200 identity DTO
    M->>C: HTTP/REST internal catalog snapshot за EpisodeId
    C->>CD: SQL SELECT episode/season/movie
    CD-->>C: SQL result
    C-->>M: HTTP 200 snapshot DTO
    M->>MD: SQL upsert локальних reference-проєкцій
    M->>MD: SQL rating + watchlist + aggregate + outbox, COMMIT
    MD-->>M: SQL COMMIT result
    M-->>GW: HTTP 201 RatingResponse
    GW-->>UI: HTTP 201
    loop Background outbox до успішної доставки
        M->>C: HTTP/REST PUT internal statistics + monotonic version
        C->>CD: SQL update aggregate/version у локальній транзакції
        CD-->>C: SQL COMMIT
        C-->>M: HTTP 204, повтор/стара версія безпечні
        M->>MD: SQL mark outbox delivered
    end
```
