namespace LiveMatchApi.Infrastructure;

public sealed record ProtocolStatus(
    bool QuicSupported,
    bool Http3Enabled,
    IReadOnlyList<string> SupportedProtocols);

