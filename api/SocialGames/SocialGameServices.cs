using System.Threading.RateLimiting;
using Mercantec.Tech.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.SocialGames;

public static class SocialGameServices
{
    public const string JoinPolicy = "social-game-join";

    public static IServiceCollection AddSocialGames(this IServiceCollection services)
    {
        services.AddScoped<SocialRoomStore>();
        services.AddHostedService<SocialRoomClock>();
        return services.AddRateLimiter(options => options.AddPolicy(JoinPolicy, context =>
            RateLimitPartition.GetSlidingWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new SlidingWindowRateLimiterOptions { PermitLimit = 150, Window = TimeSpan.FromSeconds(30), SegmentsPerWindow = 6 })));
    }
}

internal sealed class SocialRoomClock(IServiceScopeFactory scopes, ILogger<SocialRoomClock> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        var nextCleanup = DateTime.MinValue;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SocialRoomStore>().RunDeadlines(stoppingToken);
                var now = DateTime.UtcNow;
                if (now >= nextCleanup)
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.SocialGameRooms.Where(room => room.ExpiresAtUtc <= now).ExecuteDeleteAsync(stoppingToken);
                    nextCleanup = now.AddHours(1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Kunne ikke behandle tidsfrister i sociale spil."); }
        }
    }
}
