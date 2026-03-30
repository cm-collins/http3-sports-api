namespace LiveMatchApi.Services;

public sealed class ApiFootballOptions
{
    public string BaseUrl { get; init; } = "https://v3.football.api-sports.io/";
    public string? ApiKey { get; init; }
    public int LiveCacheSeconds { get; init; } = 10;
    public int UpcomingCacheSeconds { get; init; } = 60;
    public int StatsCacheSeconds { get; init; } = 30;
    public int StreamPollSeconds { get; init; } = 10;
    public int TimeoutSeconds { get; init; } = 10;
}
