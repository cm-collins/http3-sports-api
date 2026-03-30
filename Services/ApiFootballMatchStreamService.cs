using System.Runtime.CompilerServices;
using LiveMatchApi.Contracts;
using LiveMatchApi.Models;
using Microsoft.Extensions.Options;

namespace LiveMatchApi.Services;

public sealed class ApiFootballMatchStreamService : IMatchStreamService
{
    private readonly IMatchesRepository _matchesRepository;
    private readonly IOptions<ApiFootballOptions> _options;
    private readonly ILogger<ApiFootballMatchStreamService> _logger;

    public ApiFootballMatchStreamService(
        IMatchesRepository matchesRepository,
        IOptions<ApiFootballOptions> options,
        ILogger<ApiFootballMatchStreamService> logger)
    {
        _matchesRepository = matchesRepository;
        _options = options;
        _logger = logger;
    }

    public async IAsyncEnumerable<MatchStreamMessage> StreamAsync(
        long fixtureId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pollSeconds = Math.Clamp(_options.Value.StreamPollSeconds, 1, 60);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollSeconds));

        LiveMatch? previous = null;
        var nextId = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            LiveMatch? current = null;
            Exception? pollException = null;
            try
            {
                current = await _matchesRepository.GetByFixtureIdAsync(fixtureId, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                pollException = ex;
            }

            if (pollException is not null)
            {
                _logger.LogWarning(pollException, "Match stream poll failed for fixture {FixtureId}.", fixtureId);
                yield return new MatchStreamMessage(
                    Id: ++nextId,
                    Event: "upstream_error",
                    Data: new { matchId = fixtureId, utcTime = DateTimeOffset.UtcNow });

                if (!await timer.WaitForNextTickAsync(cancellationToken))
                {
                    yield break;
                }

                continue;
            }

            if (current is null)
            {
                yield return new MatchStreamMessage(
                    Id: ++nextId,
                    Event: "match_missing",
                    Data: new { matchId = fixtureId, utcTime = DateTimeOffset.UtcNow });
                yield break;
            }

            if (previous is not null && (current.HomeScore != previous.HomeScore || current.AwayScore != previous.AwayScore))
            {
                foreach (var goal in CreateGoalEvents(previous, current))
                {
                    yield return new MatchStreamMessage(Id: ++nextId, Event: "goal", Data: goal);
                }
            }

            if (previous is null || HasMeaningfulChange(previous, current))
            {
                yield return new MatchStreamMessage(
                    Id: ++nextId,
                    Event: "score_update",
                    Data: new ScoreUpdateDto(
                        MatchId: fixtureId,
                        HomeScore: current.HomeScore,
                        AwayScore: current.AwayScore,
                        Minute: current.Minute,
                        Status: current.Status));
            }

            if (IsMatchEnded(current.Status))
            {
                yield return new MatchStreamMessage(
                    Id: ++nextId,
                    Event: "match_end",
                    Data: new MatchEndDto(
                        MatchId: fixtureId,
                        HomeScore: current.HomeScore,
                        AwayScore: current.AwayScore,
                        Minute: current.Minute,
                        Status: current.Status,
                        Winner: GetWinner(current.HomeScore, current.AwayScore)));
                yield break;
            }

            previous = current;

            // Keep the connection active even when no score changes occur.
            yield return new MatchStreamMessage(
                Id: ++nextId,
                Event: "heartbeat",
                Data: new { matchId = fixtureId, utcTime = DateTimeOffset.UtcNow });

            if (!await timer.WaitForNextTickAsync(cancellationToken))
            {
                yield break;
            }
        }
    }

    private static bool HasMeaningfulChange(LiveMatch previous, LiveMatch current) =>
        previous.HomeScore != current.HomeScore ||
        previous.AwayScore != current.AwayScore ||
        previous.Minute != current.Minute ||
        !string.Equals(previous.Status, current.Status, StringComparison.Ordinal);

    private static IEnumerable<GoalDto> CreateGoalEvents(LiveMatch previous, LiveMatch current)
    {
        var matchId = current.FixtureId;
        var minute = current.Minute;

        var homeDelta = Math.Max(0, current.HomeScore - previous.HomeScore);
        for (var i = 0; i < homeDelta; i++)
        {
            yield return new GoalDto(
                MatchId: matchId,
                Team: "home",
                Minute: minute,
                HomeScore: current.HomeScore,
                AwayScore: current.AwayScore);
        }

        var awayDelta = Math.Max(0, current.AwayScore - previous.AwayScore);
        for (var i = 0; i < awayDelta; i++)
        {
            yield return new GoalDto(
                MatchId: matchId,
                Team: "away",
                Minute: minute,
                HomeScore: current.HomeScore,
                AwayScore: current.AwayScore);
        }
    }

    private static bool IsMatchEnded(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        // API-Football can return either the long or short status string.
        return status.Equals("FT", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("AET", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("PEN", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("Finished", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("Match Finished", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWinner(int home, int away) =>
        home > away ? "home" :
        away > home ? "away" :
        "draw";
}
