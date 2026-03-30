using LiveMatchApi.Contracts;
using LiveMatchApi.Infrastructure;
using LiveMatchApi.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LiveMatchApi.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/match");
        group.RequireRateLimiting("api");

        // Match detail endpoints use API-Football today.
        group.AddEndpointFilter((context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<ApiFootballOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? ValueTask.FromResult<object?>(ApiProblems.ProviderNotConfigured())
                : next(context);
        });

        group.MapGet("/{fixtureId:long}/stats", GetStats);
        group.MapGet("/{fixtureId:long}/stream", StreamMatch);
        group.MapGet("/{fixtureId:long}/score-stream", StreamScore);
        group.MapGet("/{fixtureId:long}/commentary-stream", StreamCommentary);

        return endpoints;
    }

    private static async Task<IResult> GetStats(
        long fixtureId,
        HttpContext httpContext,
        IMatchStatsService statsService,
        IApiMetaFactory metaFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("MatchStats");

        try
        {
            var response = await statsService.GetStatsAsync(fixtureId, httpContext, cancellationToken);
            return Results.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Upstream provider request failed for fixture {FixtureId}.", fixtureId);
            return Results.Ok(new MatchStatsResponse(
                MatchId: fixtureId,
                Status: "degraded",
                Teams: Array.Empty<TeamStatsDto>(),
                Meta: metaFactory.Create(httpContext, source: "api-football"),
                Warning: "Stats provider unavailable."));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Upstream provider request timed out for fixture {FixtureId}.", fixtureId);
            return Results.Ok(new MatchStatsResponse(
                MatchId: fixtureId,
                Status: "degraded",
                Teams: Array.Empty<TeamStatsDto>(),
                Meta: metaFactory.Create(httpContext, source: "api-football"),
                Warning: "Stats provider timed out."));
        }
    }

    private static async Task<IResult> StreamMatch(
        long fixtureId,
        HttpContext httpContext,
        IMatchesRepository matchesRepository,
        IMatchStreamService streamService,
        IApiMetaFactory metaFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) =>
        await StreamInternal(
            fixtureId,
            httpContext,
            matchesRepository,
            streamService,
            metaFactory,
            loggerFactory,
            cancellationToken,
            includeMessage: static _ => true);

    private static Task<IResult> StreamScore(
        long fixtureId,
        HttpContext httpContext,
        IMatchesRepository matchesRepository,
        IMatchStreamService streamService,
        IApiMetaFactory metaFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) =>
        StreamInternal(
            fixtureId,
            httpContext,
            matchesRepository,
            streamService,
            metaFactory,
            loggerFactory,
            cancellationToken,
            includeMessage: static message =>
                string.Equals(message.Event, "score_update", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(message.Event, "match_end", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(message.Event, "upstream_error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(message.Event, "match_missing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(message.Event, "heartbeat", StringComparison.OrdinalIgnoreCase));

    private static Task<IResult> StreamCommentary(
        long fixtureId,
        HttpContext httpContext,
        IMatchesRepository matchesRepository,
        IMatchStreamService streamService,
        IApiMetaFactory metaFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) =>
        StreamInternal(
            fixtureId,
            httpContext,
            matchesRepository,
            streamService,
            metaFactory,
            loggerFactory,
            cancellationToken,
            includeMessage: static message =>
                string.Equals(message.Event, "goal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(message.Event, "match_end", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(message.Event, "upstream_error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(message.Event, "match_missing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(message.Event, "heartbeat", StringComparison.OrdinalIgnoreCase));

    private static async Task<IResult> StreamInternal(
        long fixtureId,
        HttpContext httpContext,
        IMatchesRepository matchesRepository,
        IMatchStreamService streamService,
        IApiMetaFactory metaFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        Func<MatchStreamMessage, bool> includeMessage)
    {
        var logger = loggerFactory.CreateLogger("MatchStream");

        // Fail fast if the match doesn't exist before switching into SSE mode.
        try
        {
            var match = await matchesRepository.GetByFixtureIdAsync(fixtureId, cancellationToken);
            if (match is null)
            {
                return ApiProblems.MatchNotFound(fixtureId);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Upstream provider request failed while starting stream for fixture {FixtureId}.", fixtureId);
            return ApiProblems.UpstreamUnavailable();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Upstream provider request timed out while starting stream for fixture {FixtureId}.", fixtureId);
            return ApiProblems.UpstreamUnavailable();
        }

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
            data: metaFactory.Create(httpContext, source: "api-football"),
            cancellationToken);

        await foreach (var message in streamService.StreamAsync(fixtureId, cancellationToken))
        {
            if (!includeMessage(message))
            {
                continue;
            }

            if (string.Equals(message.Event, "heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                await SseWriter.WriteCommentAsync(httpContext.Response, "heartbeat", cancellationToken);
                continue;
            }

            await SseWriter.WriteEventAsync(
                httpContext.Response,
                id: message.Id,
                eventName: message.Event,
                data: message.Data,
                cancellationToken);
        }

        return Results.Empty;
    }
}
