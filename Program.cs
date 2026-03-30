using System.Threading.RateLimiting;
using System.Security.Cryptography.X509Certificates;
using System.Net.Quic;
using LiveMatchApi.Infrastructure;
using LiveMatchApi.Endpoints;
using LiveMatchApi.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var developmentCertificate = TryFindLocalhostDevelopmentCertificate();
var quicSupported = QuicListener.IsSupported;
var http3Enabled = developmentCertificate is not null && quicSupported;

var supportedProtocols = developmentCertificate is null
    ? new[] { "HTTP/1.1" }
    : http3Enabled
        ? new[] { "HTTP/1.1", "HTTP/2", "HTTP/3" }
        : new[] { "HTTP/1.1", "HTTP/2" };

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });

    if (developmentCertificate is not null)
    {
        options.ListenAnyIP(5001, listenOptions =>
        {
            listenOptions.UseHttps(developmentCertificate);
            listenOptions.Protocols = http3Enabled
                ? HttpProtocols.Http1AndHttp2AndHttp3
                : HttpProtocols.Http1AndHttp2;
        });
    }
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(new ProtocolStatus(
    QuicSupported: quicSupported,
    Http3Enabled: http3Enabled,
    SupportedProtocols: supportedProtocols));
builder.Services.AddSingleton<IApiMetaFactory, ApiMetaFactory>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 120;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services.AddOptions<ApiFootballOptions>()
    .BindConfiguration("ApiFootball")
    .ValidateOnStart();

builder.Services.AddOptions<ScoreBatOptions>()
    .BindConfiguration("ScoreBat")
    .ValidateOnStart();

builder.Services.AddHttpClient(ApiFootballLiveMatchRepository.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ApiFootballOptions>>().Value;

    var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : $"{options.BaseUrl}/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 60));

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("x-apisports-key", options.ApiKey);
    }
});

builder.Services.AddHttpClient(ScoreBatHighlightsService.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ScoreBatOptions>>().Value;

    var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : $"{options.BaseUrl}/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 60));
});

builder.Services.AddSingleton<IMatchesRepository, ApiFootballLiveMatchRepository>();
builder.Services.AddSingleton<IMatchStatsService, ApiFootballMatchStatsService>();
builder.Services.AddSingleton<IMatchStreamService, ApiFootballMatchStreamService>();
builder.Services.AddSingleton<IHighlightsService, ScoreBatHighlightsService>();

var app = builder.Build();

app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new
{
    service = "http3-sports-api",
    status = "ready",
    protocols = supportedProtocols,
    quicSupported,
    http3Enabled,
    endpoints = new[]
    {
        "/health",
        "/api/matches/live",
        "/api/matches/upcoming",
        "/api/matches/{fixtureId}",
        "/api/match/{fixtureId}/stream",
        "/api/match/{fixtureId}/score-stream",
        "/api/match/{fixtureId}/commentary-stream",
        "/api/match/{fixtureId}/stats",
        "/api/highlights/feed",
        "/api/highlights/{team}",
        "/api/live-matches (alias)"
    }
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "LiveMatchApi",
    utcTime = DateTimeOffset.UtcNow
}));

app.MapMatchesEndpoints();
app.MapMatchEndpoints();
app.MapHighlightsEndpoints();

if (developmentCertificate is null)
{
    app.Logger.LogWarning("No localhost development certificate was found. HTTPS/HTTP3 on port 5001 is disabled.");
}
else if (!quicSupported)
{
    app.Logger.LogWarning("QUIC is not supported in this environment. HTTP/3 is disabled; HTTPS will use HTTP/1.1 + HTTP/2 only.");
}

app.Run();

static X509Certificate2? TryFindLocalhostDevelopmentCertificate()
{
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);

    return store.Certificates
        .OfType<X509Certificate2>()
        .Where(certificate =>
            certificate.Subject.Contains("CN=localhost", StringComparison.OrdinalIgnoreCase) &&
            certificate.NotAfter > DateTime.UtcNow)
        .OrderByDescending(certificate => certificate.NotAfter)
        .FirstOrDefault();
}
