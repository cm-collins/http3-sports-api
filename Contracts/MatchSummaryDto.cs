using LiveMatchApi.Models;

namespace LiveMatchApi.Contracts;

public sealed record MatchSummaryDto(
    long MatchId,
    int LeagueId,
    string League,
    TeamSummaryDto HomeTeam,
    TeamSummaryDto AwayTeam,
    int HomeScore,
    int AwayScore,
    int? Minute,
    string Status,
    string? Venue,
    DateTimeOffset KickoffUtc)
{
    public static MatchSummaryDto FromModel(LiveMatch match) => new(
        MatchId: match.FixtureId,
        LeagueId: match.LeagueId,
        League: match.League,
        HomeTeam: new TeamSummaryDto(match.HomeTeamId, match.HomeTeam, match.HomeTeamLogoUrl),
        AwayTeam: new TeamSummaryDto(match.AwayTeamId, match.AwayTeam, match.AwayTeamLogoUrl),
        HomeScore: match.HomeScore,
        AwayScore: match.AwayScore,
        Minute: match.Minute,
        Status: match.Status,
        Venue: match.Venue,
        KickoffUtc: match.KickoffUtc);
}

