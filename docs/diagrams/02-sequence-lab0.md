# Послідовність ЛР0: оцінити епізод у вихідному моноліті

Режим `Gateway:Mode=Monolith`, `Services:Enabled=false`. JWT перевіряється монолітом. Усі внутрішні виклики модулів та EF Core залишаються локальними. База — вихідний CinemaCity; в оновленій перехідній схемі посилання збережені у власних projections.

```mermaid
sequenceDiagram
    title ЛР0 — існуюча оцінка епізоду через gateway
    participant UI as React CinemaCity :5173
    participant GW as Ocelot :5000
    participant M as Моноліт :5100
    participant DB as PostgreSQL/cinemacity :5432
    UI->>GW: HTTP/REST POST /api/ratings + JWT
    GW->>M: HTTP/REST POST /api/ratings + X-Correlation-Id
    Note over M: Локальна перевірка JWT і прав
    M->>DB: SQL SELECT episode та перевірка дубля
    DB-->>M: SQL result
    M->>DB: SQL INSERT rating, UPDATE average, watchlist
    DB-->>M: SQL COMMIT
    M-->>GW: HTTP 201 + RatingResponse
    GW-->>UI: HTTP 201 з тим самим контрактом
```
