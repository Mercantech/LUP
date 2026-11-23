FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY discord-bot/Lup.DiscordBot.csproj discord-bot/
RUN dotnet restore discord-bot/Lup.DiscordBot.csproj
COPY shared/DailyGames.cs shared/LiveKnowledgeChecks.cs shared/
COPY discord-bot/ discord-bot/
RUN dotnet publish discord-bot/Lup.DiscordBot.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Lup.DiscordBot.dll"]
