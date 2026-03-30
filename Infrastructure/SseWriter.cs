using System.Text.Json;

namespace LiveMatchApi.Infrastructure;

public static class SseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteRetryAsync(HttpResponse response, int retryMs, CancellationToken cancellationToken) =>
        response.WriteAsync($"retry: {retryMs}\n\n", cancellationToken);

    public static async ValueTask WriteEventAsync<T>(
        HttpResponse response,
        long id,
        string eventName,
        T data,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($"id: {id}\n", cancellationToken);
        await response.WriteAsync($"event: {eventName}\n", cancellationToken);

        var payload = JsonSerializer.Serialize(data, SerializerOptions);
        foreach (var line in payload.Split('\n'))
        {
            await response.WriteAsync($"data: {line}\n", cancellationToken);
        }

        await response.WriteAsync("\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    public static async ValueTask WriteCommentAsync(HttpResponse response, string comment, CancellationToken cancellationToken)
    {
        await response.WriteAsync($": {comment}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
