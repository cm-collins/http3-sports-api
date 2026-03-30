namespace LiveMatchApi.Contracts;

public sealed record HighlightsItemDto(
    string Id,
    string? MatchId,
    string Title,
    string EmbedUrl,
    string? ThumbnailUrl,
    int? Minute,
    string? Competition,
    DateTimeOffset? PublishedAt);

