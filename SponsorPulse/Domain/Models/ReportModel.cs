using SponsorPulse.Domain.Enums;
using SponsorPulse.Domain.Models.Twitch;

namespace SponsorPulse.Domain.Models;

/// <summary>
/// Modèle complet pour la génération de rapport PDF
/// </summary>
public record ReportModel
{
    // Identité de l'entreprise
    public string CompanyName { get; init; } = string.Empty;
    public string? LogoUrl { get; init; }
    public string PrimaryColor { get; init; } = "#6366F1";
    public string SecondaryColor { get; init; } = "#8B5CF6";

    // Données de l'événement
    public string EventName { get; init; } = string.Empty;
    public string EventDescription { get; init; } = string.Empty;
    public DateTime EventDate { get; init; }
    public string EventPlatform { get; init; } = string.Empty;
    public bool HasViewerCount { get; init; }
    public bool HasPeakViewers { get; init; }
    public bool HasStreamDuration { get; init; }

    // Metrics
    public TwitchMetrics? TwitchMetrics { get; init; }
    public TwitterAnalytics? TwitterAnalytics { get; init; }
    public List<TwitchStreamSnapshot> StreamSnapshots { get; init; } = [];
    public List<ReportSocialPost> SocialPosts { get; init; } = [];
    public bool HasStreamData =>
        HasViewerCount || HasPeakViewers || HasStreamDuration || StreamSnapshots.Count > 0;
    public bool HasSocialData => TwitterAnalytics is not null || SocialPosts.Count > 0;

    // Contenu généré
    public string StorytellingText { get; init; } = string.Empty;
    public string AnalysisText { get; init; } = string.Empty;
    public List<StorytellingReponse.Slide> StorytellingSlides { get; init; } = new();
    public List<string> OpportunitiesMissed { get; init; } = new();
    public List<string> Suggestions { get; init; } = new();

    // Médias
    public List<string> PhotoUrls { get; init; } = new();

    // Date du rapport
    public DateTime ReportDate => TimeProvider.System.GetUtcNow().DateTime;
}

public record ReportSocialPost(
    SocialPlatform Platform,
    string AuthorName,
    string Text,
    string? Url,
    DateTime CreatedAt,
    long Impressions,
    int Engagement,
    string AuthorHandle = "",
    int Likes = 0,
    int Shares = 0,
    int Comments = 0
);
