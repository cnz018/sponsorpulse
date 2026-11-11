using SponsorPulse.Domain.Enums;

namespace SponsorPulse.Infrastructure.Persistence;

public class SocialPostEntity
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public SocialPlatform Platform { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorHandle { get; set; } = string.Empty;
    public long AuthorFollowersCount { get; set; }
    public long ImpressionsCount { get; set; }
    public string? AuthorProfileImageUrl { get; set; }
    public string ContentText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Url { get; set; }
    public int LikesCount { get; set; }
    public int SharesCount { get; set; }
    public int CommentsCount { get; set; }
    public string RawJsonPayload { get; set; } = string.Empty;
    public string PlatformSpecificDataJson { get; set; } = "{}";
    public DateTime FetchedAt { get; set; }
}
