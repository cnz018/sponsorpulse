using System;

namespace SponsorPulse.Domain.Models.Twitch;

/// <summary>
/// Représente une capture instantanée des métriques d'un stream Twitch.
/// </summary>
public class TwitchStreamSnapshot
{
    /// <summary>
    /// Identifiant unique .
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Identifiant unique de l'event .
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Nom du canal Twitch suivi (ex: 'kameto').
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de spectateurs instantané (vi).
    /// </summary>
    public int ViewerCount { get; set; }

    /// <summary>
    /// Peak de spectateurs durant le stream (vi max).
    /// </summary>
    public int PeakViewerCount { get; set; }

    /// <summary>
    /// Heure réelle de début du stream communiquée par Twitch.
    /// </summary>
    public DateTimeOffset? StreamStartedAt { get; set; }

    /// <summary>
    /// Nom du jeu ou de la catégorie (ex: 'Just Chatting', 'League of Legends').
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de messages enregistrés depuis le dernier relevé (pour le calcul du CDR).
    /// </summary>
    public int ChatMessageCount { get; set; }

    /// <summary>
    /// Horodatage exact de la capture en UTC.
    /// </summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}
