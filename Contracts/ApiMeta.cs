namespace LiveMatchApi.Contracts;

public sealed record ApiMeta(
    string Protocol,
    DateTimeOffset UtcTime,
    string Source);

