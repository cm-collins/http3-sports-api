namespace LiveMatchApi.Contracts;

public sealed record MatchListResponse(
    IReadOnlyList<MatchSummaryDto> Matches,
    ApiMeta Meta);

