using System.Diagnostics;
using LiveMatchApi.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace LiveMatchApi.Endpoints;

public static class BenchmarkEndpoints
{
    private const int MaxPayloadKb = 2048; // 2 MB cap to prevent abuse
    private const string PayloadCacheKeyPrefix = "bench:payload:";

    public static IEndpointRouteBuilder MapBenchmarkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/benchmark");
        group.RequireRateLimiting("benchmark");

        group.MapGet("/ping", Ping);
        group.MapGet("/payload/{kb:int}", Payload);
        group.MapGet("/panel/{name}", Panel);
        group.MapGet("/stream", Stream);

        return endpoints;
    }

    private static IResult Ping(HttpContext httpContext, IApiMetaFactory metaFactory)
    {
        var sw = Stopwatch.StartNew();

        var response = new
        {
            status = "ok",
            serverElapsedMs = (int)sw.Elapsed.TotalMilliseconds,
            meta = metaFactory.Create(httpContext, source: "benchmark")
        };

        httpContext.Response.Headers["Server-Timing"] = $"app;dur={sw.Elapsed.TotalMilliseconds:0.0}";
        return Results.Ok(response);
    }

    private static IResult Payload(
        int kb,
        HttpContext httpContext,
        IMemoryCache cache,
        ProtocolStatus protocolStatus)
    {
        var sw = Stopwatch.StartNew();

        if (kb < 0 || kb > MaxPayloadKb)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid payload size",
                detail: $"kb must be between 0 and {MaxPayloadKb}.");
        }

        var cacheKey = $"{PayloadCacheKeyPrefix}{kb}";
        if (!cache.TryGetValue(cacheKey, out byte[]? bytes) || bytes is null)
        {
            bytes = new byte[kb * 1024];
            cache.Set(cacheKey, bytes, TimeSpan.FromMinutes(5));
            httpContext.Response.Headers["X-Cache"] = "miss";
        }
        else
        {
            httpContext.Response.Headers["X-Cache"] = "hit";
        }

        httpContext.Response.Headers["X-Payload-Kb"] = kb.ToString();
        httpContext.Response.Headers["X-Protocol"] = httpContext.Request.Protocol;
        httpContext.Response.Headers["X-Quic-Supported"] = protocolStatus.QuicSupported.ToString();
        httpContext.Response.Headers["X-Http3-Enabled"] = protocolStatus.Http3Enabled.ToString();
        httpContext.Response.Headers["Server-Timing"] = $"app;dur={sw.Elapsed.TotalMilliseconds:0.0}";
        httpContext.Response.ContentType = "application/octet-stream";
        return Results.Bytes(bytes);
    }

    private static async Task<IResult> Panel(
        string name,
        int? delayMs,
        HttpContext httpContext,
        IApiMetaFactory metaFactory,
        CancellationToken cancellationToken)
    {
        var delay = Math.Clamp(delayMs ?? 0, 0, 30_000);

        var sw = Stopwatch.StartNew();
        if (delay > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }

        httpContext.Response.Headers["Server-Timing"] = $"app;dur={sw.Elapsed.TotalMilliseconds:0.0}";

        return Results.Ok(new
        {
            status = "ok",
            panel = name,
            requestedDelayMs = delay,
            serverElapsedMs = (int)sw.Elapsed.TotalMilliseconds,
            meta = metaFactory.Create(httpContext, source: "benchmark")
        });
    }

    private static async Task<IResult> Stream(
        int? intervalMs,
        HttpContext httpContext,
        IApiMetaFactory metaFactory,
        CancellationToken cancellationToken)
    {
        var interval = Math.Clamp(intervalMs ?? 1000, 250, 10_000);

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await httpContext.Response.StartAsync(cancellationToken);

        await SseWriter.WriteRetryAsync(httpContext.Response, retryMs: 2000, cancellationToken);
        await SseWriter.WriteEventAsync(
            httpContext.Response,
            id: 0,
            eventName: "meta",
            data: metaFactory.Create(httpContext, source: "benchmark"),
            cancellationToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));
        var id = 0L;

        while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
        {
            id++;
            await SseWriter.WriteEventAsync(
                httpContext.Response,
                id: id,
                eventName: "tick",
                data: new { id, utcTime = DateTimeOffset.UtcNow },
                cancellationToken);
        }

        return Results.Empty;
    }
}
