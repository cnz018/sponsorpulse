namespace SponsorPulse.Application.Common.Models;

/// <summary>
/// DTO contenant les données brutes Twitter et Twitch utilisées pour générer le storytelling.
/// </summary>
public record StorytellingRequest
{
    /// <summary>
    /// Données Twitter agrégées.
    /// </summary>
    public TwitterData Twitter { get; init; } = new();

    /// <summary>
    /// Données Twitch agrégées.
    /// </summary>
    public TwitchData Twitch { get; init; } = new();
}

/// <summary>
/// Représente les informations Twitter nécessaires au storytelling.
/// </summary>
public record TwitterData
{
    /// <summary>
    /// Liste des tweets pertinents.
    /// </summary>
    public List<Tweet> Tweets { get; init; } = new();

    /// <summary>
    /// Utilisateurs Twitter associés aux tweets.
    /// </summary>
    public List<TwitterUser> Users { get; init; } = new();

    /// <summary>
    /// Métriques agrégées Twitter.
    /// </summary>
    public TwitterMetrics Metrics { get; init; } = new();
}

/// <summary>
/// Tweet simplifié.
/// </summary>
public record Tweet(string Id, string Text, DateTime CreatedAt);

/// <summary>
/// Utilisateur Twitter simplifié.
/// </summary>
public record TwitterUser(string Id, string Username, string DisplayName);

/// <summary>
/// Métriques Twitter utiles pour le storytelling.
/// </summary>
public record TwitterMetrics
{
    public int TweetCount { get; init; }
    public double AverageEngagement { get; init; }
    public List<string> PopularHashtags { get; init; } = new();
}

/// <summary>
/// Représente les informations Twitch nécessaires au storytelling.
/// </summary>
public record TwitchData
{
    /// <summary>
    /// Liste des streams Twitch pertinents.
    /// </summary>
    public List<TwitchStreamLite> Streams { get; init; } = new();

    /// <summary>
    /// Métriques agrégées Twitch.
    /// </summary>
    public TwitchMetrics Metrics { get; init; } = new();
}

/// <summary>
/// Stream Twitch simplifié.
/// </summary>
public record TwitchStreamLite(string Id, string Title, DateTime StartedAt);

/// <summary>
/// Métriques Twitch utiles pour le storytelling.
/// </summary>
public record TwitchMetrics
{
    public int ViewerCount { get; init; }
    public double AverageWatchTime { get; init; }
    public double AverageEngagement { get; init; }
}