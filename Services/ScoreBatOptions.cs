namespace LiveMatchApi.Services;

public sealed class ScoreBatOptions
{
    public string BaseUrl { get; init; } = "https://www.scorebat.com/";
    public int CacheSeconds { get; init; } = 300;
    public int TimeoutSeconds { get; init; } = 10;
}

