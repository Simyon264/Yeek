using System.Globalization;
using System.Threading.RateLimiting;

namespace Yeek.Core;

public static class RateLimitHostingExtensions
{
    public static void AddRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.OnRejected = (context, cancellationToken) =>
            {

                var path = context.HttpContext.Request.Path;
                var key = context.Lease.MetadataNames.FirstOrDefault() ?? "unknown";

                var username = context.HttpContext.User?.Identity?.IsAuthenticated == true
                    ? context.HttpContext.User.Identity!.Name
                    : "anonymous";
                var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int) retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.RequestServices.GetService<ILoggerFactory>()?
                    .CreateLogger("Microsoft.AspNetCore.RateLimitingMiddleware")
                    .LogWarning(
                        "Rate limit triggered on {Path}. Username={Username}, IP={IP}, Policy={PolicyType}, LeaseMetadata={Metadata}",
                        path,
                        username,
                        ip,
                        context.Lease?.GetType().Name ?? "unknown",
                        key
                    );

                return new ValueTask();
            };


            options.AddPolicy("VotePolicy", httpContext =>
            {
                var key = httpContext.User?.Identity?.Name
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anonymous";

                return RateLimitPartition.GetTokenBucketLimiter(
                    key,
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 5,
                        TokensPerPeriod = 5,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        AutoReplenishment = true,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            options.AddPolicy("UploadPolicy", httpContext =>
            {
                var key = httpContext.User?.Identity?.Name
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(3),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy("DownloadPolicy", httpContext =>
            {
                var key = httpContext.User?.Identity?.Name
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anonymous";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    key,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 50,
                        Window = TimeSpan.FromMinutes(10),
                        SegmentsPerWindow = 4,
                        AutoReplenishment = true,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });
        });
    }
}