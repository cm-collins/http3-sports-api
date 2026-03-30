namespace LiveMatchApi.Contracts;

public sealed record MatchResponse(
    MatchSummaryDto Match,
    ApiMeta Meta);

