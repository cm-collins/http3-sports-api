using System.Security.Cryptography.X509Certificates;
using LiveMatchApi.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
var developmentCertificate = TryFindLocalhostDevelopmentCertificate();
var supportedProtocols = developmentCertificate is null
    ? new[] { "HTTP/1.1", "HTTP/2" }
    : new[] { "HTTP/1.1", "HTTP/2", "HTTP/3" };

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });

    if (developmentCertificate is not null)
    {
        options.ListenAnyIP(5001, listenOptions =>
        {
            listenOptions.UseHttps(developmentCertificate);
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
        });
    }
});

builder.Services.AddSingleton<ILiveMatchRepository, InMemoryLiveMatchRepository>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "http3-sports-api",
    status = "ready",
    protocols = supportedProtocols,
    http3Enabled = developmentCertificate is not null,
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

liveMatches.MapGet("/", (ILiveMatchRepository repository) =>
{
    return Results.Ok(repository.GetAll());
});

liveMatches.MapGet("/{id:guid}", (Guid id, ILiveMatchRepository repository) =>
{
    var match = repository.GetById(id);

    return match is null
        ? Results.NotFound(new
        {
            message = $"Live match '{id}' was not found."
        })
        : Results.Ok(match);
});

if (developmentCertificate is null)
{
    app.Logger.LogWarning("No localhost development certificate was found. HTTPS/HTTP3 on port 5001 is disabled.");
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
