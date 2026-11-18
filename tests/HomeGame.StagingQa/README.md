# Hjem.exe i isoleret staging

Denne test bruger de samme API- og webimages som kandidaten til udgivelse. API'ets almindelige startup kører EF-migreringer på en frisk PostgreSQL-database. Test-issuerens RS256-token valideres af API'ets almindelige HTTPS OIDC/JWKS-konfiguration, og alle spil-, quest- og rettighedskontroller kører gennem rigtige HTTP-kald. Browseren får hverken mockede API-svar eller adgang til produktionsdata.

Compose-projektet hedder `stage-hjem-20261118`. Database, web, browser og testissuer bruger ét internt netværk med egne volumes. Ingen service publicerer en hostport. Kun API'et tilsluttes desuden `lup-home-runner-control` og får read-only adgang til den særskilte worker-konfiguration `lup-hjem-stage-connection-20261118`.

## Forudsætninger

- Docker Engine på den godkendte testvært med plads til det separate netværk `10.42.94.48/28`. `LUP_HOME_QA_SUBNET` kan vælge et andet kontrolleret frit subnet.
- Kandidatens API-image bygget med `api/Dockerfile --target runtime` og webimage med `web/Dockerfile`.
- Den private worker er klar med alle tre runtimes. Den særskilte staging-konfiguration har HTTPS-URL, token og certifikatpin.
- En frisk stagingdatabase i PostgreSQL 16, samme major som produktionen. Et eksisterende `stage-hjem-20261118_qa-db`-volume får launcheren til at stoppe. Launcheren kontrollerer også, at databasen er tom, før API'et bliver startet.

Den offline kopi af eksisterende data indgår aldrig i Compose-filen. Testen opretter sit eget forløb, testhold og sine egne fiktive deltagere gennem de normale API'er.

## Kør efter staging-gate

Fra kandidatens rod på Linux-testværten sættes de to konkrete image-tags. Gateværdien registrerer, at denne separate syntetiske test må starte.

```sh
export LUP_HOME_QA_API_IMAGE=lup-home-candidate-api:VERSION
export LUP_HOME_QA_WEB_IMAGE=lup-home-candidate-web:VERSION
export LUP_HOME_QA_APPROVED=synthetic-only-20261118
sh tests/HomeGame.StagingQa/run.sh
```

Det forventede fælles releasefingerprint kan sættes med `HOME_QA_EXPECTED_REVISION`. Browseren kontrollerer altid, at API- og webimage rapporterer samme fingerprint.

## Dækning

Testen gennemfører elevens aflevering og underviserens godkendelse i den rigtige Blazor-klient. Først bruger en ny fiktiv elev sin normale startsaldo på 320 guld til ti tæpper. Et ekstra køb til 30 guld afvises ved saldo 20. En godkendt quest giver derefter 20 guld. Gentaget godkendelse giver samlet én belønning. Elevens køb af det ellevte tæppe gennem Hjem.exe efterlader 10 guld, som både bolig og avatarsamling skal vise efter genindlæsning.

Underviserens prøvebolig gennemfører P01–P06 med rigtig Python, C# og Arduino-C++ gennem den private isolerede worker. De 18 kombinationer bruger 54 scenarier. Underviserens saldo forbliver 320 guld, og elevprogression er tom. Browseren viser det sidste faktiske resultat og alle seks åbne opgaver. Testen kontrollerer også, at en anden underviser savner holdadgang, og at en anden elev savner adgang til forsøget.

Rapport og billeder ligger i det separate volume `stage-hjem-20261118_qa-artifacts`. Rapporten indeholder fiktive ID'er, anmodningsstier, statuskoder og releasefingerprint. Tokens, privatnøgler og workerens forbindelse skrives ikke til rapporten.

## Stop efter aflæsning

`docker compose --env-file /dev/null -f tests/HomeGame.StagingQa/compose.yml down` stopper kun stagingprojektet. Den eksterne workerforbindelse ejes af den separate workeropsætning.

En ny acceptkørsel kræver, at det tidligere projekts syntetiske database er fjernet. Bevar eller kopiér rapporten før testvolumes ryddes. Produktionsvolumes og offline data skal fortsat ligge uden for denne test.
