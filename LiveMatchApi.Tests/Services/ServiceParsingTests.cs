using System.Net;
using System.Net.Http;
using System.Text;
using LiveMatchApi.Infrastructure;
using LiveMatchApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LiveMatchApi.Tests.Services;

public sealed class ServiceParsingTests
{
    [Fact]
    public async Task ApiFootballMatchStatsService_ParsesAndCaches()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var json = """
            {
              "response": [
                {
                  "team": { "id": 1, "name": "Home", "logo": "https://example.invalid/home.png" },
                  "statistics": [
                    { "type": "Shots on Goal", "value": 3 },
                    { "type": "Ball Possession", "value": "58%" }
                  ]
                },
                {
                  "team": { "id": 2, "name": "Away", "logo": "https://example.invalid/away.png" },
                  "statistics": [
                    { "type": "Shots on Goal", "value": 1 }
                  ]
                }
              ]
            }
            """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.invalid/")
        };

        var factory = new StubHttpClientFactory((name) =>
            name == ApiFootballLiveMatchRepository.HttpClientName ? httpClient : new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton(new ProtocolStatus(QuicSupported: false, Http3Enabled: false, SupportedProtocols: new[] { "HTTP/1.1" }));
        services.AddSingleton<IApiMetaFactory, ApiMetaFactory>();
        var sp = services.BuildServiceProvider();

        var cache = sp.GetRequiredService<IMemoryCache>();
        var options = Options.Create(new ApiFootballOptions { StatsCacheSeconds = 30 });
        var meta = sp.GetRequiredService<IApiMetaFactory>();

        var sut = new ApiFootballMatchStatsService(factory, cache, options, meta, NullLogger<ApiFootballMatchStatsService>.Instance);

        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var first = await sut.GetStatsAsync(999, httpContext, CancellationToken.None);
        var second = await sut.GetStatsAsync(999, httpContext, CancellationToken.None);

        Assert.Equal("ok", first.Status);
        Assert.Equal(2, first.Teams.Count);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(first.Teams.Count, second.Teams.Count);
    }

    [Fact]
    public async Task ScoreBatHighlightsService_ReturnsDegradedOnUpstreamFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.invalid/")
        };

        var factory = new StubHttpClientFactory(name =>
            name == ScoreBatHighlightsService.HttpClientName ? httpClient : new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton(new ProtocolStatus(QuicSupported: false, Http3Enabled: false, SupportedProtocols: new[] { "HTTP/1.1" }));
        services.AddSingleton<IApiMetaFactory, ApiMetaFactory>();
        var sp = services.BuildServiceProvider();

        var cache = sp.GetRequiredService<IMemoryCache>();
        var options = Options.Create(new ScoreBatOptions { CacheSeconds = 0 });
        var meta = sp.GetRequiredService<IApiMetaFactory>();

        var sut = new ScoreBatHighlightsService(factory, cache, options, meta, NullLogger<ScoreBatHighlightsService>.Instance);

        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var response = await sut.GetFeedAsync(httpContext, CancellationToken.None);

        Assert.Equal("degraded", response.Status);
        Assert.NotNull(response.Warning);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<string, HttpClient> _factory;

        public StubHttpClientFactory(Func<string, HttpClient> factory)
        {
            _factory = factory;
        }

        public HttpClient CreateClient(string name) => _factory(name);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public int CallCount { get; private set; }

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_handler(request));
        }
    }
}

