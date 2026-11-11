namespace SponsorPulse.Domain.Models;

/// <summary>
/// Représente les données de chat pendant un stream.
/// </summary>
public record ChatMetrics(
    int TotalMessages,
    int UniqueChatters,
    int MessagesPerMinute,
    double EmoteUsageRate,
    List<string> TopEmotes
);

/// <summary>
/// Représente les métriques de rétention des viewers.
/// </summary>
public record ViewerRetention(
    double AverageRetentionRate,
    double PeakRetentionRate,
    TimeSpan AverageWatchTime,
    double CompletionRate
);

/// <summary>
/// Représente les mentions et l'exposition d'un sponsor pendant un stream.
/// </summary>
public record SponsorExposure(
    int MentionCount,
    TimeSpan TotalExposureTime,
    int LogoImpressions,
    double BrandSentimentScore
);

/// <summary>
/// Représente les métriques de conversion liées au stream.
/// </summary>
public record ConversionMetrics(
    int Clicks,
    int Visits,
    double ConversionRate,
    decimal RevenuePerViewer,
    int NewFollowers
);

/// <summary>
/// Métriques complètes d'un stream Twitch pour le reporting sponsor.
/// </summary>
public record TwitchMetrics
{
    public int ViewerCount { get; init; }
    public bool IsLive { get; init; } = true;
    public int PeakViewers { get; init; }
    public int UniqueViewers { get; init; }
    public TimeSpan StreamDuration { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public string GameName { get; init; } = string.Empty;
    public ChatMetrics Chat { get; init; } = new(0, 0, 0, 0, []);
    public ViewerRetention Retention { get; init; } = new(0, 0, TimeSpan.Zero, 0);
    public SponsorExposure Sponsor { get; init; } = new(0, TimeSpan.Zero, 0, 0);
    public ConversionMetrics Conversion { get; init; } = new(0, 0, 0, 0, 0);
    public double AvgViewers { get; init; }
    public double ViewerHours { get; init; }
    public double EngagementRate { get; init; }
    public decimal EstimatedROI { get; init; }
    public decimal CPM { get; init; }

    public static TwitchMetrics Empty => new() { IsLive = false };

    public static TwitchMetrics Create() => new();

    public static TwitchMetrics Create(
        int viewerCount,
        int peakViewers,
        int uniqueViewers,
        TimeSpan streamDuration,
        DateTimeOffset startedAt,
        string gameName
    ) =>
        new()
        {
            ViewerCount = viewerCount,
            PeakViewers = peakViewers,
            UniqueViewers = uniqueViewers,
            StreamDuration = streamDuration,
            StartedAt = startedAt,
            GameName = gameName,
        };
}
