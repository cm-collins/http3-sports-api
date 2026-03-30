using LiveMatchApi.Models;

namespace LiveMatchApi.Services;

public interface ILiveMatchRepository
{
    Task<IReadOnlyList<LiveMatch>> GetAllAsync(CancellationToken cancellationToken);
    Task<LiveMatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
