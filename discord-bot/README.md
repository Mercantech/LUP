# LUP i Discord

Elever kan bruge `/lup` i en Discord-server. Svaret vises kun til den, der bruger kommandoen.

| Prøv i Discord | Hvad sker der? |
| --- | --- |
| `/lup søg emne:C#` | Åbner LUP med søgningen udfyldt. Brugerens login på LUP afgør, hvilke resultater der er synlige. |
| `/lup spil kode:012345` | Giver et direkte link til klassens spil. Brug den sekscifrede kode fra læreren. |
| `/lup taljagt` | Henter dagens seks tal og målet fra LUP. Eleven kan regne med i Discord og aflevere sit bud på LUP. |
| `/lup hjælp` | Viser kommandoerne og åbner LUP’s oversigt. |

## Installér botten på en server

1. Opret en applikation med navnet **LUP** i [Discord Developer Portal](https://discord.com/developers/applications). Som ikon kan du bruge [LUP’s appikon](../web/wwwroot/icon-512.png).
2. Vælg **Installation → Guild Install** med scopes `bot` og `applications.commands`. Behold standardrettighederne på nul. Botten bruger Discord Gateway, så **Interactions Endpoint URL** skal være tom, og **Privileged Gateway Intents** kan være slået fra.
3. Åbn **Install Link**, vælg **Add to Server**, og vælg den Discord-server, hvor du har tilladelse til at tilføje apps.
4. Hent bot-tokenet under **Bot**, og gem det i bot-tjenestens miljøvariabel `DISCORD_BOT_TOKEN`. I Dokploy ligger tjenesten under **Mercantec.tech → production → LUP Discord**.
5. Vælg **Deploy** i Dokploy. Prøv `/lup hjælp` og derefter `/lup taljagt` i en kanal, hvor medlemmer kan bruge appkommandoer.

Tjenesten henter `docker-compose.discord.yml` fra `main` og opretter selv Discord-kommandoen ved opstart. Hver Discord-applikation skal have én kørende instans. [Discords installationsvejledning](https://docs.discord.com/developers/quick-start/getting-started)

## Lokal udvikling

Brug en separat Discord-applikation til udvikling. Sæt `DISCORD_BOT_TOKEN` og eventuelt `DISCORD_GUILD_ID` i miljøet eller i repoets lokale `.env`. Kør fra repoets rod.

```sh
docker compose -f docker-compose.discord.yml up -d --build
docker compose -f docker-compose.discord.yml logs -f discord-bot
```

| Variabel | Anvendelse |
| --- | --- |
| `DISCORD_BOT_TOKEN` | Påkrævet. Token til botten fra Discord Developer Portal. |
| `DISCORD_GUILD_ID` | Valgfri. Registrerer kommandoen på én bestemt testserver. Når feltet er tomt, registreres den globalt til serverinstallationer. |
| `LUP_PUBLIC_ORIGIN` | LUP’s HTTPS-origin til links og Taljagt. Standard er `https://lup.dk`. |

Kør kommandotestene uden Discord-token med `dotnet test tests/Lup.DiscordBot.Tests`. Svarene ligger i [LupCommands.cs](LupCommands.cs), og Discord-registreringen ligger i [DiscordWorker.cs](DiscordWorker.cs).
