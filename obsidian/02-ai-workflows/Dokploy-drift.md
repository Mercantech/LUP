---
type: workflow
status: active
tags:
  - dokploy
  - hosting
  - ai
---

# Dokploy-drift

Brug [Deployment i LUP](https://lup.dk/notion/cae2b3bf-5d25-458e-b567-073cd5074c4a) til den fælles teori. Kapitlet [Fra Git til drift med Dokploy](https://lup.dk/notion/f357faac-317b-4c53-a297-dcb65267ad9c) viser vejen fra Git-revision til en verificeret release. Herunder står LUP-projektets konkrete driftsindstillinger.

## Domæne

| Miljø | Host |
|-------|------|
| **Prod (primær)** | **`mercantec.tech`** (apex — **intet** subdomæne) |
| **Prod (alias)** | **`lup.dk`** (+ `www.lup.dk`) — samme `web`:3000 |
| Dev (senere) | aftales separat (branch + env) — ikke hardcod i kode |

Dokploy: domain på service **`web`**, port **3000**. For mercantec.tech / lup.dk bag Cloudflare: følg org-praksis (`certificateType` / https ift. tunnel).

`lup.dk` er Traefik-labels på `web` (Host `lup.dk` eller `www.lup.dk`). Alternativt Dokploy MCP `domain-create` for begge hosts. DNS/Cloudflare skal pege apex + www til **samme origin/tunnel** som `mercantec.tech`.

## Udgivelsesgren

Vælg GitHubs `main` som produktionsgren i Dokploy. Udgivelsesobservatøren slår den kørende revision op i historikken for `main`. Efter en udgivelse sammenlignes revisionen i `/release.json` med `/api/platform-updates/deployment`.

## Netværk (påkrævet)

Produktionsstakken **Mercantec.tech → Production** genbruger sin eksisterende private Docker-bridge. Tilføj `-f ./docker-compose.production.yml` efter `-f ./docker-compose.yml` i Dokploys Compose-kommando. Behold projektets navn, som bestemmer navnene på bridgen og datavolumenerne. Hvis bridgen har et særskilt navn, sæt `LUP_PRIVATE_NETWORK_NAME` til navnet på den bridge, produktionsdatabasen allerede bruger. Brug **Deploy** til at starte produktion, så Dokploy bruger kommandoen med begge Compose-filer.

1. `default` er det private net for databasen, API'et, collaboration og object storage. Web bruger også `dokploy-network` til Traefik.
2. Aliaser på det private net er **`mercantec-tech-api`**, **`mercantec-tech-db`** og **`mercantec-tech-minio`**.
3. Nginx bruger `resolver 127.0.0.11` og `set $upstream_api http://lup-api-internal:8080`.
4. Connection string: `Host=mercantec-tech-db`
5. Object storage går fra API'et til `http://mercantec-tech-minio:9000` med S3 path-style. `MINIO_ROOT_USER` og `MINIO_ROOT_PASSWORD` skal være udfyldt i Dokploy. Produktionsoverlayet afviser tomme værdier før opstart og bruger de samme credentials i MinIO, initialisering, API og previewworker.
6. Image: **`pgsty/silo`** (pinned tag i compose). Brug **ikke** `quay.io/minio/minio` / `quay.io/minio/mc` — Quay returnerer **401** (community-images fjernet).
7. Produktionsoverlayet tilslutter kun MinIO til det private net, sætter `traefik.enable=false` og fjerner arvede routerlabels og publicerede porte. Browseren henter filer gennem LUPs API med de eksisterende adgangskontroller og mediebilletter. Offentlige karakteroriginaler bruger den eksisterende katalogkontrollerede nginx/API-rute. MinIO Console kan tilgås lokalt på `http://localhost:9001` med `docker-compose.local.yml`.

Behold produktionsprojektets navn og eksisterende datavolumener. PostgreSQL bruger `mercantec_tech_pg`, og MinIO bruger `mercantec_tech_minio` under samme Compose-projekt. Produktionsoverlayet ændrer kun adgangen til object storage.

Proxyér **aldrig** til generisk `http://api:...` på det delte net.

## .NET API

- `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false`
- `ASPNETCORE_URLS=http://+:8080`
- Connection string fra **env** (`ConnectionStrings__Default` / `CONNECTION_STRING`)

## MCP-flow

Brug **Dokploy MCP** (ikke ad hoc curl):

1. Compose peger på GitHub repo/branch + `composePath`
2. `compose-saveEnvironment` (`createEnvFile: true`) — `CORS_SPA_ORIGINS` skal inkludere `https://mercantec.tech;https://lup.dk;https://www.lup.dk` (+ lokale origins efter behov)
3. `domain-create` → host **`mercantec.tech`**, service `web`, port 3000
4. `domain-create` → host **`lup.dk`**, service `web`, port 3000 (og evt. `www.lup.dk`) — eller stol på Traefik-labels i compose
5. Kontroller, at Compose-kommandoen inkluderer `docker-compose.production.yml`, og at MinIO kun har det private net og `traefik.enable=false` i den effektive konfiguration.
6. `compose-deploy`
7. Fejl: `deployment-readLogs` / `compose-readLogs`

Typiske fejl: subnet-pool, inotify, localhost-DB, nginx upstream DNS.

## Relateret

- [[Stack-og-hosting]]
- [[Deploy-Dokploy]] (prompt)
- [[Husregler-fra-Cursor]]
- Issue #5 (Dokploy setup)
