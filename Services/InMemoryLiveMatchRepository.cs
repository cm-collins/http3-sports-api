using LiveMatchApi.Models;

namespace LiveMatchApi.Services;

public sealed class InMemoryLiveMatchRepository : ILiveMatchRepository
{
    private static readonly IReadOnlyList<LiveMatch> Matches =
    [
        new(
            Guid.Parse("b1a30c84-d73e-4af8-bec2-6526f925733b"),
            "Premier League",
            "Arsenal",
            "Liverpool",
            2,
            1,
            76,
            "Live",
            "Emirates Stadium",
            DateTimeOffset.UtcNow.AddMinutes(-76)),
        new(
            Guid.Parse("60b14417-6aa3-4544-bfc9-b9fe9c8d72b7"),
            "NBA",
            "Boston Celtics",
            "Milwaukee Bucks",
            101,
            98,
            44,
            "Q4",
            "TD Garden",
            DateTimeOffset.UtcNow.AddMinutes(-96)),
        new(
            Guid.Parse("09bf36d9-8a2d-419c-b1a5-4f2d9fcb6d9c"),
            "La Liga",
            "Barcelona",
            "Atletico Madrid",
            0,
            0,
            12,
            "Live",
            "Estadi Olimpic Lluis Companys",
            DateTimeOffset.UtcNow.AddMinutes(-12))
    ];

    public IReadOnlyList<LiveMatch> GetAll() => Matches;

    public LiveMatch? GetById(Guid id) => Matches.FirstOrDefault(match => match.Id == id);
}

