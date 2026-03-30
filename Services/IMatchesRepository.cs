using LiveMatchApi.Models;

namespace LiveMatchApi.Services;

public interface IMatchesRepository
{
    Task<IReadOnlyList<LiveMatch>> GetLiveAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LiveMatch>> GetUpcomingAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
    Task<LiveMatch?> GetByFixtureIdAsync(long fixtureId, CancellationToken cancellationToken);
}

