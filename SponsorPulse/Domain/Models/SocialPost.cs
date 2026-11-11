using SponsorPulse.Domain.Enums;

namespace SponsorPulse.Domain.Models;

public class SocialPost
{
    public Guid Id { get; init; }
    public Guid EventId { get; set; }
    public SocialPlatform Platform { get; init; }
    public string AuthorId { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorHandle { get; init; } = string.Empty;
    public long AuthorFollowersCount { get; init; }
    public string? AuthorProfileImageUrl { get; init; }
    public string ContentText { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string? Url { get; init; }
    public int LikesCount { get; init; }
    public int SharesCount { get; init; }
    public int CommentsCount { get; init; }
    public long ImpressionsCount { get; init; }
    public string RawJsonPayload { get; init; } = string.Empty;
    public Dictionary<string, object> PlatformSpecificData { get; init; } = [];
}
