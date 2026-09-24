namespace SponsorPulse.Domain.Entities;

using SponsorPulse.Domain.Models;

public enum EventStatus
{
    Draft,
    Fetching,
    AnalysisReady,
    Completed,
    Error,
    Live,
    Scheduled,
}

public record Event(
    Guid Id,
    string Name,
    string Description,
    DateTime Date,
    string StreamPlatform,
    string BroadcasterId,
    string Slug
)
{
    // Owner (user) - FK
    public Guid OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    // Twitch Metrics
    public int? ViewerCount { get; set; }
    public int? PeakViewers { get; set; }
    public TimeSpan? StreamDuration { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? GameName { get; set; }

    // Twitter Data
    public string? TwitterHashtags { get; set; }
    public DateTime? SocialStartDate { get; set; }
    public DateTime? SocialEndDate { get; set; }
    public TwitterAnalytics? TwitterAnalytics { get; set; }

    // Media
    public List<EventMedia> Media { get; set; } = new();

    private const int MAX_SLUG_LENGTH = 7;
    private const int SLUG_CONVERSION_FACTOR = 62;

    public static Event Create(
        string name,
        string description,
        DateTime date,
        string platform,
        string broadcasterId
    )
    {
        // Utilisation de GUID v7 pour des IDs triables chronologiquement
        var slug = GenerateSlug();
        return new Event(
            Guid.CreateVersion7(),
            name,
            description,
            date,
            platform,
            broadcasterId,
            slug
        );
    }

    private static string GenerateSlug()
    {
        // Base62 encoding of milliseconds since Jan 1 2025
        var chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var epoch = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var diff = DateTime.UtcNow - epoch;
        long value = (long)diff.TotalMilliseconds;

        if (value < 0)
            value = 0; // Should not happen given the epoch

        var sb = new System.Text.StringBuilder();
        do
        {
            sb.Insert(0, chars[(int)(value % SLUG_CONVERSION_FACTOR)]);
            value /= SLUG_CONVERSION_FACTOR;
        } while (value > 0);

        // Ensure max 7 chars (although it fits for ~100 years)
        var result = sb.ToString();
        return result.Length > MAX_SLUG_LENGTH
            ? result.Substring(result.Length - MAX_SLUG_LENGTH)
            : result;
    }
}
