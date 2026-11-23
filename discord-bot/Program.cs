using System.Globalization;
using Discord;
using Discord.WebSocket;
using Lup.DiscordBot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN")?.Trim();
if (string.IsNullOrEmpty(token))
{
    Console.Error.WriteLine("Angiv DISCORD_BOT_TOKEN for at starte LUP-botten.");
    return 1;
}

ulong? guildId = null;
var guildSetting = Environment.GetEnvironmentVariable("DISCORD_GUILD_ID")?.Trim();
if (!string.IsNullOrEmpty(guildSetting))
{
    if (!ulong.TryParse(guildSetting, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0)
    {
        Console.Error.WriteLine("DISCORD_GUILD_ID skal være et gyldigt Discord-server-id.");
        return 1;
    }
    guildId = id;
}

var originSetting = Environment.GetEnvironmentVariable("LUP_PUBLIC_ORIGIN") ?? "https://lup.dk";
if (!Uri.TryCreate(originSetting, UriKind.Absolute, out var origin) ||
    origin.Scheme != Uri.UriSchemeHttps || origin.Host.Length == 0 || origin.UserInfo.Length > 0 ||
    origin.AbsolutePath != "/" || origin.Query.Length > 0 || origin.Fragment.Length > 0)
{
    Console.Error.WriteLine("LUP_PUBLIC_ORIGIN skal være en HTTPS-origin uden login, sti, query eller fragment.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    UseCookies = false,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
})
{
    BaseAddress = origin,
    Timeout = TimeSpan.FromSeconds(8)
});
builder.Services.AddSingleton<LupCommands>();
builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig { GatewayIntents = GatewayIntents.Guilds }));
builder.Services.AddHostedService(services => new DiscordWorker(
    services.GetRequiredService<DiscordSocketClient>(), services.GetRequiredService<LupCommands>(),
    services.GetRequiredService<ILogger<DiscordWorker>>(), services.GetRequiredService<IHostApplicationLifetime>(), token, guildId));
await builder.Build().RunAsync();
return Environment.ExitCode;
