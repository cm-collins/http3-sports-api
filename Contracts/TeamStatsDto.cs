namespace LiveMatchApi.Contracts;

public sealed record TeamStatsDto(
    TeamSummaryDto Team,
    IReadOnlyList<StatLineDto> Statistics);

