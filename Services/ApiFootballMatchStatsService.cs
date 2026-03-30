using System.Text.Json;
using LiveMatchApi.Contracts;
using LiveMatchApi.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LiveMatchApi.Services;

public sealed class ApiFootballMatchStatsService : IMatchStatsService
{
    private const string CacheKeyPrefix = "api-football:fixtures:statistics:";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptions<ApiFootballOptions> _options;
    private readonly IApiMetaFactory _metaFactory;
    private readonly ILogger<ApiFootballMatchStatsService> _logger;

    public ApiFootballMatchStatsService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<ApiFootballOptions> options,
        IApiMetaFactory metaFactory,
        ILogger<ApiFootballMatchStatsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options;
        _metaFactory = metaFactory;
        _logger = logger;
    }

    public async Task<MatchStatsResponse> GetStatsAsync(long fixtureId, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}{fixtureId}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<TeamStatsDto>? cached) && cached is not null)
        {
            return new MatchStatsResponse(
                MatchId: fixtureId,
                Status: "ok",
                Teams: cached,
                Meta: _metaFactory.Create(httpContext, source: "api-football"));
        }

        var client = _httpClientFactory.CreateClient(ApiFootballLiveMatchRepository.HttpClientName);
        using var response = await client.GetAsync($"fixtures/statistics?fixture={fixtureId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var teams = ParseStats(document.RootElement);

        var cacheSeconds = Math.Max(0, _options.Value.StatsCacheSeconds);
        _cache.Set(cacheKey, teams, TimeSpan.FromSeconds(cacheSeconds));

        return new MatchStatsResponse(
            MatchId: fixtureId,
            Status: "ok",
            Teams: teams,
            Meta: _metaFactory.Create(httpContext, source: "api-football"));
    }

    private IReadOnlyList<TeamStatsDto> ParseStats(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var responseArray) || responseArray.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Unexpected API-Football statistics response shape: missing 'response' array.");
            return Array.Empty<TeamStatsDto>();
        }

        var result = new List<TeamStatsDto>(capacity: Math.Min(2, responseArray.GetArrayLength()));

        foreach (var item in responseArray.EnumerateArray())
        {
            try
            {
                var teamId = GetInt32(item, "team", "id") ?? 0;
                var teamName = GetString(item, "team", "name") ?? "Unknown";
                var teamLogo = GetString(item, "team", "logo");

                var stats = new List<StatLineDto>(capacity: 32);
                if (item.TryGetProperty("statistics", out var statisticsArray) && statisticsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var stat in statisticsArray.EnumerateArray())
                    {
                        var type = GetString(stat, "type") ?? "Unknown";
                        string? value = null;

                        if (stat.TryGetProperty("value", out var rawValue))
                        {
                            value = rawValue.ValueKind switch
                            {
                                JsonValueKind.String => rawValue.GetString(),
                                JsonValueKind.Number => rawValue.ToString(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                JsonValueKind.Null => null,
                                _ => rawValue.ToString()
                            };
                        }

                        stats.Add(new StatLineDto(type, value));
                    }
                }

                result.Add(new TeamStatsDto(
                    Team: new TeamSummaryDto(teamId, teamName, teamLogo),
                    Statistics: stats));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse a statistics item from API-Football response.");
            }
        }

        return result;
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static int? GetInt32(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) ? value : null;
    }
}
