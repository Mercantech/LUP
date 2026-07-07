export type Forlob = {
  slug: string;
  short: string;
  titel: string;
  kort: string;
  markdown: string;
};

export const forlob: Forlob[] = [
  {
    slug: 'hovedforlob-1',
    short: 'H1',
    titel: 'Hovedforløb 1',
    kort: 'Fundament i C#, OOP, SQL, HTTP, Blazor og projektarbejde (SCRUM/GitHub).',
    markdown: `
## Mål for undervisningen
Formålet med H1 er at give eleverne et solidt fundament i grundlæggende programmering og relaterede teknologier, som de kan anvende i et samlet, helhedsorienteret projekt. Efter forløbet skal eleven kunne:

- Udvikle grundlæggende applikationer i C# ved brug af objektorienterede principper.
- Designe og anvende relationelle databaser med SQL.
- Forstå netværkskommunikation og HTTP-protokoller.
- Implementere brugervenlige brugergrænseflader med Blazor.
- Arbejde i teams ved brug af SCRUM-metodologi og GitHub til versionsstyring.
- Dokumentere og reflektere over arbejdsprocessen og det færdige produkt.

## Indhold i undervisningen
Undervisningen kombinerer teori og praksis gennem følgende hovedtemaer:

- Grundlæggende programmering – variabler, datatyper, kontrolstrukturer, metoder, debugging og grundlæggende OOP i C#.
- Netværk og kommunikation – klient/server-arkitektur, OSI-modellen og HTTP-protokoller.
- Objektorienteret programmering (OOP) – klasser, arv, polymorfi, interfaces, indkapsling og UML.
- Databasedesign og integration – relationelle databaser, SQL, CRUD-operationer og integration med applikationer (ADO.NET).
- Frontend-udvikling med Blazor – databinding, komponenter, routing, HTML, CSS, Bootstrap og JavaScript-integration.
- Projektstyring og samarbejde – introduktion til SCRUM, sprint-planlægning, retrospektiver og versionsstyring med GitHub.

Projektet **Niche Markedsplads** danner rammen om forløbet.

## Helhedsorientering
Alle fagspecifikke H1-fag integreres i projektarbejdet, så eleverne oplever en sammenhæng mellem teori og praksis. Projektet struktureres som en erhvervsnær opgave, hvor udviklingsprocessen spejler branchens arbejdsgange fra idé til færdigt produkt.

## Praksisrelation
Forløbet er tilrettelagt med fokus på at spejle professionelle udviklingsprocesser. Eleverne arbejder med værktøjer som Visual Studio, GitHub, PostgreSQL og Blazor i et setup, der minder om virkelige udviklingsprojekter. Projektarbejdet følger SCRUM-principper med sprintplanlægning, standups og løbende feedback, så eleverne opnår erfaring med moderne samarbejdsformer i softwareudvikling.

## Tværfaglighed
Forløbet kombinerer fagområderne programmering, databasedesign, netværk, frontend-udvikling og projektstyring. Eleverne lærer at integrere teknologier og metoder på tværs af fagene for at skabe en samlet løsning, der fungerer både teknisk og brugermæssigt.

## Differentiering
Eleverne arbejder i selvvalgte grupper og kan tilpasse projektets kompleksitet og retning i dialog med underviseren. Der tilbydes supplerende opgaver og udfordringer for elever, der ønsker at fordybe sig yderligere, fx spilprojekter (Minestryger, Find the Cat), ekstra OOP-øvelser, eller automatiseringsopgaver med Selenium.

## Evaluering og bedømmelse

## Læringsaktivitet Grundlæggende programmering – 72 lektioner
Du kan/har:

- Designe og udvikle konsolprogrammer ud fra case-beskrivelser
- Anvende et C# til at oprette, fejlfinde og teste mindre konsolapplikationer
- Forstå datatyper
- Anvende kontrolstrukturer, metoder, variabler og konstanter i deres programmer
- Dokumentere kode med kommentarer og versionsstyring

Teoretisk del (eksempler):
- Informations- og dokumentations-søgning
- Kravspecifikationer
- Grundlæggende fejlfinding og debugging
- Testmetoder og værktøjer
- Metoder, arrays & kontrol-strukturer

Praktisk del:
- Udvikling af varierede og enkle konsolbaserede programmer i C#
- Dagsbaserede opgaver fra Microsoft Learn
- Grundlæggende GitHub

Feedbackfokus:
- Løbende mundtlig feedback på lærlingens evne til at planlægge og designe konsolprogrammer
- Evne til at identificere og rette fejl i konsolapplikationer

Bedømmelse (særligt vægt på):
- Evne til at gennemføre hele softwareudviklingscyklussen fra design til test
- Evne til at begrunde valgte løsninger og beskrive udviklingsprocessen
- Implementering af versionsstyring og fejlfindingsteknikker

## Læringsaktivitet Netværk I – 72 lektioner
Du kan/har:

- Designe og implementere effektive netværksløsninger, der opfylder specifikke krav
- Administrere og konfigurere VLAN'er og routing i komplekse netværksmiljøer
- Sikre netværk, redundans og pålidelighed ved hjælp af forskellige protokoller og metoder
- Udføre fejlfinding og diagnosticering af netværks-problemer og implementere sikkerhedstiltag
- Grundlæggende forståelse af layer 2 og layer 3

Teoretisk del:
- OSI-modellen
- Grundlæggende principper for netværksdesign (arkitektur, protokoller og komponenter)
- Routing og switching (VLAN, IP-routing og layer 2-switching)

Praktisk del:
- Hands-on øvelser med VLAN'er, routing og Layer 3-switche
- Arbejde med at lave egen webserver i C#

Feedbackfokus:
- Elevens engagement
- Elevens nysgerrighed
- Elevens arbejde med CCNA læringsmateriale

Bedømmelse (særligt vægt på):
- Elevernes evne til at forstå pensum fra Cisco og relatere det til softwareudvikling

## Læringsaktivitet Clientsideprogrammering – 36 lektioner
Du har/kan:

- Oprette websider ved hjælp af HTML, CSS og Bootstrap-frameworket til at skabe ensartede og responsive brugergrænseflader
- Forklare begreberne i Client-Server-modellen og have forståelse af HTTP-protokollen
- Oprette HTML-dokumenter med korrekt struktur og semantik

Teoretisk del:
- Client-Server-modellen og HTTP-protokollen
- Semantisk HTML og Bootstrap
- UX og HCI introduktion

Praktisk del:
- Agilt projektarbejde på en webfrontend; fra simpelt til mere kompliceret design
- Daglige opgaver, fremlæggelser og feedback loops i grupper såvel plenum
- Fælles udgangspunkt: C#, Blazor med Bootstrap og MSSQL til projektarbejde

Feedbackfokus:
- Samarbejde i gruppen
- Engagement
- Nysgerrighed

Bedømmelse (særligt vægt på):
- Elevens evne til at gå fra simpel statisk enkelt-side til mere avanceret/dynamisk implementering
- Kvalificering af egne valg

## Læringsaktivitet Databaseprogrammering / Serverteknologi / Databaseserver & Computerteknologi – 108 lektioner
Du har/kan:

- Kendskab til serveropsætning og drift, særligt ift web og db server
- Bruge SQL til at oprette, ændre og aflæse data i en lokal database på din computer
- Redegøre for forskellige relationstyper mellem database-tabeller og forstå deres betydning i design af effektive databaser
- Installere og konfigurere Visual Studio så man kan afvikle og udvikle på webserver, database og applikationsniveau
- Route til forskellige websider baseret på domænenavne

Teoretisk del:
- Grundlæggende databaseadministration og sikkerhed, serveropsætning & drift
- Introduktion til relationelle database og alternativer

Praktisk del:
- SQL-øvelser: SELECT, INSERT, UPDATE, DELETE, WHERE, JOIN
- Skrive scripts til at automatisere gentagne databaseopgaver og samle dit arbejde i scripts, som kan genanvendes
- Installere og konfigurere Internet Information Services og en MSSQL database (lokalt eller via Azure el.lign.)

Feedbackfokus:
- Forståelse af grundlæggende databaseadministrationsprincipper og webserver-funktionalitet
- Evne til at forklare konceptet bag relationelle databaser
- Evne til at lave/forklare/arbejde med simple CRUD operationer

Bedømmelse (særligt vægt på):
- Evne til at oprette, ændre og aflæse data samt udføre komplekse forespørgsler
- Evne til at anvende SQL korrekt og effektivt
- Evne til at illustrere og forklare database-relationstyper med eksempler i en frontend

## Læringsaktivitet Objektorienteret programmering – 72 lektioner
Du har/kan:

- Forstå og anvende objektorienteret programmering (OOP) koncepter til at strukturere softwareprojekter
- Arbejde med klasser til at oprette og bruge objekter
- Definere og designe dine egne klasser
- Udarbejde en applikation, der benytter OOP-konceptet effektivt

Teoretisk del:
- Grundlæggende OOAD og OOP koncepter
- Fordele ved anvendelse af OOP i softwareudvikling

Praktisk del:
- Oprettelse af klasser og objekter i C#
- Implementering af grundlæggende OOP-koncepter i en praktisk applikation
- Anvendelse af lists, arrays mv til at organisere data
- Implementering af abstrakte klasser og metoder

Senest opdateret 5/12/23 af Peter Jones
`.trim()
  },
  {
    slug: 'hovedforlob-2',
    short: 'H2',
    titel: 'Hovedforløb 2',
    kort: 'Komplekse systemer med Entity Framework, avancerede relationer, Blazor, API-integration og SCRUM.',
    markdown: `
## Mål for undervisningen
Formålet med H2 er at bygge videre på elevernes grundlæggende færdigheder fra H1 og give dem kompetencer til at udvikle komplekse, skalerbare systemer med integration af backend, database og frontend. Efter forløbet skal eleven kunne:

- Udvikle skalerbare backend-systemer ved hjælp af ORM (Entity Framework).
- Designe og implementere databaser med avancerede relationer.
- Udvikle responsive og brugervenlige frontends i Blazor.
- Arbejde med API-design og integration til dataudveksling.
- Anvende SCRUM som metode til planlægning, udvikling og evaluering.
- Dokumentere og præsentere arbejdsproces og produkt, både teknisk og forretningsmæssigt.

## Indhold i undervisningen
- Backend-programmering: API-design, forretningslogik, validering, async og sikkerhed.
- Databaseudvikling: avancerede SQL-operationer, relationer, constraints, triggers, stored procedures og ORM via Entity Framework.
- Frontend-udvikling: dynamiske grænseflader, navigation, datavisualisering og inputvalidering i Blazor.
- Projektstyring: SCRUM, sprintplanlægning, backlog-håndtering og retrospektiver.
- Test og kvalitetssikring: automatiserede tests, fejlhåndtering og optimering af UX.

Forløbet kulminerer i **Hotelbookingsystemet**.
`.trim()
  },
  {
    slug: 'hovedforlob-3',
    short: 'H3',
    titel: 'Hovedforløb 3',
    kort: 'IoT-løsninger med Arduino, .NET API, databaseintegration, dashboards og sikkerhed.',
    markdown: `
## Mål for undervisningen
H3 har til formål at give eleverne kompetencer til at designe og implementere komplette IoT-løsninger, hvor hardware, backend og frontend integreres i én samlet løsning. Efter forløbet skal eleven kunne:

- Integrere IoT-enheder med backend og database.
- Designe og udvikle API’er til realtidsdataudveksling.
- Udvikle dashboards med visualisering og kontrol af IoT-enheder.
- Arbejde med sikkerhed, dataintegritet og skalerbarhed i IoT-systemer.
- Dokumentere og præsentere en helhedsorienteret løsning, både teknisk og funktionelt.

## Indhold i undervisningen
- IoT og hardwareprogrammering: Arduino MKR IoT kit, sensorer/aktuatorer, C++.
- Backend og API-udvikling: REST API i C#/.NET, CRUD, HTTPS og Entity Framework.
- Databaseudvikling: relationelle databaser, avanceret SQL, ORM-integration.
- Frontend og dashboard: realtidsvisualisering af sensordata og styring.
- Sikkerhed og performance: kryptering, beskyttelse mod angreb, skalerbarhed.
- Projektstyring: SCRUM med sprints, standups, sprint reviews og retrospektiver.

Forløbet kulminerer i **Interaktiv IoT Platform**.
`.trim()
  },
  {
    slug: 'hovedforlob-4',
    short: 'H4',
    titel: 'Hovedforløb 4',
    kort: 'Flutter mobile apps med API-integration, offline lagring, test/debugging og SCRUM-projekt.',
    markdown: `
## Mål for undervisningen
H4 har til formål at give eleverne kompetencer til at designe og udvikle cross-platform mobile applikationer med Flutter, hvor backend-integration, API-kommunikation og offline funktionalitet indgår. Efter forløbet skal eleven kunne:

- Designe og udvikle mobilapplikationer med Flutter.
- Integrere API’er til kommunikation med backend-systemer.
- Implementere offline funktionalitet via lokal datalagring.
- Gennemføre test og debugging for at sikre kvalitet, stabilitet og sikkerhed.
- Dokumentere og præsentere både arbejdsproces og færdigt produkt.

## Indhold i undervisningen
- Mobile app-udvikling: Flutter/Dart, widgetstruktur, navigation og UI-design.
- Backend-integration: REST API’er i C#/.NET, CRUD, JWT, hosting i Docker.
- Lokal datalagring: SQLite/synkronisering.
- Test og sikkerhed: automatiserede tests, rollebaseret adgang, beskyttelse mod datalækage.
- Projektstyring: SCRUM (sprint-planlægning, reviews, retrospektiver).

Forløbet kulminerer i projektet **Mobil kundestyringsapp**.
`.trim()
  },
  {
    slug: 'hovedforlob-5',
    short: 'H5',
    titel: 'Hovedforløb 5',
    kort: 'Linux administration, IoT/embedded og Big Data pipelines i et integreret mini-svendeprøveprojekt.',
    markdown: `
## Mål for undervisningen
Formålet med H5 er at give eleverne avancerede kompetencer inden for Linux-administration, embedded systemer, IoT og Big Data, samt at træne dem i at anvende disse teknologier i en helhedsorienteret udviklingsopgave. Efter forløbet skal eleven kunne:

- Opsætte, konfigurere og sikre Linux-baserede servere og embedded enheder.
- Udvikle og implementere IoT-løsninger med dataindsamling, overførsel og behandling.
- Etablere en Big Data-pipeline til lagring, analyse og visualisering af data.
- Integrere teknologierne i et samlet projekt med kravspecifikation, dokumentation og præsentation.
- Arbejde selvstændigt og i teams med projektstyring og procesrefleksion.

## Indhold i undervisningen
Tre teknologispor efterfulgt af en mini-svendeprøve:

- Linux på server og embedded: Ubuntu-server, brugere, netværk, sikkerhed, Docker og embedded Linux-platforme.
- Embedded systemer og IoT: Arduino/BeagleBone, sensorintegration, dataoverførsel, IoT-sikkerhed.
- Big Data: pipelines (Kafka/RabbitMQ), SQL/NoSQL (inkl. timescaleDB), visualisering (Dash/Grafana m.fl.).
- Mini-svendeprøveprojekt: 4-ugers gruppeprojekt med integration af alle elementer.
`.trim()
  },
  {
    slug: 'hovedforlob-6',
    short: 'H6',
    titel: 'Hovedforløb 6 (Svendeprøve)',
    kort: 'Afsluttende projekt + individuel mundtlig fremlæggelse, 5 uger med dokumentation.',
    markdown: `
## Svendeprøveforløbet
EUD hovedforløb 6 (eller afslutningen af EUX hovedforløb 5) indeholder den afsluttende prøve, fremover kaldet svendeprøven.

Svendeprøveforløbet er på i alt fem uger, hvor de første fire uger anvendes til at udarbejde en projektopgave med tilhørende dokumentation (infrastruktur eller programmering). Den sidste uge anvendes til forberedelse og afholdelse af selve svendeprøven.

## Delprøver
- Projektopgavens løsning samt tilhørende dokumentation
- En individuel mundtlig fremlæggelse (40 minutter inkl. votering)

Begge delprøver skal bestås (min. 02) og den endelige karakter er gennemsnittet.

## Dokumentation
- Produktrapport: kravspecifikation, vejledning, produktdokumentation og bilag.
- Procesrapport: problemformulering, projektplanlægning, logbog og konklusion.
`.trim()
  }
];

