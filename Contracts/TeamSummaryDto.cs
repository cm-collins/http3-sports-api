namespace LiveMatchApi.Contracts;

public sealed record TeamSummaryDto(
    int Id,
    string Name,
    string? LogoUrl);

