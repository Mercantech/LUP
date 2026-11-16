# Kodearbejder til Hjem.exe

API'et ejer bolig, vurdering, simulationstid, aflæsninger og anvendelse af kommandoer. En separat kodearbejder kompilerer eller indlæser én kildefil per scenario og afvikler den faktiske Python-, C#- eller C++-kode. API'et sender et frosset sensorøjebliksbillede for hver aktivering. Arbejderen returnerer kommandoer, logtekst og ønsket simuleret ventetid. SDK'et er version 1.

## Start på en dedikeret Linux-vært

1. Brug en separat Linux-VM med Docker Engine, cgroup v2 med CPU-, memory-, swap- og proceskvoter, Python 3.11 eller nyere og gVisor registreret som Docker-runtime `runsc`. Hold VM'en adskilt fra LUPs database, de øvrige applikationer og deres credentials. Indstil firewall, så kun LUPs API kan tilgå gatewayen over en TLS-proxy. [gVisor-installation](https://gvisor.dev/docs/user_guide/install/) · [gVisor og Docker](https://gvisor.dev/docs/user_guide/quick_start/docker/)
2. Byg images fra mappen `tools/home-runner`. Byggetrækket henter baseimages og faste SDK-afhængigheder. Elevens egen kompilering og eksekvering kører derefter uden netværk.

   ```sh
   docker build --target python -t lup-home-python:sdk1 .
   docker build --target csharp -t lup-home-csharp:sdk1 .
   docker build --target arduino -t lup-home-arduino:sdk1 .
   ```

3. Installér filerne på worker-VM'en i `/opt/lup-home-runner`, opret servicebrugeren `lup-home-runner`, og installer den medfølgende systemd-service. Giv den dedikerede service adgang til Docker på worker-VM'en. Brug en beskyttet `/etc/lup-home-runner.env` med `HOME_RUNNER_TOKEN` på mindst 32 ASCII-tegn. `HOME_RUNNER_CAPACITY` er som standard 8 aktive sandboxes. Vælg kapaciteten ud fra målt klasselast. Gatewayen lytter som standard på `127.0.0.1:8096`.
4. Kør TLS-proxyen på worker-VM'en og giv LUPs API `HomeGame__RunnerUrl` med HTTPS-adressen til gatewayen samt `HomeGame__RunnerToken` med samme token. Tokenet bliver på serversiden. API'et har ikke adgang til workerens Docker-socket.
5. Kør de deployede smoke tests før elevadgang. Sæt `HOME_RUNNER_URL` til gatewayens adresse og `HOME_RUNNER_TOKEN` til tokenet i den shell, der kører testen.

   ```sh
   python3 verify.py DeployedRunnerTests
   ```

Hvis gatewayen bruger et selvsigneret certifikat, sættes `SSL_CERT_FILE` til certifikatets PEM-fil under testene. API'et får certifikatets SHA-256-pin i `HOME_GAME_RUNNER_CERT_SHA256`. Beregn den med `openssl x509 -in worker-tls.crt -outform DER | sha256sum`, og brug de første 64 hextegn. Certifikatet skal indeholde gatewayens DNS-navn i sit SAN-felt.

På produktionens Docker-vært oprettes den [private bro](../../docs/home-game.md#drift) før VM'en startes. Kør gatewaytestene, og sæt URL, token, pin og `HOME_GAME_ENABLED=true` i Dokploy før Compose-udrulningen.

## Isolation og levetid

- En ny gVisor-sandbox får et read-only image, numerisk bruger uden root, ingen ekstra capabilities, intet container- eller værtsvolumen, ingen netværksplade og kun størrelsesbegrænset midlertidigt filsystem. Compiler og elevproces bruger samme sandbox. Dockers CPU-, memory- og proceskvoter anvendes både ved kompilering og afvikling. [Docker containerindstillinger](https://docs.docker.com/reference/cli/docker/container/run/) · [gVisors isolationsmodel](https://gvisor.dev/docs/architecture_guide/security/)
- Startvinduet er 30 sekunder, aktiveringsvinduet er 3 sekunder, og sandboxen udløber efter 120 sekunder uden aktivering eller 900 sekunder samlet. CPU-kvoten er en halv kerne for Python og én kerne hver for C# og C++. Hukommelsesgrænserne er 128, 768 og 512 MiB, inklusive det midlertidige filsystem.
- Gatewayen afviser forkert run-ID, controller-ID, sekvens, tick og kommandotype. Sandboxens svar bindes til den aktuelle aktivering. Der bruges en uforanderlig aliasoversigt fra det frosne layout. Den næste aktivering følger simulatorens tickregler. API'et validerer selv alle kommandoer, før et batch kan ændre simulationen.
- Gatewayen begrænser kilden til 128 KiB, kommandoer til 256 per aktivering og log til 64 KiB per scenario. Kørsler afvises tydeligt, hvis runtime, image eller kapacitet mangler. Stop fjerner hele containeren. Workerens udløbsjob fjerner forladte sandboxes, og servicegenstart rydder efter tidligere processer på den dedikerede daemon.

## SDK

| Handling | Python | C# | Arduino-C++ |
| --- | --- | --- | --- |
| Import | `import home` | `using HomeSdk;` | `#include <VirtualHome.h>` |
| Læs tal | `home.read("daylight")` | `Home.Read("daylight")` | `home.read("daylight")` |
| Læs boolesk | `home.read("presence")` | `Home.ReadBool("presence")` | `home.readBool("presence")` |
| Lampe | `home.set("desk_lamp", True)` | `Home.Set("desk_lamp", true)` | `home.set("desk_lamp", true)` |
| Vent 200 ms | `home.wait(0.2)` | `Home.Wait(0.2)` | `delay(200)` |
| Tid i ms | `home.now_ms()` | `Home.NowMs()` | `millis()` |
| Log | `home.log(value)` | `Home.Log(value)` | `Serial.println(value)` |

En ventekommando afleverer de samlede kommandoer med det samme. Næste aktivering får et nyt snapshot. Når et Python- eller C#-program afslutter normalt, afleverer det sine sidste kommandoer, og simulationen fortsætter. En fejl i en aktivering kasserer hele dens batch. Logs og compilermeddelelser transporteres som almindelig tekst.

Arduino bruger standard C++17, `setup()` én gang og `loop()` efterfølgende. `home.begin()` og `Serial.begin()` er kompatibilitetsfunktioner uden ekstra opsætning. `millis()` returnerer 32-bit simulerede millisekunder. Brug `uint32_t` til gemte tidspunkter og subtraktion, hvis koden skal fortsætte korrekt gennem wrap. Der er ingen fysiske pins, boardvalg eller installation af eksterne Arduino-biblioteker i SDK 1.

`verify.py` indeholder de samme repræsentative programmer på alle tre sprog. De lokale tests kompilerer og kører kun filens egne kontrolprogrammer. Arduino-testen kræver g++ eller et Visual C++-udviklermiljø og nlohmann JSON. Hvis headeren ligger uden for compilerens standardstier, sæt `LUP_RUNNER_CPP_INCLUDE` til mappen, der indeholder `nlohmann/json.hpp`. Isolations- og startkæden verificeres med de deployede tests på Linux.

`python verify_pilot.py` kobler de tre SDK'er til den autoritative simulator og gennemfører P01–P06 i alle tre sprog. Programmet bruger én gennemgående bolig per sprog, den validerede placering fra hvert byggetrin og de samme scenariedefinitioner som API'et. Det kompilerer kun sine egne faste referenceløsninger. Testen kræver Python, .NET 10 og en C++17-compiler med nlohmann JSON.

## Gatewaykontrakt

Gatewayen bruger JSON over HTTPS med bearer-token. API'et tildeler hvert scenario et nyt sessions-ID under det fælles run-ID. Gatewayen afviser en start, hvis Stop nåede frem først.

| Metode | Adfærd |
| --- | --- |
| `GET /health` | HTTP 200 når gVisor, cgroup-kvoter og alle tre images er klar, ellers 503 |
| `POST /sessions` | `runId`, `sessionId`, `controllerId`, `language`, `source` og frosne `aliases`. Svar `sessionId` eller `error` |
| `GET /sessions/{id}` | HTTP 204 holder en aktiv session i live mellem sjældne aktiveringer. Udløbne eller afsluttede sessioner giver 404 |
| `POST /sessions/{id}/activate` | `runId`, `controllerId`, `sequence`, `tick`, `timeMs` og `values`. Svar med bundne felter, `commands`, `nextDelayMs`, `logs`, `done` og eventuelt `error` |
| `DELETE /sessions/{id}` | Stop og frigiv sandboxen, også hvis sessionen allerede er afsluttet |
