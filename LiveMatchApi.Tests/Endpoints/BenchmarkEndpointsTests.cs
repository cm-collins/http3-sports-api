using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace LiveMatchApi.Tests.Endpoints;

public sealed class BenchmarkEndpointsTests : IClassFixture<LiveMatchApiFactory>
{
    private readonly HttpClient _client;

    public BenchmarkEndpointsTests(LiveMatchApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Benchmark_Ping_ReturnsOk()
    {
        var payload = await _client.GetFromJsonAsync<Dictionary<string, object>>("/api/benchmark/ping");
        Assert.NotNull(payload);
        Assert.Equal("ok", payload!["status"]?.ToString());
    }

    [Fact]
    public async Task Benchmark_Payload_ReturnsCorrectSizeAndHeaders()
    {
        using var response = await _client.GetAsync("/api/benchmark/payload/10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("10", response.Headers.GetValues("X-Payload-Kb").Single());
        Assert.NotEmpty(response.Headers.GetValues("X-Protocol").Single());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(10 * 1024, bytes.Length);
    }

    [Fact]
    public async Task Benchmark_Payload_RejectsOversize()
    {
        using var response = await _client.GetAsync("/api/benchmark/payload/999999");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Benchmark_Stream_IsSse()
    {
        using var response = await _client.GetAsync("/api/benchmark/stream?intervalMs=250", HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/event-stream", response.Content.Headers.ContentType?.ToString());

        await using var stream = await response.Content.ReadAsStreamAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var buffer = new byte[4096];
        var sb = new StringBuilder(capacity: 16 * 1024);

        var sawTick = false;
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, cts.Token);
                if (read == 0)
                {
                    break;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                if (sb.ToString().Contains("event: tick", StringComparison.Ordinal))
                {
                    sawTick = true;
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // If we didn't see a tick before timeout, the assert below will fail.
        }

        Assert.True(sawTick);
    }
}
