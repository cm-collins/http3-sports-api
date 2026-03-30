using System.Diagnostics;
using System.Net;
using System.Net.Quic;

static int Usage()
{
    Console.Error.WriteLine("ProtocolProbe");
    Console.Error.WriteLine("  --url <url> [--h1|--h2|--h3] [--insecure]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  dotnet run --project tools/ProtocolProbe/ProtocolProbe.csproj -- --url https://localhost:5001/health --h2 --insecure");
    Console.Error.WriteLine("  dotnet run --project tools/ProtocolProbe/ProtocolProbe.csproj -- --url https://localhost:5001/health --h3 --insecure");
    return 2;
}

var url = (string?)null;
var insecure = false;
var requested = "h2";

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];

    if (arg is "--url" && i + 1 < args.Length)
    {
        url = args[++i];
        continue;
    }

    if (arg is "--insecure")
    {
        insecure = true;
        continue;
    }

    if (arg is "--h1" or "--h2" or "--h3")
    {
        requested = arg[2..];
        continue;
    }

    return Usage();
}

if (string.IsNullOrWhiteSpace(url))
{
    return Usage();
}

Version requestVersion;
HttpVersionPolicy versionPolicy;

switch (requested)
{
    case "h1":
        requestVersion = HttpVersion.Version11;
        versionPolicy = HttpVersionPolicy.RequestVersionExact;
        break;
    case "h2":
        requestVersion = HttpVersion.Version20;
        versionPolicy = HttpVersionPolicy.RequestVersionExact;
        break;
    case "h3":
        AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", true);
        Console.WriteLine($"quicSupported={QuicListener.IsSupported}");
        requestVersion = HttpVersion.Version30;
        versionPolicy = HttpVersionPolicy.RequestVersionExact;
        break;
    default:
        return Usage();
}

var handler = new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.All,
    AllowAutoRedirect = false
};

if (insecure)
{
    handler.SslOptions = new()
    {
        RemoteCertificateValidationCallback = (_, _, _, _) => true
    };
}

using var client = new HttpClient(handler)
{
    Timeout = TimeSpan.FromSeconds(15)
};

using var request = new HttpRequestMessage(HttpMethod.Get, url)
{
    Version = requestVersion,
    VersionPolicy = versionPolicy
};

var stopwatch = Stopwatch.StartNew();

try
{
    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    stopwatch.Stop();

    var negotiated = response.Version;
    var status = (int)response.StatusCode;

    Console.WriteLine($"requested={requested} negotiated=HTTP/{negotiated} status={status} elapsedMs={(int)stopwatch.Elapsed.TotalMilliseconds}");

    // Read a small body preview for sanity.
    var body = await response.Content.ReadAsStringAsync();
    var preview = body.Length <= 300 ? body : body[..300] + "...";
    Console.WriteLine(preview);

    return status >= 200 && status < 600 ? 0 : 1;
}
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
{
    stopwatch.Stop();
    Console.Error.WriteLine($"requested={requested} error={ex.GetType().Name} elapsedMs={(int)stopwatch.Elapsed.TotalMilliseconds}");
    Console.Error.WriteLine(ex.Message);
    return 1;
}
