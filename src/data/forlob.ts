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
    kort: 'Grundlæggende infrastruktur: netværk, servere, AD, backup, sikkerhed og samarbejde.',
    markdown: `
## Mål for undervisningen
Undervisningen har til formål at give eleverne en solid teknisk og metodisk forståelse for opbygning, drift og fejlfinding af en virksomheds IT-infrastruktur. Efter forløbet skal eleven kunne:

- Designe, implementere og dokumentere en infrastrukturløsning ud fra en kravspecifikation.
- Forstå og anvende centrale teknologier inden for netværk, servere, virtualisering, storage, backup, directory services og netværkssikkerhed.
- Anvende grundlæggende principper for informationssikkerhed og cybersikkerhed, herunder udvikle adgangskontrol- og passwordpolitikker.
- Indgå i samarbejde med forskellige personlighedstyper og bidrage konstruktivt i projektgrupper.
- Kommunikere tekniske løsninger mundtligt og skriftligt samt argumentere for valg af teknologier og metoder.
- Anvende kundeserviceprincipper i it-driftskontekster.

## Indhold i undervisningen
Undervisningen kombinerer teori, praktiske øvelser og projektarbejde inden for følgende hovedtemaer:

- Netværk og infrastruktur: netværksdesign, adressering, VLAN, routing, trådløse netværk, redundans, sikkerhed på OSI lag 2.
- Servere og virtualisering: Windows- og Linux-servere, virtualisering, directory services, storage-løsninger, backupstrategier.
- Informationssikkerhed og cybersikkerhed: privacy, databeskyttelse, trusselvektorer, risikovurdering, adgangskontrol- og passwordpolitikker.
- Automatisering og drift: scripting med PowerShell, serveradministration og overvågning.
- Samarbejde og kommunikation: kundeservice, konflikthåndtering, behovsafdækning, dokumentation og præsentation.

## Helhedsorientering
Eleverne arbejder med sammenhængen mellem netværk, servere, sikkerhed, backup og kundeservice som dele af en samlet IT-infrastruktur.

## Praksisrelation
Udgangspunkt i realistiske cases fra erhvervslivet. Teori omsættes til løsninger, der dokumenteres, testes og præsenteres for tekniske og ikke-tekniske målgrupper.

## Tværfaglighed
Netværk, server, storage, backup, sikkerhed, scripting, kommunikation og kundeservice ses i sammenhæng.

## Differentiering
Varierede arbejdsformer, ekstra udfordringer ved behov, løbende 1:1-samtaler og fleksibel gruppedannelse.

## Evaluering og bedømmelse (uddrag)
- Introduktion (36 lektioner): repetition af GF2, trivsel, gruppeinddeling, projektfremlæggelse.
- Undervisningsfase (144 lektioner): proof-of-concept etablering af basisteknologier, daglige oplæg og seminarer.
- H1 projekt (144 lektioner): design, implementering og dokumentation ud fra case/kravspecifikation.
- Projektfremlæggelse og evaluering (36 lektioner): præsentation, argumentation og refleksion over proces og valg.
`.trim()
  },
  {
    slug: 'hovedforlob-2',
    short: 'H2',
    titel: 'Hovedforløb 2',
    kort: 'Linux-servere, NAT/routing/OSPF, netværksstyring, databaser og integreret projekt.',
    markdown: `
## Mål for undervisningen
Undervisningen udbygger elevernes tekniske kompetencer inden for avanceret serverteknologi og netværksinfrastruktur. Efter forløbet skal eleven kunne:

- Installere, konfigurere og administrere Linux-servere samt services (fil, DNS, DHCP, webserver).
- Implementere og optimere netværksløsninger (NAT, routing, netværksstyring, virtualisering).
- Planlægge og implementere backup og sikkerhedsforanstaltninger.
- Designe og gennemføre et integreret infrastrukturprojekt ud fra realistiske kravspecifikationer.
- Dokumentere løsninger og samarbejde effektivt i projektteams.

## Indhold i undervisningen
- Serverteknologi: Linux-arkitektur, webservere, deployment services, sikkerhed og backup. Introduktion til ISO 27001/27002 m.m.
- Netværk og cybersikkerhed: NAT, routing, netværksstyring (NTP, CDP/LLDP), SDN, QoS, OSPF, compliance og MFA.
- Projektarbejde: integreret case med design, implementering og dokumentation.

## Evaluering og bedømmelse (uddrag)
- Serverteknologi (72 lektioner)
- Netværk (72 lektioner)
- Database (108 lektioner)
- Projektperioden (108 lektioner)
`.trim()
  },
  {
    slug: 'hovedforlob-3',
    short: 'H3',
    titel: 'Hovedforløb 3',
    kort: 'Avanceret netværk/virtualisering, automation, overvågning og ITIL4 practices i projekt.',
    markdown: `
## Mål for undervisningen
Fokus på performance, sikkerhed, kapacitet og skalerbarhed. Efter forløbet skal eleven kunne:

- Analysere nuværende og fremtidige netværks-/virtualiseringsbehov.
- Designe og implementere netværk og virtualisering samt integrere centrale ITIL4-principper.
- Konfigurere og optimere infrastruktur og automatisering.
- Udarbejde kravspecifikation, projektplan og dokumentation efter svendeprøveprincipper.

## Indhold i undervisningen (uddrag)
- Netværk: WLAN, OSPF/eBGP, VLAN/trunk/etherchannel, VRF/VxLAN/GRE/IPsec, multicast, QoS, NTP, NAT/PAT, FHRP.
- Virtualisering: hypervisor + management, SAN/NAS/iSCSI, clustering, live migration, containere.
- IT Service Management: 7 ITIL4 practices.
- Projektarbejde: selvvalgte cases med fuld cyklus.
`.trim()
  },
  {
    slug: 'hovedforlob-4',
    short: 'H4',
    titel: 'Hovedforløb 4',
    kort: 'Mangler i det leverede materiale (indsæt når I har teksten).',
    markdown: `
## Mangler
Der var ikke indsat tekst for Hovedforløb 4 i det materiale jeg fik. Når I har det, kan det lægges ind her i samme struktur som de andre hovedforløb.
`.trim()
  },
  {
    slug: 'hovedforlob-5',
    short: 'H5',
    titel: 'Hovedforløb 5',
    kort: 'Komplekse virtualiserings- og cloudløsninger, kravspecifikation og projektstyring.',
    markdown: `
## Mål for undervisningen
Forløbet samler og anvender tekniske og projektmæssige kompetencer i komplekse virtualiserings- og cloudløsninger. Efter forløbet skal eleven kunne:

- Analysere behov for IT-infrastruktur.
- Designe og implementere samlet løsning med fokus på performance, sikkerhed, kapacitet og skalerbarhed.
- Arbejde med avanceret virtualisering og cloud inkl. sikkerhed og lovgivning.
- Udarbejde og analysere kravspecifikationer ud fra kundebehov.
- Arbejde med projektstyring og systematisk fejlfinding.
- Dokumentere og præsentere løsninger, samt reflektere over alternative valg.

## Indhold i undervisningen (uddrag)
- Virtualisering: hypervisor/management, redundans, storage, cluster, HA/FT, live migration.
- Cloud: private/public/hybrid, IaaS/PaaS/SaaS, GDPR/ISO27001, sikkerhed og compliance.
- Kravspecifikation, systemudvikling, projektstyring og fejlfinding.
`.trim()
  },
  {
    slug: 'hovedforlob-6',
    short: 'H6',
    titel: 'Hovedforløb 6 (Svendeprøve)',
    kort: 'Afsluttende projekt + individuel mundtlig fremlæggelse, 5 uger med dokumentation.',
    markdown: `
## Svendeprøveforløbet
Svendeprøven varer 5 uger: 4 uger til projekt + dokumentation, 1 uge til forberedelse og afholdelse.

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

