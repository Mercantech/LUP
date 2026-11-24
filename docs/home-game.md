# Hjem.exe

Hjem.exe bruger den samme LUP-session og avatar som profilen. I 2D-rummet kan eleven indrette, programmere og følge lyset ved skrivebordet. Lyspanelet og målepunktet på kortet viser lux fra samme simuleringstrin. Kom i gang-guiden indeholder kode til lampen og sensorerne.

## Integration

| Område | Integration i LUP |
| --- | --- |
| Applikation | `web/Mercantec.Tech.Web.csproj` bygger Blazor WebAssembly. `api/Mercantec.Tech.Api.csproj` bygger ASP.NET Core. Begge bruger .NET 10 og kontrakter fra `shared/`. |
| Indgang | `web/Pages/HomeGame.razor` samler spillet på `/spil/hjem` og håndterer de tidligere lejlighedsindgange. |
| Identitet | `api/Auth/UserClaims.cs` læser brugerens Auth-subjekt fra det validerede token. Klienten sender også sit forventede subjekt, så en kontoskiftende browser får en konflikt frem for at skrive på den forkerte konto. |
| Avatar | Spillet viser den eksisterende avatar fra platformens avatarprofil og bruger en enklere visning, når profilen kun har et portræt. |
| Hold | `api/Learning/CourseAccess.cs` afgør, hvilke hold en underviser må administrere. Lærer-API'et kræver et aktivt medlemskab for den konkrete elev. Elever læser kun instrukser fra hold, de er medlem af. |
| Data | `api/Data/AppDbContext.HomeGame.cs` mapper spillets data. PostgreSQL-migrationen `20261116114103_HomeGameAndQuestGold` tilføjer de seks Hjem.exe-tabeller og kvitteringerne for questguld. |
| Økonomi | Boligbutikken, quests og avatarsamlingen bruger det samme guld i `AvatarProfile.Gold`. `AvatarWallet` bogfører køb, refunderinger og belønninger i den relevante databasetransaktion. Startudstyr i hjemmet er gratis. |
| Drift | `api/HomeGame/HomeRunCoordinator.cs` koordinerer det autoritative forsøg. `tools/home-runner/` bygger og kører elevens kode på en separat Linux-vært. LUPs API har ingen Docker-socket eller direkte adgang til elevprocesser. |

## API og datakontrakter

Spillets fælles typer ligger i `shared/HomeGameApi.cs`. Grid, katalog og simulation ligger i `shared/HomeGame/`. Builderens forhåndsvisning og serverens kontrol bruger de samme placeringsregler.

| Metode og sti | Funktion |
| --- | --- |
| `GET /api/me/home-game` | Hent eller opret hjem, valgt avatar, LUP-guld, kladder, hold og seneste forsøg. For undervisere åbner serverens rollecheck prøveboligen. |
| `POST /api/me/home-game/build` | Køb og placér, flyt, drej, læg på lager, sælg, omdøb, skift alias eller fortryd én byggehandling. Gentag med samme handlings-ID ved forbindelsesfejl. |
| `PUT /api/me/home-game/program` | Gem en ny kodeversion for et af de tre sprog. Samme tekst genbruger versionen. |
| `POST /api/me/home-game/runs` | Start en fri kørsel på den aktuelle revision og kodeversion. Et serveroprettet run-ID identificerer forsøget. |
| `GET /api/me/home-game/runs/{id}` | Hent seneste autoritative kørselsstatus og verdenssnapshot efter genforbindelse. |
| `POST /api/me/home-game/runs/{id}/step` | Beregn et afgrænset antal simulationstrin fra en forventet sekvens. En dublet kan hente status uden at beregne samme trin igen. |
| `POST /api/me/home-game/runs/{id}/control` | Pause, fortsæt, stop, vælg afspilningshastighed og ændr input i fri afprøvning. |
| `GET /api/me/home-game/runs/{id}/replay` | Hent layout, scenarier og faktisk accepterede kommandoer fra et afsluttet forsøg. |
| `PUT /api/me/home-game/reflection`, `POST /api/me/home-game/help` | Gem refleksion og send en idempotent hjælpeanmodning til elevens valgte hold. |
| `GET /api/holds/{holdId}/home-game`, `PUT …/settings`, `GET …/students/{studentSub}` | Se holdets hjælpeanmodninger, angiv starterkode, og vis elevens kode, log og refleksion. Sæt `?runId={id}` for at åbne forsøget og kodeversionen fra en tidligere hjælpeanmodning. |

Placering, kode og refleksion deler en revisionskontrol. En ændring kan derfor give HTTP 409, hvis en anden fane har gemt i mellemtiden. Fortrydelse valideres på ny, inklusive den pausede avatars celle.

Kørsel bruger 100 ms simulationstrin. Hvert scenarie får en separat kortlivet worker-session med sin egen identitet. Gatewayen binder svar til både forsøget, controlleren, aktiveringssekvensen og tick. Den afviser store beskeder og kasserer hele aktiveringens batch ved ugyldige kommandoer. En fastfrosset aktiveringsmåling påvirkes først af en kommando i det næste snapshot. Windows-tests kører en kontrolleret HTTP-fixture for gatewayen. Den anvendes kun i tests.

## Guld og prøvebolig

Første lærergodkendelse af en quest giver 20 guld pr. elev i den godkendte aflevering. For gruppeafleveringer følger belønningen de deltagere, der er gemt sammen med afleveringen. Questgodkendelsen registreres per quest og konto, så en gentaget godkendelse giver samme saldo. Kodekørsel i værkstedet er fri og koster 0 guld.

Hjem.exe køber med den aktuelle fælles saldo og refunderer den betalte guldpris ved fortrydelse eller salg. Spillet opretter et hjem med gratis startudstyr, når kontoen åbner det første gang.

Undervisere åbner deres prøvebolig via `/teacher/home-game` og **Åbn min prøvebolig**. De kan afprøve udstyr gratis, køre deres kode og se målinger og log. Adgang til elevers arbejde følger holdets eksisterende rettigheder.

## Drift

1. Sæt en separat Linux-vært op som beskrevet i `tools/home-runner/README.md`. Installér den valgte container-runtime og byg de tre SDK-images. Kodeexport, netværk og værtsdata forbliver afgrænset til de dokumenterede runnerprofiler.
2. Konfigurér `HOME_GAME_RUNNER_URL` til workerens HTTPS-adresse og `HOME_GAME_RUNNER_TOKEN` til et tilfældigt secret på mindst 32 tegn. Læg det samme secret i workerens `HOME_RUNNER_TOKEN`. Ved et privat, selvsigneret certifikat skal `HOME_GAME_RUNNER_CERT_SHA256` være SHA-256 for certifikatets DER-fil som 64 hextegn. Certifikatet skal være gyldigt og have URL'ens værtsnavn i sit SAN-felt. En tom pin bruger platformens almindelige certifikattillid. HTTP via loopback er til lokal udvikling med tom pin. Afstem `HOME_GAME_WORKER_SLOTS` med workerens kapacitet. Standard er 8 samtidige kørsler og op til 24 i alt med ventende forsøg.
3. Afprøv status, stop, tidsgrænser og oprydning på workerens faktiske Linux-runtime. Kør `verify.py DeployedRunnerTests` med gatewayens URL og token.
4. Sæt `HOME_GAME_ENABLED=true` i produktionsmiljøet, når workeren er klar. Kør platformens sædvanlige migrations- og deploymentsproces. API'et anvender migrationen ved opstart. Pilotens levende simulation kører i én API-proces, på samme måde som Arena. Efter servergenstart markeres uafsluttede forsøg som udløbne. Layout, kode og afsluttet fremgang er vedvarende.
5. Afprøv lampen med rigtige programmer i de tre elevsprog. Godkend en quest på et prøvesæt med en testkonto som elev, køb et møbel i Hjem.exe, og sammenlign den opdaterede saldo med elevens avatarsamling.

Produktionens compose lader Hjem.exe være deaktiveret, indtil `HOME_GAME_ENABLED=true` sættes eksplicit. Hvis den konfigurerede kodearbejder bliver utilgængelig, kan eleven gemme indretning og kode. Kørselsstatus forklarer, at forbindelsen mangler.

Når workeren kører i en separat VM på produktionens Docker-vært, oprettes det eksterne net med `docker network create --internal --driver bridge lup-home-runner-control` før Compose-udrulningen. Kun API og VM'ens værtscontainer tilsluttes dette net. Værtscontaineren bruger netalias `lup-home-worker`, og API'et bruger `https://lup-home-worker:8443`. Netnavnet kan ændres med `HOME_GAME_CONTROL_NETWORK`. Det eksterne net består, når Compose genopretter API-containeren. Netværket er udeladt af den lokale Compose-variant.

## Prøv lyset

Åbn dit hjem på `/spil/hjem`. I en ny bolig står lampen ved skrivebordet. Hvis du har lagt den på lager, kan du placere den fra **Indret**. Åbn **Programmér** og **Kom i gang** og brug lampeeksemplet til dit valgte sprog. Når koden kører, kan du se lysstyrken på læsepladsen under **Afprøv**. Flyt lampen eller prøv et andet input, og sammenlign målingerne.

## Verifikation

Browserkontrollen i `tests/home-game-browser.cjs` bruger fiktive svar til at kontrollere indretning, refusion, kodegemning, tilgængelighed og mobilvisning. Den separate test i `tests/HomeGame.StagingQa` bruger rigtige API- og webimages, en privat database, syntetiske konti og den isolerede kodeworker. Den gennemfører en questgodkendelse, et køb med den fælles guldsaldo, karakterskift og kodekørsel i Python, C# og Arduino-C++.
