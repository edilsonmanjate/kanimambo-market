using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Infrastructure.Channels;

internal static class GatewayCall
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<Result<string>> PostAsync(HttpClient http, GatewayOptions options, ChannelKind channel, string path, object body,
        Func<JsonElement, string> readId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = JsonContent.Create(body, options: Json) };
            request.Headers.Add("X-Api-Key", options.ApiKey);
            using var response = await http.SendAsync(request, cancellationToken);
            var payload = response.Content.Headers.ContentLength is 0 ? default : await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new ChannelError(channel, "unavailable", $"{channel} gateway answered {(int)response.StatusCode}.", transient: true);
            }

            if (!response.IsSuccessStatusCode)
            {
                var code = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("error", out var e) ? e.GetString() ?? "rejected" : "rejected";
                return new ChannelError(channel, code.ToLowerInvariant(), $"{channel} refused the message: {code}.", transient: false);
            }

            return readId(payload);
        }
        catch (HttpRequestException exception)
        {
            return new ChannelError(channel, "unavailable", exception.Message, transient: true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ChannelError(channel, "unavailable", $"{channel} gateway did not answer in time.", transient: true);
        }
    }
}
