using LiveMatchApi.Contracts;

namespace LiveMatchApi.Services;

public interface IHighlightsService
{
    Task<HighlightsResponse> GetFeedAsync(HttpContext httpContext, CancellationToken cancellationToken);
    Task<HighlightsResponse> GetTeamAsync(string team, HttpContext httpContext, CancellationToken cancellationToken);
}

