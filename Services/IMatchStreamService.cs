using LiveMatchApi.Contracts;

namespace LiveMatchApi.Services;

public interface IMatchStreamService
{
    IAsyncEnumerable<MatchStreamMessage> StreamAsync(long fixtureId, CancellationToken cancellationToken);
}

