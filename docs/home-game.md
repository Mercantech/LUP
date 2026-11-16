# Hjem.exe

Hjem.exe bruger den samme LUP-session og avatar som profilen. Eleven åbner sit hjem, placerer en lampe og skriver kode til controlleren. Den autoritative simulation måler lys og tilstedeværelse, flytter avataren og vurderer elevens løsning i seks pilotopgaver.

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
| `GET /api/me/home-game` | Hent eller opret hjem, LUP-guld, opgaver, kladder, hold og seneste forsøg. For undervisere åbner serverens rollecheck prøveboligen med alle opgaver og udstyr. |
| `POST /api/me/home-game/build` | Køb og placér, flyt, drej, læg på lager, sælg, omdøb, skift alias eller fortryd én byggehandling. Gentag med samme handlings-ID ved forbindelsesfejl. |
| `PUT /api/me/home-game/program` | Gem en ny kodeversion for et af de tre sprog. Samme tekst genbruger versionen. |
| `POST /api/me/home-game/runs` | Frys revision, kodeversion, scenarier og seed. Et serveroprettet run-ID identificerer forsøget. |
| `GET /api/me/home-game/runs/{id}` | Hent seneste autoritative kørselsstatus og verdenssnapshot efter genforbindelse. |
| `POST /api/me/home-game/runs/{id}/step` | Beregn et afgrænset antal simulationstrin fra en forventet sekvens. En dublet kan hente status uden at beregne samme trin igen. |
| `POST /api/me/home-game/runs/{id}/control` | Pause, fortsæt, stop, vælg afspilningshastighed og ændr input i fri afprøvning. |
| `GET /api/me/home-game/runs/{id}/replay` | Hent layout, scenarier og faktisk accepterede kommandoer fra et afsluttet forsøg. |
| `PUT /api/me/home-game/reflection`, `POST /api/me/home-game/help` | Gem refleksion og send en idempotent hjælpeanmodning til elevens valgte hold. |
| `GET /api/holds/{holdId}/home-game`, `PUT …/settings`, `GET …/students/{studentSub}` | Holdoversigt, åbn opgaver og udstyr, angiv starterkode, og vis elevens kode, rapport, log og refleksion. Sæt `?runId={id}` for at åbne forsøget og kodeversionen fra en tidligere hjælpeanmodning. |

Placering, kode, refleksion og første beståelse deler en revisionskontrol. En ændring kan derfor give HTTP 409, hvis en anden fane har gemt i mellemtiden. Fortrydelse valideres på ny, inklusive den pausede avatars celle. Et forsøg kan ikke optjene belønning, hvis den revision, det blev startet på, er afløst af en ændring.

Kørsel bruger 100 ms simulationstrin. Hvert scenarie får en separat kortlivet worker-session med sin egen identitet. Gatewayen binder svar til både forsøget, controlleren, aktiveringssekvensen og tick. Den afviser store beskeder og kasserer hele aktiveringens batch ved ugyldige kommandoer. En fastfrosset aktiveringsmåling påvirkes først af en kommando i det næste snapshot. Windows-tests kører en kontrolleret HTTP-fixture for gatewayen. Den anvendes kun i tests.

## Guld og prøvebolig

Første lærergodkendelse af en quest giver 20 guld pr. elev i den godkendte aflevering. For gruppeafleveringer følger belønningen de deltagere, der er gemt sammen med afleveringen. Første beståelse af hver opgave i Hjem.exe giver 25 guld. Godkendelser og beståelser bliver registreret per opgave og konto, så gentagelser kun giver én belønning.

Hjem.exe køber med den aktuelle fælles saldo og refunderer den betalte guldpris ved fortrydelse eller salg. Spillet opretter et hjem med gratis startudstyr, når kontoen åbner det første gang.

Undervisere åbner deres prøvebolig via `/teacher/home-game` og **Åbn min prøvebolig**. Alle seks opgaver og alt pilotudstyr er tilgængeligt med gratis køb. En prøvevurdering kører de samme scenarier og viser målinger, log og resultat. Prøvevurderinger giver 0 guld og 0 elevbeståelser. Adgang til elevers arbejde følger holdets eksisterende rettigheder.

## Drift

1. Sæt en separat Linux-vært op som beskrevet i `tools/home-runner/README.md`. Installér den valgte container-runtime og byg de tre SDK-images. Kodeexport, netværk og værtsdata forbliver afgrænset til de dokumenterede runnerprofiler.
2. Konfigurér `HOME_GAME_RUNNER_URL` til workerens HTTPS-adresse og `HOME_GAME_RUNNER_TOKEN` til et tilfældigt secret på mindst 32 tegn. Læg det samme secret i workerens `HOME_RUNNER_TOKEN`. Ved et privat, selvsigneret certifikat skal `HOME_GAME_RUNNER_CERT_SHA256` være SHA-256 for certifikatets DER-fil som 64 hextegn. Certifikatet skal være gyldigt og have URL'ens værtsnavn i sit SAN-felt. En tom pin bruger platformens almindelige certifikattillid. HTTP via loopback er til lokal udvikling med tom pin. Afstem `HOME_GAME_WORKER_SLOTS` med workerens kapacitet. Standard er 8 samtidige kørsler og op til 24 i alt med ventende forsøg.
3. Afprøv status, stop, tidsgrænser og oprydning på workerens faktiske Linux-runtime. Kør `verify.py DeployedRunnerTests` med gatewayens URL og token.
4. Sæt `HOME_GAME_ENABLED=true` i produktionsmiljøet, når workeren er klar. Kør platformens sædvanlige migrations- og deploymentsproces. API'et anvender migrationen ved opstart. Pilotens levende simulation kører i én API-proces, på samme måde som Arena. Efter servergenstart markeres uafsluttede forsøg som udløbne. Layout, kode og afsluttet fremgang er vedvarende.
5. Kør de seks opgaver med rigtige programmer i de tre elevsprog. Godkend en quest på et prøvesæt med en testkonto som elev, køb et møbel i Hjem.exe, og sammenlign den opdaterede saldo med elevens avatarsamling.

Produktionens compose lader Hjem.exe være deaktiveret, indtil `HOME_GAME_ENABLED=true` sættes eksplicit. Hvis den konfigurerede kodearbejder bliver utilgængelig, kan eleven gemme indretning og kode. Kørselsstatus forklarer, at forbindelsen mangler. Der kan ikke tildeles opgavebelønning ved en runnerfejl.

Når workeren kører i en separat VM på produktionens Docker-vært, oprettes det eksterne net med `docker network create --internal --driver bridge lup-home-runner-control` før Compose-udrulningen. Kun API og VM'ens værtscontainer tilsluttes dette net. Værtscontaineren bruger netalias `lup-home-worker`, og API'et bruger `https://lup-home-worker:8443`. Netnavnet kan ændres med `HOME_GAME_CONTROL_NETWORK`. Det eksterne net består, når Compose genopretter API-containeren. Netværket er udeladt af den lokale Compose-variant.

## Gennemfør pilotforløbet

Placér gulvlampen ved skrivebordet. Vælg Python og skriv `home.set("desk_lamp", True)` efter `import home`. C# bruger `Home.Set("desk_lamp", true);` efter `using HomeSdk;`. I Arduino-C++ skal du inkludere `VirtualHome.h`, skrive `home.set("desk_lamp", true);` i `setup()` og beholde en tom `loop()`. Kør i mørke og sammenlign cellens lux med læsemålet under Målinger. Brug Vurdér opgave for at optjene den første belønning.

I P02 kan du flytte bordet til (2, 6) og lampen til (3, 7). Find derefter vægpladser til dagslyssensoren, tilstedeværelsessensoren og kontakten, efterhånden som opgaverne åbner dem. Med bordet på P02-pladsen kan sensoren ved (0, 7) se læsefeltet. P03 bruger kun dagslysmålingen. P04 tilføjer tilstedeværelse. P05 tilføjer kontakten. Afprøv samlet i P06, og gem din egen refleksion.

### Python

```python
import home

while True:
    dark = home.read("daylight") < 300
    present = home.read("presence")
    allowed = home.read("auto_enabled")
    home.set("desk_lamp", dark and present and allowed)
    home.wait(0.2)
```

### C#

```csharp
using HomeSdk;

while (true)
{
    var dark = Home.Read("daylight") < 300;
    var present = Home.ReadBool("presence");
    var allowed = Home.ReadBool("auto_enabled");
    Home.Set("desk_lamp", dark && present && allowed);
    Home.Wait(0.2);
}
```

### Arduino-C++

```cpp
#include <VirtualHome.h>

void setup() {
    home.begin();
}

void loop() {
    bool dark = home.read("daylight") < 300;
    bool present = home.readBool("presence");
    bool allowed = home.readBool("auto_enabled");
    home.set("desk_lamp", dark && present && allowed);
    delay(200);
}
```

## Verifikation og pilotscope

| Område | Status |
| --- | --- |
| INT01–03, DATA01–04, ECON01–02 | API-tests dækker eget subjekt, holdrelationer, historisk kode til hjælp, transaktionskvitteringer, revision, programversion og én belønning. En questgodkendelse kan finansiere et efterfølgende køb i Hjem.exe med samme saldo i avatar-API'et. |
| GRID01–06, GEO01–04, LIGHT01–04, NAV01–02 | Domænet har en deterministisk model og særskilte regel- og scenariefixtures. Overfladeenheder og det høje skab bruges som tekniske fixtures i piloten. Butikken tilbyder gulv-, dekorations- og vægenheder. |
| SDK01–05, TIME01–02, SEC01–02, REPLAY01 | Sprog-SDK, gatewaykontrakt og simulator er afprøvet særskilt. |
| UI01–02 | `tests/home-game-browser.cjs` afprøver tastatur, placering, køb med guld, refundering, kodegemning, pause, genafspilning, progression og mobilnavigation med fiktive HTTP-svar. Den afprøver også alle seks oplåste opgaver og gratis prøveudstyr på en underviserkonto med 0 guld. |
| EDU01–02 | `tools/home-runner/verify_pilot.py` gennemførte alle seks opgaver i hvert sprog. Testen kørte 54 scenarier med de lokale Python-, C#- og C++-runtimes og den autoritative simulator. |
| RUN01–04 | Seks driftstests af den separate Linux-runner afventer en dedikeret gVisor-vært. Klassebelastning og hele kæden gennem workerens HTTPS-gateway verificeres på den vært. |
| Senere indhold | Flere rum, temperatur, støj, renlighed, dæmpning i spillets katalog, loftsudstyr, fysisk Arduino og flere controllere følger efter lyspiloten. |

En ny pilotopgave tilføjes i `HomeCatalog.Tasks`. Beskriv de nødvendige enheder og vælg faste scenarier med vurderingsvinduer. Tilføj deterministiske referencefixtures og en stabil opgaveidentitet før udgivelse. Den eksisterende første-belønning følger opgaveidentiteten på tværs af sprog og indholdsversioner.
