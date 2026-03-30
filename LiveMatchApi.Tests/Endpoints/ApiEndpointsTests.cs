using System.Net;
using System.Net.Http.Json;
using LiveMatchApi.Contracts;
using Xunit;

namespace LiveMatchApi.Tests.Endpoints;

public sealed class ApiEndpointsTests : IClassFixture<LiveMatchApiFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointsTests(LiveMatchApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Root_ReturnsServiceInfo()
    {
        var payload = await _client.GetFromJsonAsync<Dictionary<string, object>>("/");
        Assert.NotNull(payload);
        Assert.True(payload!.ContainsKey("service"));
    }

    [Fact]
    public async Task Matches_Live_ReturnsEnvelope()
    {
        var response = await _client.GetFromJsonAsync<MatchListResponse>("/api/matches/live");
        Assert.NotNull(response);
        Assert.NotEmpty(response!.Matches);
        Assert.Equal("api-football", response.Meta.Source);
    }

    [Fact]
    public async Task Matches_Upcoming_ReturnsEnvelope()
    {
        var response = await _client.GetFromJsonAsync<MatchListResponse>("/api/matches/upcoming");
        Assert.NotNull(response);
        Assert.NotEmpty(response!.Matches);
        Assert.Equal("api-football", response.Meta.Source);
    }

    [Fact]
    public async Task Matches_ByFixtureId_ReturnsMatch()
    {
        var response = await _client.GetFromJsonAsync<MatchResponse>($"/api/matches/{LiveMatchApiFactory.FixtureId}");
        Assert.NotNull(response);
        Assert.Equal(LiveMatchApiFactory.FixtureId, response!.Match.MatchId);
    }

    [Fact]
    public async Task Matches_ByFixtureId_Unknown_Returns404()
    {
        using var response = await _client.GetAsync("/api/matches/999999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Match_Stats_ReturnsOkContract()
    {
        var response = await _client.GetFromJsonAsync<MatchStatsResponse>($"/api/match/{LiveMatchApiFactory.FixtureId}/stats");
        Assert.NotNull(response);
        Assert.Equal("ok", response!.Status);
        Assert.Equal(LiveMatchApiFactory.FixtureId, response.MatchId);
        Assert.Equal("api-football", response.Meta.Source);
    }

    [Fact]
    public async Task Highlights_Feed_ReturnsOkContract()
    {
        var response = await _client.GetFromJsonAsync<HighlightsResponse>("/api/highlights/feed");
        Assert.NotNull(response);
        Assert.Equal("ok", response!.Status);
        Assert.Equal("scorebat", response.Meta.Source);
    }
}

