namespace LiveMatchApi.Services;

public sealed record MatchStreamMessage(
    long Id,
    string Event,
    object Data);

