using System.Threading.RateLimiting;
using Mercantec.Tech.Api.Controllers;

namespace Mercantec.Tech.Api.KnowledgeChecks;

public static class LiveQuizServices
{
    public static IServiceCollection AddLiveQuiz(this IServiceCollection services)
    {
        services.AddHostedService<LiveQuizCleanup>();
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LiveKnowledgeChecksController.JoinRatePolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        // Allow a class to arrive together, including devices behind a shared internet connection.
                        PermitLimit = 150,
                        Window = TimeSpan.FromSeconds(30),
                        SegmentsPerWindow = 6
                    }));
        });
    }
}
