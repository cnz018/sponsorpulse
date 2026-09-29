using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Doimain.Enums;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;
using SponsorPulse.Infrastructure.Services;

namespace SponsorPulse.Application.Services;

public class TwitchTrackingStrategy(HttpClient httpClient, ILogger<TwitchTrackingStrategy> logger)
    : IPlatformTrackingStrategy
{
    public StreamPlatform Platform => StreamPlatform.Twitch;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<TwitchTrackingStrategy> _logger = logger;

    public async Task<object> CaptureAsync(string broadcasterId, Guid? userId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                return Result<TwitchMetrics>.Failure("A Twitch broadcaster ID is required.");
            }

            if (Uri.IsWellFormedUriString(broadcasterId, UriKind.Absolute))
            {
                broadcasterId = new Uri(broadcasterId).Segments.Last().Trim('/');
            }

            var broadcasterQuery = broadcasterId.All(char.IsAsciiDigit) ? "user_id" : "user_login";
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.twitch.tv/helix/streams?{broadcasterQuery}={Uri.EscapeDataString(broadcasterId)}"
            );

            if (userId.HasValue)
            {
                request.Options.Set(TwitchAuthenticationHandler.UserIdOption, userId.Value);
            }

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Twitch returned status {StatusCode}.", response.StatusCode);
                return Result<TwitchMetrics>.Failure(
                    $"Twitch API returned {(int)response.StatusCode}."
                );
            }

            var streamResponse = await response.Content.ReadFromJsonAsync<TwitchStreamsResponse>();

            if (streamResponse?.Data is null)
            {
                return Result<TwitchMetrics>.Failure(
                    "Twitch returned an invalid streams response."
                );
            }

            var stream = streamResponse.Data.FirstOrDefault();
            if (stream is null)
            {
                _logger.LogInformation(
                    "Twitch returned no live stream for broadcaster {BroadcasterId}.",
                    broadcasterId
                );
                return Result<TwitchMetrics>.Success(TwitchMetrics.Empty);
            }

            _logger.LogInformation(
                "Twitch metrics mapped for broadcaster {BroadcasterId}: {ViewerCount} viewers, game {GameName}.",
                broadcasterId,
                stream.ViewerCount,
                stream.GameName
            );
            return Result<TwitchMetrics>.Success(
                TwitchMetrics.Create(
                    stream.ViewerCount,
                    stream.ViewerCount,
                    0,
                    DateTimeOffset.UtcNow - stream.StartedAt,
                    stream.StartedAt,
                    stream.GameName
                )
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Twitch capture failed for broadcaster {BroadcasterId}.",
                broadcasterId
            );
            return Result<TwitchMetrics>.Failure("Twitch capture failed.");
        }
    }

    private sealed record TwitchStreamsResponse(
        [property: JsonPropertyName("data")] List<TwitchStream>? Data
    );

    private sealed record TwitchStream(
        [property: JsonPropertyName("viewer_count")] int ViewerCount,
        [property: JsonPropertyName("game_name")] string GameName,
        [property: JsonPropertyName("started_at")] DateTimeOffset StartedAt
    );
}
