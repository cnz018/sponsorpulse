using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SponsorPulse.Infrastructure.Xpoz.Models;

/// <summary>
/// Modèles de réponse désérialisés depuis l'API Xpoz.ai
/// </summary>

public record XpozAuthor(
    string Id,
    string Username,
    string DisplayName,
    long FollowersCount,
    string? ProfileImageUrl
);

public record XpozMetrics(
    int Likes,
    int Retweets,
    int Replies,
    int Quotes,
    int Bookmarks,
    long Impressions
);

public record XpozTweet(
    string Id,
    string Text,
    XpozAuthor Author,
    DateTime CreatedAt,
    string? Url,
    XpozMetrics Metrics,
    string? Location,
    string? Language
);

public record XpozTwitterResponse(List<XpozTweet> Posts, int TotalCount, string? NextCursor)
{
    public static XpozTwitterResponse Empty => new([], 0, null);
}
