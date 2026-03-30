using LiveMatchApi.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace LiveMatchApi.Endpoints;

public static class HighlightsEndpoints
{
    public static IEndpointRouteBuilder MapHighlightsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/highlights");
        group.RequireRateLimiting("api");

        group.MapGet("/feed", GetFeed);
        group.MapGet("/{team}", GetByTeam);

        return endpoints;
    }

    private static async Task<IResult> GetFeed(
        HttpContext httpContext,
        IHighlightsService highlightsService,
        CancellationToken cancellationToken)
    {
        var response = await highlightsService.GetFeedAsync(httpContext, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetByTeam(
        string team,
        HttpContext httpContext,
        IHighlightsService highlightsService,
        CancellationToken cancellationToken)
    {
        var response = await highlightsService.GetTeamAsync(team, httpContext, cancellationToken);
        return Results.Ok(response);
    }
}

