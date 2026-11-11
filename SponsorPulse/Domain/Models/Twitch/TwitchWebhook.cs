using System.Text.Json.Serialization;

namespace SponsorPulse.Models;

/// <summary>
/// Modèle principal reçu depuis le Webhook Twitch EventSub.
/// </summary>
public record TwitchWebhookPayload
{
    // Rempli uniquement lors du handshake de validation
    [JsonPropertyName("challenge")]
    public string? Challenge { get; set; }

    [JsonPropertyName("subscription")]
    public TwitchSubscriptionInfo? Subscription { get; set; }

    [JsonPropertyName("event")]
    public TwitchStreamEvent? Event { get; set; }
}

public record TwitchSubscriptionInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // ex: "stream.online"
}

public record TwitchStreamEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; set; } = string.Empty;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; set; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; set; } // Heure de début envoyée par Twitch (en UTC)
}
