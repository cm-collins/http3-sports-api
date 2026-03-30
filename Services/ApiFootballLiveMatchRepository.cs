using System.Security.Cryptography;
using System.Text.Json;
using LiveMatchApi.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LiveMatchApi.Services;

public sealed class ApiFootballLiveMatchRepository : ILiveMatchRepository
{
    public const string HttpClientName = "ApiFootball";

    private const string LiveMatchesCacheKey = "api-football:fixtures:live=all";

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

    public async Task<IReadOnlyList<LiveMatch>> GetAllAsync(CancellationToken cancellationToken)
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

    public async Task<LiveMatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var matches = await GetAllAsync(cancellationToken);
        return matches.FirstOrDefault(match => match.Id == id);
    }

    private IReadOnlyList<LiveMatch> ParseLiveMatches(JsonElement root)
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

                var league = GetString(item, "league", "name") ?? "Unknown";
                var homeTeam = GetString(item, "teams", "home", "name") ?? "Home";
                var awayTeam = GetString(item, "teams", "away", "name") ?? "Away";

                var homeScore = GetInt32(item, "goals", "home") ?? 0;
                var awayScore = GetInt32(item, "goals", "away") ?? 0;

                var minute = GetInt32(item, "fixture", "status", "elapsed") ?? 0;
                var status = GetString(item, "fixture", "status", "long")
                             ?? GetString(item, "fixture", "status", "short")
                             ?? "Unknown";

                var venue = GetString(item, "fixture", "venue", "name") ?? "Unknown";
                var kickoffUtc = GetDateTimeOffset(item, "fixture", "date") ?? DateTimeOffset.UtcNow;

                matches.Add(new LiveMatch(
                    Id: CreateDeterministicGuid($"api-football:fixture:{fixtureId.Value}"),
                    League: league,
                    HomeTeam: homeTeam,
                    AwayTeam: awayTeam,
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

    private static Guid CreateDeterministicGuid(string input)
    {
        // Stable 16-byte id from a string key. This keeps the API's existing GUID shape while using provider ids.
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}

