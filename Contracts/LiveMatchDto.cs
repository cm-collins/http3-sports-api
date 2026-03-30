using LiveMatchApi.Models;

namespace LiveMatchApi.Contracts;

public sealed record LiveMatchDto(
    Guid Id,
    string League,
    string HomeTeam,
    string AwayTeam,
    int HomeScore,
    int AwayScore,
    int Minute,
    string Status,
    string Venue,
    DateTimeOffset KickoffUtc)
{
    public static LiveMatchDto FromModel(LiveMatch match) => new(
        match.Id,
        match.League,
        match.HomeTeam,
        match.AwayTeam,
        match.HomeScore,
        match.AwayScore,
        match.Minute,
        match.Status,
        match.Venue,
        match.KickoffUtc);
}

