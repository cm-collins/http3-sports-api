namespace LiveMatchApi.Models;

public sealed record LiveMatch(
    Guid Id,
    string League,
    string HomeTeam,
    string AwayTeam,
    int HomeScore,
    int AwayScore,
    int Minute,
    string Status,
    string Venue,
    DateTimeOffset KickoffUtc);

