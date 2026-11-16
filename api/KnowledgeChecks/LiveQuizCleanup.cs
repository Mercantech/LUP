using Mercantec.Tech.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.KnowledgeChecks;

internal sealed class LiveQuizCleanup(IServiceScopeFactory scopes, ILogger<LiveQuizCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                await db.LiveKnowledgeChecks.Where(room => room.ExpiresAtUtc <= now).ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Kunne ikke rydde udløbne livequizzer.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
