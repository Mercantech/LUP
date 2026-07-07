# LUP - Lokal Undervisnings Plan

## Astro frontend

Projektet er sat op som en **Astro** statisk side med **theme switch** mellem:

- **Editorial** (Playfair Display + Space Grotesk + Space Mono)
- **Terminal** (dark-only, monospace, scanlines/glow)

### Krav

Du skal have **Node.js LTS** installeret for at kunne køre Astro (derfor virkede `npm` ikke i terminalen før).

### Kom i gang

```bash
npm install
npm run dev
```

Byg statisk output:

```bash
npm run build
```

## Docker + Traefik (Dokploy)

Der er tilføjet `Dockerfile` + `docker-compose.yml` med Traefik labels til domænet **`lup.mercantec.tech`**.

Kør lokalt (kræver Docker):

```bash
docker compose up --build
```

### Traefik forudsætninger

- Compose-filen forventer et eksternt netværk der hedder `traefik`.
- TLS er sat til `websecure` + `letsencrypt` resolveren (tilpas hvis jeres Traefik bruger andre navne).
