using LiveMatchApi.Contracts;
using LiveMatchApi.Infrastructure;
using LiveMatchApi.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LiveMatchApi.Endpoints;

public static class MatchesEndpoints
{
    public static IEndpointRouteBuilder MapMatchesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapMatchesGroup(endpoints.MapGroup("/api/matches"));

        // Compatibility alias for earlier phases. Phase 1 source of truth is `/api/matches/*`.
        MapLiveMatchesAlias(endpoints.MapGroup("/api/live-matches"));

        return endpoints;
    }

    private static void MapMatchesGroup(RouteGroupBuilder group)
    {
        group.RequireRateLimiting("api");

        group.AddEndpointFilter((context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<ApiFootballOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? ValueTask.FromResult<object?>(ApiProblems.ProviderNotConfigured())
                : next(context);
        });

        group.MapGet("/live", GetLive);
        group.MapGet("/upcoming", GetUpcoming);
        group.MapGet("/{fixtureId:long}", GetByFixtureId);

        group.MapGet("/", GetLive);
    }

    private static void MapLiveMatchesAlias(RouteGroupBuilder group)
    {
        group.RequireRateLimiting("api");

        group.AddEndpointFilter((context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<ApiFootballOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? ValueTask.FromResult<object?>(ApiProblems.ProviderNotConfigured())
                : next(context);
        });

        group.MapGet("/", GetLive);
        group.MapGet("/{fixtureId:long}", GetByFixtureId);
    }

    private static async Task<IResult> GetLive(
        HttpContext httpContext,
        IMatchesRepository repository,
        IApiMetaFactory metaFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Matches");

        return await ExecuteProviderCall(async () =>
        {
            var matches = await repository.GetLiveAsync(cancellationToken);

            return Results.Ok(new MatchListResponse(
                Matches: matches.Select(MatchSummaryDto.FromModel).ToArray(),
                Meta: metaFactory.Create(httpContext, source: "api-football")));
        }, logger, cancellationToken);
    }

    private static async Task<IResult> GetUpcoming(
        HttpContext httpContext,
        IMatchesRepository repository,
        IApiMetaFactory metaFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Matches");

        var fromUtc = DateTimeOffset.UtcNow;
        var toUtc = fromUtc.AddHours(24);

        return await ExecuteProviderCall(async () =>
        {
            var matches = await repository.GetUpcomingAsync(fromUtc, toUtc, cancellationToken);

            return Results.Ok(new MatchListResponse(
                Matches: matches.Select(MatchSummaryDto.FromModel).ToArray(),
                Meta: metaFactory.Create(httpContext, source: "api-football")));
        }, logger, cancellationToken);
    }

    private static async Task<IResult> GetByFixtureId(
        long fixtureId,
        HttpContext httpContext,
        IMatchesRepository repository,
        IApiMetaFactory metaFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Matches");

        return await ExecuteProviderCall(async () =>
        {
            var match = await repository.GetByFixtureIdAsync(fixtureId, cancellationToken);
            if (match is null)
            {
                return ApiProblems.MatchNotFound(fixtureId);
            }

            return Results.Ok(new MatchResponse(
                Match: MatchSummaryDto.FromModel(match),
                Meta: metaFactory.Create(httpContext, source: "api-football")));
        }, logger, cancellationToken);
    }

    private static async Task<IResult> ExecuteProviderCall(
        Func<Task<IResult>> action,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Upstream provider request failed.");
            return ApiProblems.UpstreamUnavailable();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Upstream provider request timed out.");
            return ApiProblems.UpstreamUnavailable();
        }
    }
}
