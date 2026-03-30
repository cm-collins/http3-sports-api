using LiveMatchApi.Contracts;

namespace LiveMatchApi.Infrastructure;

public sealed class ApiMetaFactory : IApiMetaFactory
{
    private readonly ProtocolStatus _protocolStatus;

    public ApiMetaFactory(ProtocolStatus protocolStatus)
    {
        _protocolStatus = protocolStatus;
    }

    public ApiMeta Create(HttpContext httpContext, string source) => new(
        Protocol: httpContext.Request.Protocol,
        UtcTime: DateTimeOffset.UtcNow,
        Source: source,
        QuicSupported: _protocolStatus.QuicSupported,
        Http3Enabled: _protocolStatus.Http3Enabled);
}

