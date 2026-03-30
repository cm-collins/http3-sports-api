namespace LiveMatchApi.Models;

public sealed record LiveMatch(
    long FixtureId,
    int LeagueId,
    string League,
    int HomeTeamId,
    string HomeTeam,
    string? HomeTeamLogoUrl,
    int AwayTeamId,
    string AwayTeam,
    string? AwayTeamLogoUrl,
    int HomeScore,
    int AwayScore,
    int? Minute,
    string Status,
    string? Venue,
    DateTimeOffset KickoffUtc);
