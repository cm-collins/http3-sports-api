using System.Net.Quic;
using System.Security.Cryptography.X509Certificates;
using LiveMatchApi.Contracts;
using LiveMatchApi.Services;
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

builder.Services.AddOptions<ApiFootballOptions>()
    .BindConfiguration("ApiFootball")
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

builder.Services.AddSingleton<ILiveMatchRepository, ApiFootballLiveMatchRepository>();

var app = builder.Build();

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
        "/api/live-matches",
        "/api/live-matches/{id}"
    }
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "LiveMatchApi",
    utcTime = DateTimeOffset.UtcNow
}));

var liveMatches = app.MapGroup("/api/live-matches");

liveMatches.MapGet("/", async (HttpContext httpContext, ILiveMatchRepository repository, IOptions<ApiFootballOptions> options, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
    {
        return CreateProviderNotConfiguredProblem();
    }

    try
    {
        var matches = await repository.GetAllAsync(cancellationToken);

        var response = new LiveMatchListResponse(
            Matches: matches.Select(LiveMatchDto.FromModel).ToArray(),
            Meta: CreateMeta(protocol: httpContext.Request.Protocol, source: "api-football"));

        return Results.Ok(response);
    }
    catch (HttpRequestException ex)
    {
        app.Logger.LogWarning(ex, "Upstream provider request failed while fetching live matches.");
        return CreateUpstreamFailureProblem();
    }
    catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
    {
        app.Logger.LogWarning(ex, "Upstream provider request timed out while fetching live matches.");
        return CreateUpstreamFailureProblem();
    }
});

liveMatches.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, ILiveMatchRepository repository, IOptions<ApiFootballOptions> options, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
    {
        return CreateProviderNotConfiguredProblem();
    }

    try
    {
        var match = await repository.GetByIdAsync(id, cancellationToken);

        if (match is null)
        {
            return CreateNotFoundProblem(id);
        }

        var response = new LiveMatchResponse(
            Match: LiveMatchDto.FromModel(match),
            Meta: CreateMeta(protocol: httpContext.Request.Protocol, source: "api-football"));

        return Results.Ok(response);
    }
    catch (HttpRequestException ex)
    {
        app.Logger.LogWarning(ex, "Upstream provider request failed while fetching live match {MatchId}.", id);
        return CreateUpstreamFailureProblem();
    }
    catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
    {
        app.Logger.LogWarning(ex, "Upstream provider request timed out while fetching live match {MatchId}.", id);
        return CreateUpstreamFailureProblem();
    }
});

if (developmentCertificate is null)
{
    app.Logger.LogWarning("No localhost development certificate was found. HTTPS/HTTP3 on port 5001 is disabled.");
}
else if (!quicSupported)
{
    app.Logger.LogWarning("QUIC is not supported in this environment. HTTP/3 is disabled; HTTPS will use HTTP/1.1 + HTTP/2 only.");
}

app.Run();

ApiMeta CreateMeta(string protocol, string source) => new(
    Protocol: protocol,
    UtcTime: DateTimeOffset.UtcNow,
    Source: source);

IResult CreateProviderNotConfiguredProblem() => Results.Problem(
    statusCode: StatusCodes.Status503ServiceUnavailable,
    title: "Live match provider not configured",
    detail: "Set ApiFootball__ApiKey (environment variable) or ApiFootball:ApiKey (configuration) to enable real match data.");

IResult CreateUpstreamFailureProblem() => Results.Problem(
    statusCode: StatusCodes.Status502BadGateway,
    title: "Upstream provider unavailable",
    detail: "Failed to fetch data from the live match provider. Try again later.");

IResult CreateNotFoundProblem(Guid id) => Results.Problem(
    statusCode: StatusCodes.Status404NotFound,
    title: "Match not found",
    detail: $"Live match '{id}' was not found.");

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
