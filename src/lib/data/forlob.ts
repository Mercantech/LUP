/** Placeholder-data til forløb – erstattes med API-kald når Postgres er klar */
import type { Forlob } from '$lib/types/forlob';

export const FORLOB_PLACEHOLDER: Forlob[] = [
	{
		id: '1',
		slug: 'gf2',
		titel: 'Grundforløb 2',
		rækkefølge: 1,
		beskrivelse: 'Grundlæggende færdigheder inden for datateknik',
		indhold: {
			indhold_undervisning: {
				tekst:
					'GF2 danner fundamentet for uddannelsen. Her arbejder eleverne med grundlæggende IT-forståelse, problemløsning og studie-/arbejdsformer, som bliver bygget videre på i hovedforløbene.'
			}
		}
	},
	{
		id: '2',
		slug: 'h1',
		titel: 'Hovedforløb 1',
		rækkefølge: 2,
		beskrivelse: 'Grundlæggende programmering, netværk, OOP, database og Blazor',
		indhold: {
			mål: {
				formål:
					'Hovedforløb 1 giver eleverne et solidt fundament i grundlæggende programmering og relaterede teknologier. Kompetencerne samles i et helhedsorienteret projekt, så teori omsættes til praksis.',
				efter_forløbet: [
					'Udvikle mindre applikationer i C# med objektorienterede principper.',
					'Designe og anvende relationelle databaser samt skrive SQL til almindelige opgaver.',
					'Forstå grundlæggende netværkskommunikation, klient/server-arkitektur og HTTP.',
					'Implementere brugervenlige brugergrænseflader med Blazor.',
					'Samarbejde i teams med SCRUM og anvende GitHub til versionsstyring.',
					'Dokumentere og reflektere over proces og produkt.'
				]
			},
			indhold_undervisning: {
				tekst:
					'Undervisningen veksler mellem korte teoriforløb og praktiske øvelser, der løbende kobles til projektarbejdet. Eleverne arbejder med både backend, database og frontend – og lærer at få delene til at spille sammen i en samlet løsning.',
				temaer: [
					'Grundlæggende programmering: variabler, datatyper, kontrolstrukturer, metoder, debugging og introduktion til OOP i C#.',
					'Netværk og kommunikation: klient/server, OSI-modellen og HTTP-protokoller.',
					'Objektorienteret programmering: klasser, arv, polymorfi, interfaces, indkapsling og UML.',
					'Databasedesign og integration: relationelle databaser, SQL, CRUD og ADO.NET-integration.',
					'Frontend med Blazor: databinding, komponenter, routing, HTML/CSS/Bootstrap og JavaScript-integration.',
					'Projektstyring og samarbejde: SCRUM, sprint-planlægning, retrospektiver og GitHub-workflows.'
				],
				projektnavn: 'Niche Markedsplads'
			},
			helhedsorientering:
				'Alle H1-fag integreres i projektarbejdet, så eleverne oplever en tydelig sammenhæng mellem teori og praksis. Projektet rammesættes som en erhvervsnær opgave, hvor udviklingsprocessen spejler branchens arbejdsgange fra idé til færdigt produkt.',
			praksisrelation:
				'Forløbet tilrettelægges med fokus på professionelle arbejdsgange. Eleverne arbejder med Visual Studio, GitHub, PostgreSQL og Blazor i et setup, der ligner virkelige udviklingsprojekter. SCRUM bruges aktivt med sprintplanlægning, standups og løbende feedback.',
			tværfaglighed:
				'Forløbet kombinerer programmering, databasedesign, netværk, frontend-udvikling og projektstyring. Eleverne træner at integrere teknologier og metoder på tværs, så løsningen fungerer både teknisk og brugermæssigt.',
			differentiering:
				'Eleverne arbejder i selvvalgte grupper og kan tilpasse projektets kompleksitet i dialog med underviseren. Der tilbydes supplerende udfordringer til elever, der vil fordybe sig, fx spilprojekter, ekstra OOP-øvelser eller automatisering med Selenium.',
			evaluering: {
				læringsaktiviteter: [
					{
						titel: 'Grundlæggende programmering',
						lektioner: 72,
						læringsmål: [
							'Designe og udvikle konsolprogrammer ud fra case-beskrivelser.',
							'Oprette, fejlfinde og teste mindre konsolapplikationer i C#.',
							'Forstå og anvende datatyper.',
							'Anvende kontrolstrukturer, metoder, variabler og konstanter.',
							'Dokumentere kode med kommentarer og versionsstyring.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Informations- og dokumentationssøgning',
								'Kravspecifikationer',
								'Grundlæggende fejlfinding og debugging',
								'Testmetoder og værktøjer',
								'Metoder, arrays og kontrolstrukturer'
							],
							praktisk_del: [
								'Udvikling af varierede og enkle konsolprogrammer i C#',
								'Dagsbaserede opgaver fra Microsoft Learn',
								'Grundlæggende GitHub'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback:
								'Løbende mundtlig feedback på evnen til at planlægge og designe konsolprogrammer samt identificere og rette fejl.',
							ved_bedommelsen:
								'Der lægges vægt på hele softwareudviklingscyklussen fra design til test, begrundede valg, dokumenteret proces, versionsstyring og fejlfindingsteknikker.'
						}
					},
					{
						titel: 'Netværk I',
						lektioner: 72,
						læringsmål: [
							'Designe og implementere netværksløsninger, der opfylder krav.',
							'Administrere og konfigurere VLAN og routing i netværksmiljøer.',
							'Sikre netværk, redundans og pålidelighed med relevante protokoller og metoder.',
							'Udføre fejlfinding og diagnosticering samt implementere sikkerhedstiltag.',
							'Have grundlæggende forståelse for Layer 2 og Layer 3.'
						],
						indholdselementer: {
							teoretisk_del: [
								'OSI-modellen',
								'Grundprincipper i netværksdesign: arkitektur, protokoller og komponenter',
								'Routing og switching: VLAN, IP-routing og Layer 2-switching'
							],
							praktisk_del: [
								'Hands-on øvelser med VLAN, routing og Layer 3-switching',
								'Arbejde med at lave egen webserver i C#'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback:
								'Der gives særligt feedback på engagement, nysgerrighed og arbejdet med CCNA-materiale.',
							ved_bedommelsen:
								'Der lægges vægt på evnen til at forstå pensum fra Cisco og relatere det til softwareudvikling.'
						}
					},
					{
						titel: 'Clientsideprogrammering',
						lektioner: 36,
						læringsmål: [
							'Oprette websider med HTML, CSS og Bootstrap til ensartede og responsive brugergrænseflader.',
							'Forklare client/server-modellen og have forståelse for HTTP-protokollen.',
							'Oprette HTML-dokumenter med korrekt struktur og semantik.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Client/server-modellen og HTTP',
								'Semantisk HTML og Bootstrap',
								'Introduktion til UX og HCI'
							],
							praktisk_del: [
								'Agilt projektarbejde på webfrontend: fra simpelt til mere komplekst design',
								'Daglige opgaver, fremlæggelser og feedback loops i grupper og plenum',
								'Fælles udgangspunkt: C#, Blazor med Bootstrap og SQL i projektarbejde'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback:
								'Der gives særligt feedback på samarbejde, engagement og nysgerrighed.',
							ved_bedommelsen:
								'Der lægges vægt på evnen til at udvikle fra statisk enkelt-side til mere dynamisk løsning og kunne begrunde egne valg.'
						}
					},
					{
						titel: 'Databaseprogrammering, serverteknologi (databaseserver) & computerteknologi',
						lektioner: 108,
						læringsmål: [
							'Have kendskab til serveropsætning og drift (web- og db-server).',
							'Bruge SQL til at oprette, ændre og aflæse data i en lokal database.',
							'Redegøre for relationstyper mellem tabeller og deres betydning for design.',
							'Installere og konfigurere Visual Studio til udvikling på webserver-, database- og applikationsniveau.',
							'Route til forskellige websider baseret på domænenavne.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Databaseadministration og sikkerhed',
								'Serveropsætning og drift',
								'Introduktion til relationelle databaser og alternativer'
							],
							praktisk_del: [
								'SQL-øvelser: SELECT, INSERT, UPDATE, DELETE, WHERE, JOIN',
								'Skrive scripts til at automatisere gentagne databaseopgaver',
								'Installere og konfigurere IIS og SQL database (lokalt eller via Azure el.lign.)'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback:
								'Feedback med fokus på databaseadministration, webserver-funktionalitet, relationsforståelse og simple CRUD-operationer.',
							ved_bedommelsen:
								'Der lægges vægt på korrekt og effektiv SQL, evnen til at udføre mere komplekse forespørgsler samt at kunne forklare relationstyper med eksempler (gerne via frontend).'
						}
					},
					{
						titel: 'Objektorienteret programmering',
						lektioner: 72,
						læringsmål: [
							'Forstå og anvende OOP-koncepter til at strukturere softwareprojekter.',
							'Arbejde med klasser til at oprette og bruge objekter.',
							'Definere og designe egne klasser.',
							'Udarbejde en applikation, der benytter OOP effektivt.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Grundlæggende OOAD og OOP-koncepter',
								'Fordele ved OOP i softwareudvikling'
							],
							praktisk_del: [
								'Oprettelse af klasser og objekter i C#',
								'Implementering af grundlæggende OOP-koncepter i en applikation',
								'Anvendelse af lister, arrays m.m. til at organisere data',
								'Implementering af abstrakte klasser og metoder'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback:
								'Feedback på forståelse for OOP-koncepter og evne til at designe og strukturere løsninger.',
							ved_bedommelsen:
								'Der lægges vægt på at implementere OOP i praksis samt at kunne forklare, hvordan OOP indgår i design og analyse.'
						}
					}
				]
			}
		}
	},
	{
		id: '3',
		slug: 'h2',
		titel: 'Hovedforløb 2',
		rækkefølge: 3,
		beskrivelse: 'Backend, Entity Framework, API, sikkerhed og real-time (bookingprojekt)',
		indhold: {
			mål: {
				formål:
					'Hovedforløb 2 bygger videre på fundamentet fra H1 og giver eleverne kompetencer til at udvikle mere komplekse og skalerbare systemer, hvor backend, database og frontend hænger naturligt sammen.',
				efter_forløbet: [
					'Udvikle skalerbare backend-systemer med ORM (Entity Framework).',
					'Designe og implementere databaser med avancerede relationer.',
					'Udvikle responsive og brugervenlige Blazor-frontends.',
					'Arbejde med API-design og integration til dataudveksling.',
					'Implementere sikkerhed i praksis (autentifikation og autorisation).',
					'Arbejde med real-time kommunikation i web (fx SignalR/WebSockets).',
					'Anvende SCRUM til planlægning, udvikling og evaluering.',
					'Dokumentere og præsentere arbejdsproces og produkt – både teknisk og forretningsmæssigt.'
				]
			},
			indhold_undervisning: {
				tekst:
					'Undervisningen kombinerer teori, øvelser og projektarbejde. Eleverne arbejder med API-kald fra klienten, udvidet OOP, datalaget via Entity Framework, sikkerhed (JWT/roller) og real-time funktionalitet – med løbende fokus på kvalitet, samarbejde og leverancer.',
				temaer: [
					'Backend: API-design, forretningslogik, validering, asynkron programmering og sikkerhed.',
					'Database: avanceret SQL, relationer (1:1, 1:mange, mange:mange), constraints, triggers og stored procedures – integreret via ORM.',
					'Frontend: dynamiske grænseflader, navigation, brugerinputvalidering og datapræsentation i Blazor.',
					'Sikkerhed: autentifikation/autorisation, roller og token-baseret login (fx JWT).',
					'Real-time web: SignalR/WebSockets, events og live opdateringer i UI.',
					'Projektstyring: SCRUM, sprint-planlægning, backlog og retrospektiver.',
					'Versionsstyring og leverancer: ugentlige releases, release notes og løbende kvalitetssikring.',
					'Test og kvalitet: automatiserede tests, fejlhåndtering og forbedring af brugeroplevelsen.'
				],
				projektnavn: 'Booking side (H2)'
			},
			helhedsorientering:
				'Backend, database og frontend bindes sammen i projektarbejdet. Eleverne arbejder med alle udviklingsfaser – fra idé og design til implementering, test og præsentation – som et sammenhængende forløb.',
			praksisrelation:
				'Projektet tager udgangspunkt i en erhvervsrelevant problemstilling: et bookingsystem med fokus på dataintegritet, sikkerhed og skalerbarhed. Arbejdet organiseres som et teamforløb med faste leverancer (ugentlige releases), refleksion over proces og løbende feedback. Eleverne anvender værktøjer som Visual Studio, GitHub, Blazor, Postman, servermiljø og Entity Framework.',
			tværfaglighed:
				'Forløbet integrerer backend-programmering, databasesystemer, frontend-design, serveradministration og projektstyring – samt relaterede emner som API-design, sikkerhed, cloudhosting og brugeroplevelsesoptimering.',
			differentiering:
				'Projektets kompleksitet kan tilpasses niveau, fx enkelt- vs. multi-hotel løsning. Elever kan udvide med ekstra funktioner som betalingsintegration, loyalitet, sæsonpriser eller algoritme-/spiludvikling.',
			evaluering: {
				læringsaktiviteter: [
					{
						titel: 'OOP, DB & GUI',
						lektioner: 180,
						læringsmål: [
							'Arbejde projektorienteret ud fra en udleveret case.',
							'Dokumentere arbejdet skriftligt i form af en rapport.',
							'Anvende avancerede OOP-koncepter og beskrive klassestruktur med UML class diagram.',
							'Implementere GUI i et professionelt udviklingsmiljø og koble det til objektorienteret kode.',
							'Designe database med E/R og/eller UML-diagram samt forbinde C# til SQL database.',
							'Anvende et bredt udvalg af SQL-kommandoer i scripts.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Grundlæggende opsætning, udvikling og drift',
								'Udviklingsværktøjer og frameworks (fx Visual Studio)',
								'UML-diagrammer og dokumentation',
								'Introduktion til grafisk brugergrænsefladedesign',
								'API-kald fra klienten og udvidet OOP'
							],
							praktisk_del: [
								'Fra manuel opsætning til avanceret løsningsdesign i en projekt-case',
								'Overgang fra manuel MS SQL-opsætning til vedligehold via Entity Framework',
								'API-integration, autentifikation (JWT) og roller',
								'Real-time funktionalitet (fx SignalR/WebSockets) og ticket-system',
								'Ugentlige releases og dokumentation',
								'Løbende eksperimenter og forbedringer undervejs'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback:
								'Der gives løbende mundtlig feedback på motivation, nysgerrighed, OOP-principper og formidling af UML.',
							ved_bedommelsen:
								'Der lægges vægt på elevens interesse og tilgang til at eksperimentere, forklare og forbedre OOAD/OOP-kompetencer.'
						}
					},
					{
						titel: 'Clientsideprogrammering',
						lektioner: 72,
						læringsmål: [
							'Designe, udvikle, teste og dokumentere en clientside webløsning ud fra en case.',
							'Tilgå en JSON API og præsentere data med C#/JavaScript, HTML, CSS og Bootstrap.',
							'Udvikle mere avanceret responsivt indhold med lister, links, tabeller, semantik og DOM manipulation.'
						],
						indholdselementer: {
							teoretisk_del: [
								'UX/UI best-practice',
								'Semantisk HTML og HTML5',
								'Clientside vs serverside',
								'Bootstrap',
								'Kommunikation med API'
							],
							praktisk_del: [
								'Clientside applikation baseret på case',
								'API-kald og visualisering af data (fx Blazor-app der kalder API)',
								'Testmetoder og værktøjer',
								'DOM manipulation'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback:
								'Feedback med fokus på om eleven udfordrer sig selv og er nysgerrig ift. muligheder og alternativer.',
							ved_bedommelsen: 'Der lægges vægt på kvaliteten af den clientside webapplikation.'
						}
					},
					{
						titel: 'Serveradministration/sikkerhed (serverteknologi & service management)',
						lektioner: 108,
						læringsmål: [
							'Opsætte og administrere Active Directory.',
							'Programmere automatisk backup af server og håndtere database-backups.',
							'Administrere brugerkonti på en server.',
							'Implementere et client/server system.',
							'Have grundlæggende forståelse for sikkerhed: brugerstyring, firewalls og diskkryptering.',
							'Redegøre for 1st level support og skelne mellem service requests og fejlmeldinger.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Serveradministration, netværkskoncepter og sikkerhed',
								'Service management',
								'DHCP, DNS samt netværks- og serveropsætning'
							],
							praktisk_del: [
								'Grupper opsætter et servermiljø til hele projekt-casen',
								'Simuleret webarkitektur med DHCP, DNS, IIS og SQL-miljø'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejde og nysgerrighed i det praktiske arbejde.',
							ved_bedommelsen:
								'Der lægges vægt på forståelse for serverteknologi, servicemanagement og sikkerhed – samt formidling af det i overhøring.'
						}
					}
				]
			}
		}
	},
	{
		id: '4',
		slug: 'h3',
		titel: 'Hovedforløb 3',
		rækkefølge: 4,
		beskrivelse: 'IoT, Arduino, Interaktiv IoT Platform',
		indhold: {
			mål: {
				formål:
					'Hovedforløb 3 giver eleverne kompetencer til at designe og implementere komplette IoT-løsninger, hvor hardware, backend og frontend integreres i én samlet løsning.',
				efter_forløbet: [
					'Integrere IoT-enheder med backend og database.',
					'Designe og udvikle API’er til realtidsdataudveksling.',
					'Udvikle dashboards til visualisering og kontrol af IoT-enheder.',
					'Arbejde med sikkerhed, dataintegritet og skalerbarhed i IoT-systemer.',
					'Dokumentere og præsentere en helhedsorienteret løsning – teknisk og funktionelt.'
				]
			},
			indhold_undervisning: {
				tekst:
					'Forløbet kombinerer teori og praksis med fokus på at få hardware, dataflow og brugergrænseflade til at hænge sammen. Eleverne arbejder med sensorer/aktuatorer, API-lag, database og dashboard – med fokus på realtidsdata og stabil drift.',
				temaer: [
					'IoT/hardware: Arduino MKR IoT kit, sensorer, aktuatorer og C++-programmering.',
					'Backend/API: RESTful API’er i C#/.NET, CRUD, HTTPS og integration via Entity Framework.',
					'Database: relationelle databaser, avanceret SQL og ORM-integration.',
					'Frontend/dashboard: realtidsvisualisering og styring af enheder.',
					'Sikkerhed & performance: kryptering, beskyttelse mod angreb, skalerbarhed og pålidelighed.',
					'Projektstyring: SCRUM med sprints, standups, reviews og retrospektiver.'
				],
				projektnavn: 'Interaktiv IoT Platform'
			},
			helhedsorientering:
				'Serversideprogrammering, database, GUI, IoT og sikkerhed bindes sammen i et integreret projektforløb. Eleverne arbejder med alle faser – fra idé og hardwareopsætning til backend/databasedesign, frontend og test.',
			praksisrelation:
				'Projektet tager afsæt i scenarier, der ligner opgaver i virksomheder med IoT/automatisering. Eleverne bruger professionelt udstyr og værktøjer som Visual Studio, GitHub, Blazor, Postman og Azure/Docker.',
			tværfaglighed:
				'Forløbet kræver hardware/software-integration, backend og API, database/ORM, frontend/UI, sikkerhed og projektstyring. Det giver indsigt i hele kæden fra fysisk enhed til brugerens dashboard.',
			differentiering:
				'Elever kan udvide løsningen med flere sensorer/aktuatorer, historiske data, avanceret filtrering, brugerroller, ekstra sikkerhedslag eller cloud-/3.-parts integration.',
			evaluering: {
				læringsaktiviteter: [
					{
						titel: 'Teoretisk grundlag',
						lektioner: 108,
						læringsmål: [
							'Kendskab til forløbets udviklingsværktøj og design af brugerinterfaces (SPA/mobile).',
							'Forståelse for 3-lags arkitektur og implementering af database i arkitekturen.',
							'Kendskab til User Stories og klient/server-kommunikation.',
							'Kendskab til sikkerhed, kryptering, udviklingsmetodikker og softwaretest.',
							'Kendskab til målsætningen med IoT og IIoT samt integrationsmuligheder.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Udviklingsværktøj til apps på tværs af platforme',
								'Brugerinterface til mobiltelefon',
								'Kommunikation mellem klientapplikation, server og database',
								'Sikring af applikationer'
							],
							praktisk_del: [
								'Prototyping-øvelser ud fra indholdselementerne',
								'Erfaringsudveksling via præsentationer',
								'Programmere embedded IoT-enheder og integrere dem i en samlet løsning'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejde i gruppen.',
							ved_bedommelsen:
								'Der lægges vægt på skriftlig fremstilling af case og kravspecifikation – med fokus på User Case.'
						}
					},
					{
						titel: 'Implementering',
						lektioner: 180,
						læringsmål: [
							'Arbejde i et team og følge SCRUM.',
							'Implementere softwareprojekt ud fra kravspecifikation.',
							'Implementere client/server system med IoT og IIoT.',
							'Implementere sikkerhed (login/kryptering).',
							'Værdsætte og bidrage til godt samarbejde.'
						],
						indholdselementer: {
							teoretisk_del: ['Agile metoder', 'Scrum', 'Serverside programmering', 'Softwaretest og sikkerhed'],
							praktisk_del: [
								'Gruppen implementerer projektet efter SCRUM',
								'Der afholdes daglige teammøder og daglig scrum med underviser'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejdet i gruppen.',
							ved_bedommelsen: 'Der lægges vægt på proces og samarbejde i Scrum-forløbet samt den samlede implementering.'
						}
					},
					{
						titel: 'Test og præsentation',
						lektioner: 36,
						læringsmål: [
							'Præsentere projektet og forklare løsningens virkemåde.',
							'Argumentere for valg truffet undervejs i udviklingen.',
							'Gennemføre accepttest og dokumentere hvad der er implementeret/ikke implementeret.'
						],
						indholdselementer: { teoretisk_del: ['Ingen teori'], praktisk_del: ['Gruppepræsentation', 'Individuel videodemonstration', 'Accepttest og afprøvning'] },
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejde og præsentation.',
							ved_bedommelsen:
								'Der lægges vægt på elevens evne til at indgå i scrumteam, resultatet af projektet og refleksion over mangler/alternativer.'
						}
					}
				]
			}
		}
	},
	{
		id: '5',
		slug: 'h4',
		titel: 'Hovedforløb 4',
		rækkefølge: 5,
		beskrivelse: 'Flutter, mobilapp, Mobil kundestyringsapp',
		indhold: {
			mål: {
				formål:
					'Hovedforløb 4 giver eleverne kompetencer til at designe og udvikle cross-platform mobile applikationer i Flutter – med backend-integration, API-kommunikation og offline funktionalitet.',
				efter_forløbet: [
					'Designe og udvikle mobilapplikationer med Flutter.',
					'Integrere API’er til kommunikation med backend-systemer.',
					'Implementere offline funktionalitet via lokal datalagring.',
					'Gennemføre test og debugging for stabilitet, kvalitet og sikkerhed.',
					'Dokumentere og præsentere arbejdsproces og færdigt produkt.'
				]
			},
			indhold_undervisning: {
				tekst:
					'Forløbet kombinerer teori, øvelser og projektarbejde. Eleverne arbejder med Flutter/Dart, navigation og UI-design – og kobler mobilappen til backend via REST API’er. Der arbejdes målrettet med offline/online logik og test.',
				temaer: [
					'Flutter & Dart: widgetstruktur, navigation og UI-design.',
					'Backend-integration: RESTful API’er, CRUD, sikkerhed (JWT) og hosting i Docker.',
					'Lokal datalagring: SQLite (eller lign.) og synkronisering.',
					'Test & sikkerhed: automatiserede tests, rollebaseret adgang og beskyttelse mod datalækage.',
					'Projektstyring: SCRUM med planlægning, reviews og retrospektiver.'
				],
				projektnavn: 'Mobil kundestyringsapp'
			},
			helhedsorientering:
				'GUI-programmering, programmeringsmetodik, serverside, softwaretest og database bindes sammen i en samlet kunde-simulation. Eleverne arbejder fra kravspecifikation til levering og præsentation.',
			praksisrelation:
				'Forløbet afspejler branchens krav til mobile løsninger: API-integration, lokal datalagring og responsive grænseflader. Værktøjer: Flutter SDK, VS Code, Postman, GitHub og Docker. Kundedialog simuleres via faste møder med “Product Owner”.',
			tværfaglighed:
				'Eleverne arbejder med mobiludvikling, UI/UX, backend/API, database/lokal lagring, test, sikkerhed og projektstyring i ét samlet forløb.',
			differentiering:
				'Elever kan udvide med hardwareintegration (GPS/kamera), push-notifikationer, avanceret offline-logik, analyser eller eksterne API’er. Teststrategi kan udbygges med integrationstests og performance-tests.',
			evaluering: {
				læringsaktiviteter: [
					{
						titel: 'Teoretisk grundlag',
						lektioner: 108,
						læringsmål: [
							'Kendskab til udviklingsværktøj til mobile apps og design af brugerinterfaces.',
							'Kendskab til User Stories og klient/server-kommunikation.',
							'Kendskab til sikkerhed og kryptering.',
							'Kendskab til udviklingsmetodikker og softwaretest.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Udviklingsværktøj til mobile apps',
								'Brugerinterface til mobiltelefon',
								'Kommunikation mellem klientapplikation og server',
								'Sikring af applikationer'
							],
							praktisk_del: [
								'Prototyping-opgaver ud fra indholdselementerne',
								'Erfaringsudveksling og præsentationer'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback: 'Løbende mundtlig feedback på motivation og nysgerrighed i prototyping.',
							ved_bedommelsen: 'Der lægges vægt på elevens interesse og tilgang til at eksperimentere med prototyping.'
						}
					},
					{
						titel: 'Implementering',
						lektioner: 108,
						læringsmål: [
							'Arbejde i team og efter SCRUM.',
							'Implementere softwareprojekt ud fra kravspecifikation.',
							'Implementere client/server system og sikkerhed (login/kryptering).',
							'Værdsætte et godt samarbejde.'
						],
						indholdselementer: {
							teoretisk_del: ['Agile metoder', 'Scrum', 'Serverside programmering', 'Softwaretest og sikkerhed'],
							praktisk_del: ['Gruppen implementerer projektet efter Scrum', 'Der afholdes daglige teammøder med underviser']
						},
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejdet i gruppen.',
							ved_bedommelsen: 'Der lægges vægt på samarbejdet, processen i Scrum og selve implementeringen.'
						}
					},
					{
						titel: 'Valgfrit speciale (Core Applications + User Interface Design)',
						lektioner: 108,
						læringsmål: [
							'Arbejde med trelagsmodellen og udskifte et lag baseret på eksisterende produkt.',
							'Argumentere for valg truffet undervejs i udviklingen.',
							'Gennemføre test, der dokumenterer implementerede User Stories.'
						],
						indholdselementer: { teoretisk_del: ['Trelagsmodellen'], praktisk_del: ['Udskift mobilapp med SPA GUI i eksisterende projekt'] },
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejde og præsentation.',
							ved_bedommelsen:
								'Der lægges vægt på teamwork, resultat og evnen til at forklare mangler/valg i forhold til kravspecifikationen.'
						}
					},
					{
						titel: 'Test og præsentation',
						lektioner: 36,
						læringsmål: [
							'Præsentere projektet og forklare løsningens virkemåde.',
							'Argumentere for valg truffet undervejs.',
							'Gennemføre test, der dokumenterer implementerede User Stories.'
						],
						indholdselementer: { teoretisk_del: ['Ingen teori'], praktisk_del: ['Gruppepræsentation', 'Individuel videoaflevering', 'Demonstration hvor andre kan prøve løsningen'] },
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejde og præsentation.',
							ved_bedommelsen:
								'Der lægges vægt på teamwork, resultat og refleksion over hvad der ikke er implementeret og hvorfor.'
						}
					}
				]
			}
		}
	},
	{
		id: '6',
		slug: 'h5',
		titel: 'Hovedforløb 5',
		rækkefølge: 6,
		beskrivelse: 'Linux, IoT, Big Data og mini-svendeprøve',
		indhold: {
			mål: {
				formål:
					'Hovedforløb 5 giver eleverne avancerede kompetencer inden for Linux-administration, embedded systemer, IoT og Big Data – og træner dem i at samle teknologierne i en helhedsorienteret udviklingsopgave.',
				efter_forløbet: [
					'Opsætte, konfigurere og sikre Linux-baserede servere og embedded enheder.',
					'Udvikle og implementere IoT-løsninger med dataindsamling, overførsel og behandling.',
					'Etablere en Big Data-pipeline til lagring, analyse og visualisering af data.',
					'Integrere teknologierne i et samlet projekt med kravspecifikation, dokumentation og præsentation.',
					'Arbejde selvstændigt og i teams med projektstyring og procesrefleksion.'
				]
			},
			indhold_undervisning: {
				tekst:
					'Undervisningen kombinerer teori, praksis og projektarbejde i tre teknologispor – efterfulgt af et mini-svendeprøveprojekt. Fokus er på drift, integration og dataflow i et realistisk setup.',
				temaer: [
					'Linux på server og embedded enheder: opsætning, brugerstyring, netværk, sikkerhed, containerisering (Docker).',
					'Embedded & IoT: Arduino/BeagleBone Black, sensorintegration, dataoverførsel og sikkerhed.',
					'Big Data: pipelines med Kafka/RabbitMQ, lagring i SQL/NoSQL (fx timescaleDB) og visualisering (Dash/Grafana m.fl.).',
					'Mini-svendeprøveprojekt: 4-ugers gruppeprojekt med krav, dokumentation og præsentation.'
				]
			},
			helhedsorientering:
				'H5 er bygget op omkring et integreret projektforløb, hvor Linux, IoT og Big Data bidrager direkte til en samlet løsning. Projektet forbereder eleverne på svendeprøvens krav til teknisk dybde, dokumentation og samarbejde.',
			praksisrelation:
				'Øvelser og projekter tager udgangspunkt i brancherelevante teknologier og metoder i moderne IT-infrastruktur og softwareudvikling: datacenter-miljøer, embedded hardware, cloud-arbejdsgange, datasikkerhed og visualisering.',
			tværfaglighed:
				'Forløbet binder systemadministration, softwareudvikling, data engineering, projektstyring og IT-sikkerhed sammen på tværs af delområder.',
			differentiering:
				'Undervisningen tilpasses forudsætninger: erfarne elever kan arbejde med avanceret hardware-/streaming-/analysefunktionalitet, mens andre kan fokusere på et stabilt minimum og udbygge gradvist.',
			evaluering: {
				læringsaktiviteter: [
					{
						titel: 'Prototyping',
						lektioner: 72,
						læringsmål: [
							'Kendskab til udviklingsværktøj og design af brugerinterfaces i mobile applikationer.',
							'Kendskab til sensorer på mobiltelefoner.',
							'Kendskab til klient/server-kommunikation.',
							'Kendskab til sikring af applikationer mod uautoriseret brug.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Udviklingsværktøj til mobile apps',
								'Brugerinterface til mobiltelefon',
								'Kommunikation mellem klientapplikation og server',
								'Sikring af applikationer'
							],
							praktisk_del: [
								'Løbende prototyping-opgaver',
								'Erfaringsudveksling elever imellem'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback: 'Løbende feedback på motivation og nysgerrighed i prototyping.',
							ved_bedommelsen: 'Der lægges vægt på elevens interesse og tilgang til at eksperimentere.'
						}
					},
					{
						titel: 'Objektorienteret analyse og design',
						lektioner: 36,
						læringsmål: [
							'Skrive kravspecifikation til softwareprojekt og bruge den i dialog med kunde/rekvirent.',
							'Udarbejde test-specifikation, der sikrer at krav kan testes.'
						],
						indholdselementer: {
							teoretisk_del: [
								'Objektorienteret analyse',
								'Domænemodel',
								'Use case model (diagram + tekst)',
								'System Sequence Diagram',
								'Accepttest',
								'Objektorienteret design',
								'Sekvensdiagram',
								'Klassediagram',
								'GRASP & design patterns'
							],
							praktisk_del: [
								'Gruppearbejde: kravspecifikation + test-specifikation for projektet'
							]
						},
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejde i gruppen.',
							ved_bedommelsen:
								'Der lægges vægt på entydig og præcis kravspecifikation samt evnen til skriftlig formulering.'
						}
					},
					{
						titel: 'Implementering',
						lektioner: 108,
						læringsmål: [
							'Arbejde i team og efter SCRUM.',
							'Implementere softwareprojekt ud fra kravspecifikation.',
							'Implementere client/server system med fokus på sikkerhed (login/kryptering).',
							'Værdsætte et godt holdarbejde.'
						],
						indholdselementer: {
							teoretisk_del: ['Agile metoder', 'Scrum', 'Serverside programmering', 'Softwaretest og sikkerhed'],
							praktisk_del: ['Gruppen implementerer projektet efter Scrum', 'Der afholdes daglige teammøder med underviser']
						},
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejdet i gruppen.',
							ved_bedommelsen: 'Der lægges vægt på samarbejde og proces i Scrum samt den samlede implementering.'
						}
					},
					{
						titel: 'Test og præsentation',
						lektioner: 36,
						læringsmål: [
							'Præsentere projektet og forklare løsningens virkemåde.',
							'Argumentere for valg truffet undervejs.',
							'Gennemføre accepttest og dokumentere opfyldte/ikke opfyldte krav.'
						],
						indholdselementer: {
							teoretisk_del: ['Ingen teori'],
							praktisk_del: ['Gruppepræsentation', 'Individuel videodemonstration', 'Accepttest og afprøvning']
						},
						feedback_bedommelse: {
							undervisere_feedback: 'Der gives feedback på samarbejde og præsentation.',
							ved_bedommelsen:
								'Der lægges vægt på teamwork, resultatet og refleksion over hvad der ikke er implementeret og hvorfor.'
						}
					}
				]
			}
		}
	},
	{
		id: '7',
		slug: 'h6',
		titel: 'Hovedforløb 6',
		rækkefølge: 7,
		beskrivelse: 'Svendeprøve – projekt og mundtlig fremlæggelse',
		indhold: {
			svendeprøve: {
				ugeoverskrift: 'Svendeprøveforløbet er på i alt fem uger:',
				uger: [
					{ uge: 1, label: 'Projekt' },
					{ uge: 2, label: 'Projekt' },
					{ uge: 3, label: 'Projekt' },
					{ uge: 4, label: 'Projekt' },
					{ uge: 5, label: 'Svendeprøve' }
				],
				delprøver: [
					'Projektopgavens løsning samt tilhørende dokumentation',
					'En individuel mundtlig fremlæggelse'
				],
				dokumentation: [
					{
						titel: 'Produktrapport',
						beskrivelse: 'Kravspecifikation, vejledning, produktdokumentation og bilag.'
					},
					{
						titel: 'Procesrapport',
						beskrivelse: 'Problemformulering, projektplanlægning, logbog og konklusion.'
					}
				],
				lovgivning:
					'Lovgivningen vedrørende svendeprøver er beskrevet i bekendtgørelser og skal overholdes. Vejledningen er udarbejdet i henhold til bl.a. bekendtgørelser 372 af 09/03/2021 §6 stk. 3, 528 af 27/04/2020 §6 stk. 3 og 397 af 09/04/2019 §6 stk. 3.',
				vejledning:
					'Under svendeprøveforløbet tilknyttes en vejleder, som vejleder og faciliterer elevernes arbejde. Projektopgaven løses individuelt eller i grupper på op til 4 elever og afsluttes med aflevering af dokumentation samt individuel mundtlig prøve (40 min inkl. votering). Begge delprøver skal bestås (min. 02), og den endelige karakter er gennemsnittet.'
			}
		}
	}
];
