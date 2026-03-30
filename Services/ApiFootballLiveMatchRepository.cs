using System.Text.Json;
using LiveMatchApi.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LiveMatchApi.Services;

public sealed class ApiFootballLiveMatchRepository : IMatchesRepository
{
    public const string HttpClientName = "ApiFootball";

    private const string LiveMatchesCacheKey = "api-football:fixtures:live=all";
    private const string UpcomingMatchesCacheKeyPrefix = "api-football:fixtures:upcoming:";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptions<ApiFootballOptions> _options;
    private readonly ILogger<ApiFootballLiveMatchRepository> _logger;

    public ApiFootballLiveMatchRepository(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<ApiFootballOptions> options,
        ILogger<ApiFootballLiveMatchRepository> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LiveMatch>> GetLiveAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(LiveMatchesCacheKey, out IReadOnlyList<LiveMatch>? cached) && cached is not null)
        {
            return cached;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync("fixtures?live=all", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var matches = ParseLiveMatches(document.RootElement);

        var cacheSeconds = Math.Max(0, _options.Value.LiveCacheSeconds);
        _cache.Set(LiveMatchesCacheKey, matches, TimeSpan.FromSeconds(cacheSeconds));

        return matches;
    }

    public async Task<IReadOnlyList<LiveMatch>> GetUpcomingAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        var from = fromUtc.ToUniversalTime().ToString("yyyy-MM-dd");
        var to = toUtc.ToUniversalTime().ToString("yyyy-MM-dd");
        var cacheKey = $"{UpcomingMatchesCacheKeyPrefix}{from}:{to}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LiveMatch>? cached) && cached is not null)
        {
            return cached;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync($"fixtures?from={from}&to={to}", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var matches = ParseMatches(document.RootElement);

        var cacheSeconds = Math.Max(0, _options.Value.UpcomingCacheSeconds);
        _cache.Set(cacheKey, matches, TimeSpan.FromSeconds(cacheSeconds));

        return matches;
    }

    public async Task<LiveMatch?> GetByFixtureIdAsync(long fixtureId, CancellationToken cancellationToken)
    {
        // Fast path: search within cached live list first.
        var live = await GetLiveAsync(cancellationToken);
        var fromLive = live.FirstOrDefault(match => match.FixtureId == fixtureId);
        if (fromLive is not null)
        {
            return fromLive;
        }

        // Otherwise query upstream for this specific fixture id.
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync($"fixtures?id={fixtureId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return ParseMatches(document.RootElement).FirstOrDefault();
    }

    private IReadOnlyList<LiveMatch> ParseLiveMatches(JsonElement root) => ParseMatches(root);

    private IReadOnlyList<LiveMatch> ParseMatches(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var responseArray) || responseArray.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Unexpected API-Football response shape: missing 'response' array.");
            return Array.Empty<LiveMatch>();
        }

        var matches = new List<LiveMatch>(capacity: Math.Min(64, responseArray.GetArrayLength()));

        foreach (var item in responseArray.EnumerateArray())
        {
            try
            {
                var fixtureId = GetInt64(item, "fixture", "id");
                if (fixtureId is null)
                {
                    continue;
                }

                var leagueId = GetInt32(item, "league", "id") ?? 0;
                var league = GetString(item, "league", "name") ?? "Unknown";

                var homeTeamId = GetInt32(item, "teams", "home", "id") ?? 0;
                var homeTeam = GetString(item, "teams", "home", "name") ?? "Home";
                var homeLogo = GetString(item, "teams", "home", "logo");

                var awayTeamId = GetInt32(item, "teams", "away", "id") ?? 0;
                var awayTeam = GetString(item, "teams", "away", "name") ?? "Away";
                var awayLogo = GetString(item, "teams", "away", "logo");

                var homeScore = GetInt32(item, "goals", "home") ?? 0;
                var awayScore = GetInt32(item, "goals", "away") ?? 0;

                var minute = GetInt32(item, "fixture", "status", "elapsed");
                var status = GetString(item, "fixture", "status", "long")
                             ?? GetString(item, "fixture", "status", "short")
                             ?? "Unknown";

                var venue = GetString(item, "fixture", "venue", "name");
                var kickoffUtc = GetDateTimeOffset(item, "fixture", "date") ?? DateTimeOffset.UtcNow;

                matches.Add(new LiveMatch(
                    FixtureId: fixtureId.Value,
                    LeagueId: leagueId,
                    League: league,
                    HomeTeamId: homeTeamId,
                    HomeTeam: homeTeam,
                    HomeTeamLogoUrl: homeLogo,
                    AwayTeamId: awayTeamId,
                    AwayTeam: awayTeam,
                    AwayTeamLogoUrl: awayLogo,
                    HomeScore: homeScore,
                    AwayScore: awayScore,
                    Minute: minute,
                    Status: status,
                    Venue: venue,
                    KickoffUtc: kickoffUtc));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse a live match item from API-Football response.");
            }
        }

        return matches;
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

    private static long? GetInt64(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var value) ? value : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = element.GetString();
        return DateTimeOffset.TryParse(raw, out var dto) ? dto.ToUniversalTime() : null;
    }

    // No deterministic GUIDs: Phase 1 standardizes on provider fixture ids as public match ids.
}
