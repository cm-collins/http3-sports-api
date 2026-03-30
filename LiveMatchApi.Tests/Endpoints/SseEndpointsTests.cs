using System.Net;
using Xunit;

namespace LiveMatchApi.Tests.Endpoints;

public sealed class SseEndpointsTests : IClassFixture<LiveMatchApiFactory>
{
    private readonly HttpClient _client;

    public SseEndpointsTests(LiveMatchApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Match_Stream_EmitsEvents()
    {
        using var response = await _client.GetAsync($"/api/match/{LiveMatchApiFactory.FixtureId}/stream");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/event-stream", response.Content.Headers.ContentType?.ToString());

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: meta", body);
        Assert.Contains("event: score_update", body);
        Assert.Contains("event: goal", body);
        Assert.Contains("event: match_end", body);
    }

    [Fact]
    public async Task Match_ScoreStream_OnlyEmitsScoreEvents()
    {
        using var response = await _client.GetAsync($"/api/match/{LiveMatchApiFactory.FixtureId}/score-stream");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: score_update", body);
        Assert.Contains("event: match_end", body);
        Assert.DoesNotContain("event: goal", body);
    }

    [Fact]
    public async Task Match_CommentaryStream_EmitsGoalEvents()
    {
        using var response = await _client.GetAsync($"/api/match/{LiveMatchApiFactory.FixtureId}/commentary-stream");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: goal", body);
        Assert.Contains("event: match_end", body);
        Assert.DoesNotContain("event: score_update", body);
    }
}

