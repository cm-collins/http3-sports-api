using LiveMatchApi.Contracts;

namespace LiveMatchApi.Services;

public interface IMatchStatsService
{
    Task<MatchStatsResponse> GetStatsAsync(long fixtureId, HttpContext httpContext, CancellationToken cancellationToken);
}

