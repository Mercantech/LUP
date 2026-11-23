using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lup.DiscordBot;

internal sealed class DiscordWorker(DiscordSocketClient client, LupCommands commands, ILogger<DiscordWorker> logger,
    IHostApplicationLifetime lifetime, string token, ulong? guildId) : BackgroundService
{
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HashSet<Task> pending = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task OnReady()
        {
            ready.TrySetResult();
            return Task.CompletedTask;
        }

        Task OnCommand(SocketSlashCommand command)
        {
            if (command.Data.Name == "lup" && !stoppingToken.IsCancellationRequested)
            {
                // Discord.Net processes gateway events in sequence. Each reply can await LUP independently.
                var reply = HandleCommandAsync(command, stoppingToken);
                lock (pending) pending.Add(reply);
                _ = RemoveCompletedAsync(reply);
            }
            return Task.CompletedTask;
        }

        client.Ready += OnReady;
        client.SlashCommandExecuted += OnCommand;
        try
        {
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            await ready.Task.WaitAsync(TimeSpan.FromMinutes(2), stoppingToken);
            await RegisterCommandAsync(stoppingToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError("Kunne ikke starte Discord-botten ({Fejl}). Kontrollér bot-token og forbindelse.", ex.GetType().Name);
            Environment.ExitCode = 1;
            lifetime.StopApplication();
        }
        finally
        {
            client.SlashCommandExecuted -= OnCommand;
            client.Ready -= OnReady;
            await client.StopAsync();
            Task[] outstanding;
            lock (pending) outstanding = pending.ToArray();
            await Task.WhenAll(outstanding);
        }
    }

    private async Task RemoveCompletedAsync(Task reply)
    {
        await reply;
        lock (pending) pending.Remove(reply);
    }

    private static SlashCommandProperties BuildCommand(bool global)
    {
        var definition = new SlashCommandBuilder().WithName("lup").WithDescription("Find undervisning og spil på LUP")
            .AddOption(new SlashCommandOptionBuilder().WithName("søg").WithDescription("Søg efter undervisning på LUP")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("emne", ApplicationCommandOptionType.String, "Hvad leder du efter?", isRequired: true, minLength: 1, maxLength: 200))
            .AddOption(new SlashCommandOptionBuilder().WithName("spil").WithDescription("Åbn et LUP-spil med holdets kode")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("kode", ApplicationCommandOptionType.String, "Spillets seks cifre", isRequired: true, minLength: 6, maxLength: 6))
            .AddOption("taljagt", ApplicationCommandOptionType.SubCommand, "Se dagens udfordring i Taljagt")
            .AddOption("hjælp", ApplicationCommandOptionType.SubCommand, "Se bottens kommandoer");

        if (global)
            definition.WithContextTypes(InteractionContextType.Guild).WithIntegrationTypes(ApplicationIntegrationType.GuildInstall);
        return definition.Build();
    }

    private async Task RegisterCommandAsync(CancellationToken cancellationToken)
    {
        var command = BuildCommand(global: guildId is null);
        var options = new RequestOptions { CancelToken = cancellationToken };
        while (true)
        {
            try
            {
                if (guildId is ulong id)
                    await client.Rest.CreateGuildCommand(command, id, options);
                else
                    await client.Rest.CreateGlobalCommand(command, options);
                logger.LogInformation("LUP-botten er forbundet, og /lup er registreret.");
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Kunne ikke registrere /lup ({Fejl}). Prøver igen om et minut.", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }
    }

    private async Task HandleCommandAsync(SocketSlashCommand interaction, CancellationToken cancellationToken)
    {
        var options = new RequestOptions { CancelToken = cancellationToken };
        try
        {
            await interaction.DeferAsync(ephemeral: true, options: options);
            string reply;
            try
            {
                var subcommand = interaction.Data.Options.FirstOrDefault();
                var argument = subcommand?.Options.FirstOrDefault()?.Value as string;
                reply = guildId.HasValue && guildId != interaction.GuildId
                    ? "LUP-botten er konfigureret til en anden Discord-server."
                    : await commands.ReplyAsync(subcommand?.Name ?? "hjælp", argument, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("LUP kunne ikke besvare en kommando ({Fejl}).", ex.GetType().Name);
                reply = "LUP kunne ikke besvare kommandoen lige nu. Prøv igen om lidt.";
            }
            await interaction.ModifyOriginalResponseAsync(message =>
            {
                message.Content = reply;
                message.AllowedMentions = AllowedMentions.None;
            }, options);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogWarning("Kunne ikke sende svar til Discord ({Fejl}).", ex.GetType().Name);
        }
    }
}
