# LUP — lup.dk

Lokale undervisningsplaner for MAGS hovedforløb (H1–H6).

| | |
|---|---|
| **Live** | https://lup.dk |
| **Indhold** | H1–H6 LUP’er |

## Deploy

```bash
docker compose up -d --build
```

Lokalt med host-port:

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml up --build
```

Åbn [http://localhost:3080](http://localhost:3080).
