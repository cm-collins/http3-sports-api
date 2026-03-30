namespace LiveMatchApi.Infrastructure;

public static class ApiProblems
{
    public static IResult ProviderNotConfigured() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Live match provider not configured",
        detail: "Set ApiFootball__ApiKey (environment variable) or ApiFootball:ApiKey (configuration) to enable real match data.");

    public static IResult UpstreamUnavailable() => Results.Problem(
        statusCode: StatusCodes.Status502BadGateway,
        title: "Upstream provider unavailable",
        detail: "Failed to fetch data from the live match provider. Try again later.");

    public static IResult MatchNotFound(long fixtureId) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Match not found",
        detail: $"Match '{fixtureId}' was not found.");
}
