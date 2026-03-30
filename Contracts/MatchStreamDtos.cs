namespace LiveMatchApi.Contracts;

public sealed record ScoreUpdateDto(
    long MatchId,
    int HomeScore,
    int AwayScore,
    int? Minute,
    string Status);

public sealed record GoalDto(
    long MatchId,
    string Team,
    int? Minute,
    int HomeScore,
    int AwayScore,
    string? Scorer = null,
    string? AssistBy = null);

public sealed record MatchEndDto(
    long MatchId,
    int HomeScore,
    int AwayScore,
    int? Minute,
    string Status,
    string? Winner);

