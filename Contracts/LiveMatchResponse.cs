namespace LiveMatchApi.Contracts;

public sealed record LiveMatchResponse(
    LiveMatchDto Match,
    ApiMeta Meta);

