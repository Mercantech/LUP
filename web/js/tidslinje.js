/** Tidslinje: Inden H1 → H1 → Inden H2 → … → H6 */

const img = (name, alt) =>
  `<figure class="praktik-fig"><img src="assets/praktik/${name}" alt="${alt}" loading="lazy" /></figure>`;

export const tidslinjeMeta = {
  title: "Tidslinje",
  lead:
    "Uddannelsens rytme fra praktik før H1 til svendeprøven på H6 — skoleperioder og praktikmål i den rækkefølge, eleven møder dem.",
};

export const tidslinje = [
  {
    id: "inden-h1",
    type: "praktik",
    label: "Inden H1",
    title: "Praktik mellem GF2 og H1",
    summary:
      "Kendskab til virksomhedens teknologier, og blød start på versionsstyring og database.",
    image: "gf2-h1.png",
    body: `
      ${img("gf2-h1.png", "Praktikmål mellem GF2 og H1")}
      <p><strong>Praktikmål for virksomheden generelt</strong></p>
      <ul class="check-list">
        <li><strong>Programudvikling.</strong> Lærlingen har kendskab til programudvikling og til virksomhedens teknologier og struktur.</li>
      </ul>
      <p><strong>Forberedelse til 1. hovedforløb</strong></p>
      <ul class="check-list">
        <li><strong>Versionsstyring.</strong> GitHub bruges på H1 — sæt eleven ind i egne værktøjer og grundprincipper.</li>
        <li><strong>Database.</strong> Vis virksomhedens databasestruktur og valg inden SQL på H1.</li>
        <li><strong>Motivation.</strong> Det vigtigste er, at eleven møder motiveret og spændt op.</li>
      </ul>
    `,
  },
  {
    id: "h1",
    type: "skole",
    label: "H1",
    title: "Hovedforløb 1 — Programmering",
    summary:
      "Fundament i C#, OOP, SQL, HTTP, Blazor og projektarbejde (SCRUM/GitHub).",
    image: "h1.png",
    href: "#/h1",
    topics: [
      "Introforløb & studieteknik",
      "Objektorienteret programmering",
      "Clientside / Blazor",
      "SQL & ADO.NET",
      "Netværk I",
    ],
  },
  {
    id: "inden-h2",
    type: "praktik",
    label: "Inden H2",
    title: "Praktik mellem H1 og H2",
    summary:
      "Webapplikationer, versionering og database — plus forberedelse til ORM og frontend på H2.",
    image: "h1-h2.png",
    body: `
      ${img("h1-h2.png", "Praktikmål mellem H1 og H2")}
      <p><strong>Praktikmål for virksomheden generelt</strong></p>
      <ul class="check-list">
        <li><strong>(Web)applikationer.</strong> Enkle fullstack-løsninger med moderne frontend og backend.</li>
        <li><strong>Versionering.</strong> Forståelse for virksomhedens Git-arbejdsgange.</li>
        <li><strong>Databaseudvikling.</strong> Erfaring med databaser, typer og værktøjer.</li>
      </ul>
      <p><strong>Forberedelse til 2. hovedforløb</strong></p>
      <ul class="check-list">
        <li><strong>Versionsstyring.</strong> Mere praktisk indsigt i professionel versionskontrol.</li>
        <li><strong>Frontend.</strong> Holde Blazor-færdigheder ved lige.</li>
        <li><strong>Database.</strong> Kendskab til dataarbejde og evt. ORM i virksomheden.</li>
      </ul>
    `,
  },
  {
    id: "h2",
    type: "skole",
    label: "H2",
    title: "Hovedforløb 2 — Programmering",
    summary:
      "EF Core, REST API, SPA-frontend, AD/server og IT-service / ticketsystem.",
    image: "h2.png",
    href: "#/h2",
    topics: [
      "Videregående OOP & SOLID",
      "SQL & Entity Framework Core",
      "REST API med ASP.NET Core",
      "SPA-frontend",
      "Server, AD & ITSM",
    ],
  },
  {
    id: "inden-h3",
    type: "praktik",
    label: "Inden H3",
    title: "Praktik mellem H2 og H3",
    summary: "Viderefør API-, database- og frontend-erfaring inden IoT-forløbet.",
    image: "h2-h3.png",
    body: `
      ${img("h2-h3.png", "Praktikmål mellem H2 og H3")}
      <p>Brug praktikperioden til at holde API-, database- og frontend-kompetencerne varme inden H3, hvor IoT, sikkerhed og dashboard kommer i fokus.</p>
    `,
  },
  {
    id: "h3",
    type: "skole",
    label: "H3",
    title: "Hovedforløb 3 — Programmering",
    summary:
      "IoT med Arduino, .NET API, EF Core, dashboard, JWT/auth og sikkerhed.",
    image: "h3.png",
    href: "#/h3",
    topics: [
      "Serverside & DTO’er",
      "SPA-dashboard",
      "Softwaretest & sikkerhed",
      "ORM / EF Core",
      "IoT & embedded",
    ],
  },
  {
    id: "inden-h4",
    type: "praktik",
    label: "Inden H4",
    title: "Praktik mellem H3 og H4",
    summary: "Hold API- og sikkerhedskompetencerne ved lige inden app-udvikling.",
    image: "h3-h4.png",
    body: `
      ${img("h3-h4.png", "Praktikmål mellem H3 og H4")}
      <p>Fortsæt gerne med API, auth og database, så eleven er klar til mobiludvikling og kravspecifikation på H4.</p>
    `,
  },
  {
    id: "h4",
    type: "skole",
    label: "H4",
    title: "Hovedforløb 4 — Programmering",
    summary:
      "Flutter/Dart, API-integration, kravspec, Scrum og fortsat serverside/sikkerhed.",
    image: "h4.png",
    href: "#/h4",
    topics: [
      "Appprogrammering 1 & 2",
      "Softwaretest & sikkerhed",
      "IT-kravspecifikation",
      "Programmeringsmetodik (Scrum)",
      "Serverside (fortsat)",
    ],
  },
  {
    id: "inden-h5",
    type: "praktik",
    label: "Inden H5",
    title: "Praktik mellem H4 og H5",
    summary: "Softwaretest, app og sikkerhed — plus blød start på Linux og projektstyring.",
    image: "h4-h5.png",
    body: `
      ${img("h4-h5.png", "Praktikmål mellem H4 og H5")}
      <p><strong>Praktikmål for virksomheden generelt</strong></p>
      <ul class="check-list">
        <li><strong>Softwaretest.</strong> Kvalitetssikring og test af programmer</li>
        <li><strong>App.</strong> Udviklet eller hjulpet med app i et framework</li>
        <li><strong>Sikkerhed.</strong> Indsigt i sikkerhedsløsninger i softwareudvikling</li>
      </ul>
      <p><strong>Forberedelse til 5. hovedforløb</strong></p>
      <ul class="check-list">
        <li><strong>Linux.</strong> Kendskab til Linux i virksomhedens systemlandskab</li>
        <li><strong>Projektstyring.</strong> Mindre opgaver med planlægning og dokumentation</li>
      </ul>
    `,
  },
  {
    id: "h5",
    type: "skole",
    label: "H5",
    title: "Hovedforløb 5 — Programmering",
    summary:
      "Linux, Docker, message queues, TimescaleDB, avanceret JWT og mini-svendeprøve.",
    image: "h5.png",
    href: "#/h5",
    topics: [
      "Linux-server",
      "Docker & Compose",
      "IoT & message queue",
      "TimescaleDB",
      "CI/CD & mini-svendeprøve",
    ],
  },
  {
    id: "inden-h6",
    type: "praktik",
    label: "Inden H6",
    title: "Praktik mellem H5 og H6",
    summary: "Projektstyring og embedded — forberedelse til svendeprøven.",
    image: "h5-h6.png",
    body: `
      ${img("h5-h6.png", "Praktikmål mellem H5 og H6")}
      <p><strong>Praktikmål for virksomheden generelt</strong></p>
      <ul class="check-list">
        <li><strong>Projektstyring.</strong> Deltagelse i udvikling med projektstyringsværktøjer</li>
        <li><strong>Embedded systemer.</strong> Programmering til embedded med Linux eller anden platform</li>
      </ul>
      <p><strong>Forberedelse til 6. hovedforløb</strong></p>
      <ul class="check-list">
        <li><strong>Projektstyring.</strong> Ledelse, dokumentation og procesrapport</li>
        <li><strong>Projektorienteret arbejde.</strong> Ansvar for udviklingsopgaver fra bunden</li>
      </ul>
    `,
  },
  {
    id: "h6",
    type: "skole",
    label: "H6",
    title: "Hovedforløb 6 — Svendeprøven",
    summary:
      "20 dages svendeprøve, systemudvikling, projektstyring (PRINCE2-niveau) og eksamen.",
    image: "h6.png",
    href: "#/h6",
    topics: [
      "Svendeprøve (20 dage)",
      "Systemudvikling",
      "Projektstyring",
      "Eksamen & fagligt emne",
    ],
  },
];
