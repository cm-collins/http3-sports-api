namespace LiveMatchApi.Contracts;

public sealed record MatchStatsResponse(
    long MatchId,
    string Status,
    IReadOnlyList<TeamStatsDto> Teams,
    ApiMeta Meta,
    string? Warning = null);
