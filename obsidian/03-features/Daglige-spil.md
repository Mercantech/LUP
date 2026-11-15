---
type: feature
status: active
tags:
  - lup
  - spil
---

# Daglige spil

Find **Dagens Wordle**, **Dagens ord** og **Taljagt** under [Spil](https://lup.dk/games). Du kan se dagens udfordring uden login. Log ind for at spille en officiel runde og gemme dine resultater.

## Dagens ord

Gæt fagbegrebet ud fra dagens karakters ledetråd. Bogstavfelterne viser, hvor langt ordet er. Du har tre gæt på hele ordet og kan åbne to ekstra ledetråde, der også afslører bogstaver. Når runden er færdig, kan du læse ordets betydning og et eksempel på, hvordan det bruges.

## Taljagt

Bilbo Backend og Kamæleonen skiftes til at være vært. Kombinér mindst to af de seks talbrikker med plus, minus, gange, division og parenteser. Hver brik kan bruges én gang, også når to brikker viser samme tal. Alle mellemresultater skal være hele tal på mindst nul. Du kan prøve regnestykker, inden du afslutter med ét officielt svar. Rammer du målet, får du 100 point. Ellers falder pointene med afstanden til målet. Efter runden vises en mulig løsning.

Hvert spil har sin egen streak. Sejr og nederlag tæller, når du gennemfører dagens runde. Ved midnat dansk tid åbner en ny udfordring. Resultaterne følger din LUP-konto.

## Historik og ranglister

Åbn **Historik og ranglister** fra Spil eller et dagligt spil. Din private historik viser afsluttede runder, point, sejre og streaks. Den viser også antal forsøg, brugte ledetråde eller afstand til målet, afhængigt af spillet.

Ranglisterne samler point for hver dag, uge eller måned efter dansk tid. Ugen starter mandag. Vælg alle frivillige deltagere i LUP eller et hold, du har adgang til. Spillere med lige mange point deler placeringen.

Du vælger selv, om dit LUP-visningsnavn og dine point må vises på ranglisterne. Samtykket gælder alle daglige spil, både på dit hold og fælles i LUP, og kan til enhver tid slås fra. Din private historik er tilgængelig uanset dit valg.

Wordle giver 60 point ved første rigtige gæt og 10 færre pr. ekstra forsøg. Dagens ord giver 60 ved første rigtige gæt, 20 færre pr. ekstra gæt og 5 færre pr. åbnet ledetråd. En runde uden løsning giver 0 point i begge ordspil. Taljagt trækker afstanden til måltallet fra 100 point med 0 som minimum.

## Tilføj et dagligt spil

Registrér spillets nøgle, titel og pointregler i `DailyGameProgressCatalog`. Giv den fælles oversigt adgang til spillets afsluttede runder som dato, resultat og point samt relevante detaljer. Bevar spillerens handlinger og facit i spillets egen logik og lad den fælles oversigt stå for historik, perioder, streaks, holdadgang og rangering.

Se også [Dagens Wordle](./Dagens-Wordle.md) og [issue #96](https://github.com/Mercantech/Mercantec.tech/issues/96).
