namespace LiveMatchApi.Contracts;

public sealed record LiveMatchListResponse(
    IReadOnlyList<LiveMatchDto> Matches,
    ApiMeta Meta);

