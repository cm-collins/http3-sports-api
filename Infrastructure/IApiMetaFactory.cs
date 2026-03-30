using LiveMatchApi.Contracts;

namespace LiveMatchApi.Infrastructure;

public interface IApiMetaFactory
{
    ApiMeta Create(HttpContext httpContext, string source);
}

