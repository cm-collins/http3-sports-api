using System.Collections.Generic;
using LiveMatchApi.Contracts;
using LiveMatchApi.Infrastructure;
using LiveMatchApi.Models;
using LiveMatchApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LiveMatchApi.Tests;

public sealed class LiveMatchApiFactory : WebApplicationFactory<Program>
{
    public const long FixtureId = 123456;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiFootball:ApiKey"] = "test-key",
                ["ApiFootball:BaseUrl"] = "https://example.invalid/",
                ["ApiFootball:LiveCacheSeconds"] = "0",
                ["ApiFootball:UpcomingCacheSeconds"] = "0",
                ["ApiFootball:StatsCacheSeconds"] = "0",
                ["ApiFootball:StreamPollSeconds"] = "1",
                ["ApiFootball:TimeoutSeconds"] = "1",
                ["ScoreBat:BaseUrl"] = "https://example.invalid/",
                ["ScoreBat:CacheSeconds"] = "0",
                ["ScoreBat:TimeoutSeconds"] = "1"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMatchesRepository>();
            services.RemoveAll<IMatchStatsService>();
            services.RemoveAll<IMatchStreamService>();
            services.RemoveAll<IHighlightsService>();

            services.AddSingleton<IMatchesRepository>(new FakeMatchesRepository());
            services.AddSingleton<IMatchStatsService>(new FakeMatchStatsService());
            services.AddSingleton<IMatchStreamService>(new FakeMatchStreamService());
            services.AddSingleton<IHighlightsService>(new FakeHighlightsService());
        });
    }

    private sealed class FakeMatchesRepository : IMatchesRepository
    {
        private static LiveMatch Match => new(
            FixtureId: FixtureId,
            LeagueId: 39,
            League: "Premier League",
            HomeTeamId: 1,
            HomeTeam: "Home FC",
            HomeTeamLogoUrl: "https://example.invalid/home.png",
            AwayTeamId: 2,
            AwayTeam: "Away FC",
            AwayTeamLogoUrl: "https://example.invalid/away.png",
            HomeScore: 1,
            AwayScore: 0,
            Minute: 34,
            Status: "1H",
            Venue: "Test Stadium",
            KickoffUtc: DateTimeOffset.Parse("2026-03-30T12:00:00Z"));

        public Task<IReadOnlyList<LiveMatch>> GetLiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LiveMatch>>(new[] { Match });

        public Task<IReadOnlyList<LiveMatch>> GetUpcomingAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LiveMatch>>(new[] { Match with { Minute = null, Status = "Not Started" } });

        public Task<LiveMatch?> GetByFixtureIdAsync(long fixtureId, CancellationToken cancellationToken) =>
            Task.FromResult<LiveMatch?>(fixtureId == FixtureId ? Match : null);
    }

    private sealed class FakeMatchStatsService : IMatchStatsService
    {
        public Task<MatchStatsResponse> GetStatsAsync(long fixtureId, HttpContext httpContext, CancellationToken cancellationToken)
        {
            var metaFactory = httpContext.RequestServices.GetRequiredService<IApiMetaFactory>();

            return Task.FromResult(new MatchStatsResponse(
                MatchId: fixtureId,
                Status: "ok",
                Teams: new[]
                {
                    new TeamStatsDto(
                        Team: new TeamSummaryDto(1, "Home FC", null),
                        Statistics: new[] { new StatLineDto("Shots on Goal", "3") })
                },
                Meta: metaFactory.Create(httpContext, source: "api-football")));
        }
    }

    private sealed class FakeMatchStreamService : IMatchStreamService
    {
        public async IAsyncEnumerable<MatchStreamMessage> StreamAsync(long fixtureId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new MatchStreamMessage(
                Id: 1,
                Event: "score_update",
                Data: new ScoreUpdateDto(fixtureId, 1, 0, 34, "1H"));

            yield return new MatchStreamMessage(
                Id: 2,
                Event: "goal",
                Data: new GoalDto(fixtureId, "home", 34, 1, 0, "Test Scorer", null));

            yield return new MatchStreamMessage(
                Id: 3,
                Event: "match_end",
                Data: new MatchEndDto(fixtureId, 1, 0, 90, "FT", "home"));

            await Task.CompletedTask;
        }
    }

    private sealed class FakeHighlightsService : IHighlightsService
    {
        public Task<HighlightsResponse> GetFeedAsync(HttpContext httpContext, CancellationToken cancellationToken) =>
            Task.FromResult(CreateResponse(httpContext, "ok"));

        public Task<HighlightsResponse> GetTeamAsync(string team, HttpContext httpContext, CancellationToken cancellationToken) =>
            Task.FromResult(CreateResponse(httpContext, "ok"));

        private static HighlightsResponse CreateResponse(HttpContext httpContext, string status)
        {
            var metaFactory = httpContext.RequestServices.GetRequiredService<IApiMetaFactory>();

            return new HighlightsResponse(
                Status: status,
                Highlights: new[]
                {
                    new HighlightsItemDto(
                        Id: "h1",
                        MatchId: null,
                        Title: "Test Highlight",
                        EmbedUrl: "<iframe>test</iframe>",
                        ThumbnailUrl: null,
                        Minute: null,
                        Competition: "Premier League",
                        PublishedAt: DateTimeOffset.Parse("2026-03-30T12:00:00Z"))
                },
                Meta: metaFactory.Create(httpContext, source: "scorebat"));
        }
    }
}
