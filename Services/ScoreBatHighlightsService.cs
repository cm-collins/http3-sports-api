using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using LiveMatchApi.Contracts;
using LiveMatchApi.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LiveMatchApi.Services;

public sealed class ScoreBatHighlightsService : IHighlightsService
{
    public const string HttpClientName = "ScoreBat";

    private const string CacheKeyFeed = "scorebat:highlights:feed";
    private const string CacheKeyTeamPrefix = "scorebat:highlights:team:";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptions<ScoreBatOptions> _options;
    private readonly IApiMetaFactory _metaFactory;
    private readonly ILogger<ScoreBatHighlightsService> _logger;

    public ScoreBatHighlightsService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<ScoreBatOptions> options,
        IApiMetaFactory metaFactory,
        ILogger<ScoreBatHighlightsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options;
        _metaFactory = metaFactory;
        _logger = logger;
    }

    public Task<HighlightsResponse> GetFeedAsync(HttpContext httpContext, CancellationToken cancellationToken) =>
        GetCachedAsync(
            cacheKey: CacheKeyFeed,
            requestPath: "video-api/v3/",
            httpContext: httpContext,
            cancellationToken: cancellationToken);

    public Task<HighlightsResponse> GetTeamAsync(string team, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var normalized = (team ?? string.Empty).Trim();
        var cacheKey = $"{CacheKeyTeamPrefix}{normalized.ToLowerInvariant()}";
        var encoded = Uri.EscapeDataString(normalized);
        return GetCachedAsync(
            cacheKey: cacheKey,
            requestPath: $"video-api/v3/team/{encoded}",
            httpContext: httpContext,
            cancellationToken: cancellationToken);
    }

    private async Task<HighlightsResponse> GetCachedAsync(
        string cacheKey,
        string requestPath,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<HighlightsItemDto>? cached) && cached is not null)
        {
            return new HighlightsResponse(
                Status: "ok",
                Highlights: cached,
                Meta: _metaFactory.Create(httpContext, source: "scorebat"));
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = WithToken(requestPath);
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var highlights = ParseHighlights(document.RootElement);

            var cacheSeconds = Math.Max(0, _options.Value.CacheSeconds);
            _cache.Set(cacheKey, highlights, TimeSpan.FromSeconds(cacheSeconds));

            return new HighlightsResponse(
                Status: "ok",
                Highlights: highlights,
                Meta: _metaFactory.Create(httpContext, source: "scorebat"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to fetch highlights from ScoreBat path '{Path}'.", requestPath);
            return new HighlightsResponse(
                Status: "degraded",
                Highlights: Array.Empty<HighlightsItemDto>(),
                Meta: _metaFactory.Create(httpContext, source: "scorebat"),
                Warning: "Highlights provider unavailable.");
        }
    }

    private string WithToken(string requestPath)
    {
        var token = _options.Value.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            return requestPath;
        }

        var encoded = Uri.EscapeDataString(token.Trim());
        return requestPath.Contains('?', StringComparison.Ordinal)
            ? $"{requestPath}&token={encoded}"
            : $"{requestPath}?token={encoded}";
    }

    private IReadOnlyList<HighlightsItemDto> ParseHighlights(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var responseArray) || responseArray.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Unexpected ScoreBat response shape: missing 'response' array.");
            return Array.Empty<HighlightsItemDto>();
        }

        var result = new List<HighlightsItemDto>(capacity: Math.Min(50, responseArray.GetArrayLength()));

        foreach (var item in responseArray.EnumerateArray())
        {
            try
            {
                var title = GetString(item, "title") ?? "Untitled";
                var competition = GetString(item, "competition") ?? GetString(item, "competition", "name");
                var thumbnailUrl = GetString(item, "thumbnail");
                var publishedAt = GetDateTimeOffset(item, "date");

                var matchViewUrl = GetString(item, "matchviewUrl");
                var embed = GetFirstVideoEmbed(item);

                if (string.IsNullOrWhiteSpace(embed))
                {
                    // If the response doesn't include any embeddable video for this item, skip it.
                    continue;
                }

                var id = matchViewUrl ?? CreateFallbackId(title, publishedAt);

                result.Add(new HighlightsItemDto(
                    Id: id,
                    MatchId: null,
                    Title: title,
                    EmbedUrl: embed,
                    ThumbnailUrl: thumbnailUrl,
                    Minute: null,
                    Competition: competition,
                    PublishedAt: publishedAt));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse a highlight item from ScoreBat response.");
            }
        }

        return result;
    }

    private static string? GetFirstVideoEmbed(JsonElement item)
    {
        if (!item.TryGetProperty("videos", out var videosArray) || videosArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var video in videosArray.EnumerateArray())
        {
            var embed = GetString(video, "embed");
            if (!string.IsNullOrWhiteSpace(embed))
            {
                return embed;
            }
        }

        return null;
    }

    private static string CreateFallbackId(string title, DateTimeOffset? publishedAt)
    {
        var raw = $"{title}|{publishedAt:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
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
}
