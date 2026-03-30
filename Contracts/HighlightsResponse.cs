namespace LiveMatchApi.Contracts;

public sealed record HighlightsResponse(
    string Status,
    IReadOnlyList<HighlightsItemDto> Highlights,
    ApiMeta Meta,
    string? Warning = null);

