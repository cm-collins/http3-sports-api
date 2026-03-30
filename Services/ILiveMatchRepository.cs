using LiveMatchApi.Models;

namespace LiveMatchApi.Services;

public interface ILiveMatchRepository
{
    IReadOnlyList<LiveMatch> GetAll();
    LiveMatch? GetById(Guid id);
}

