using System.Text.Json;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Common.Models;
using SponsorPulse.Doimain.Enums;
using SponsorPulse.Infrastructure.Services;

namespace SponsorPulse.Application.Services;

public class TwitchTrackingStrategy(
    HttpClient httpClient,
    ILinkedAccountManager linkedAccountManager,
    ILogger<TwitchTrackingStrategy> logger
) : IPlatformTrackingStrategy
{
    public StreamPlatform Platform => StreamPlatform.Twitch;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILinkedAccountManager _linkedAccountManager = linkedAccountManager;
    private readonly ILogger<TwitchTrackingStrategy> _logger = logger;

    public async Task<object> CaptureAsync(string broadcasterId, Guid? userId = null)
    {
        string accessToken = string.Empty;
        try
        {
            var account = userId.HasValue
                ? await _linkedAccountManager.FindTwitchLinkedAccountAsync(userId.Value)
                : null;
            accessToken = userId.HasValue
                ? await _linkedAccountManager.GetValidTwitchAccessTokenAsync(userId.Value)
                    ?? string.Empty
                : string.Empty;

            _logger.LogInformation(
                "Linked Twitch account {AccountStatus}; access token available: {HasAccessToken}.",
                account is null ? "not found" : "found",
                !string.IsNullOrWhiteSpace(accessToken)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "An error occurred during {platform} capture: {Message}",
                this.Platform,
                ex.Message
            );

            return Results.InternalServerError<TwitchMetrics>(null);
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Jeton d'accès Twitch introuvable ou invalide.");

            return Results.BadRequest<TwitchMetrics>(null);
        }

        _logger.LogInformation(
            "Access token found for broadcaster {broadcasterId}.",
            broadcasterId
        );

        if (
            !string.IsNullOrWhiteSpace(broadcasterId)
            && Uri.IsWellFormedUriString(broadcasterId, UriKind.Absolute)
        )
        {
            broadcasterId = new Uri(broadcasterId)
                .Segments.Last()
                .TrimEnd(['/', ' ', '\\'])
                .TrimStart('/');
        }
        // 2. Appel à l'API Twitch Helix avec le jeton frais
        var requestUrl =
            $"https://api.twitch.tv/helix/analytics/rooms/{broadcasterId}/live_metrics?_content_type=application/json&auth_token={accessToken}";
        using var response = await _httpClient.GetAsync(requestUrl);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Bad Request code {statusCode}.", response.StatusCode);

            return Results.BadRequest<TwitchMetrics>(null);
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        var metrics = JsonSerializer.Deserialize<TwitchMetrics>(responseBody);

        return Results.Ok(metrics);
    }
}
