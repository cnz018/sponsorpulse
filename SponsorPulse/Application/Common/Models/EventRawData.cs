using SponsorPulse.Domain.Entities;
using SponsorPulse.Domain.Enums;
using SponsorPulse.Domain.Models;

namespace SponsorPulse.Application.Common.Models;

public record EventRawData(
    EventRawEvent Event,
    IReadOnlyList<EventRawMedia> Media,
    IReadOnlyList<EventRawSocialPost> SocialPosts,
    IReadOnlyList<EventRawTwitchSnapshot> TwitchSnapshots
);

public record EventRawEvent(
    Guid Id,
    string Name,
    string Description,
    DateTime Date,
    string StreamPlatform,
    string BroadcasterId,
    string Slug,
    Guid OwnerId,
    EventStatus Status,
    int? ViewerCount,
    int? PeakViewers,
    TimeSpan? StreamDuration,
    DateTime? StartedAt,
    string? GameName,
    string? TwitterHashtags,
    DateTime? SocialStartDate,
    DateTime? SocialEndDate,
    TwitterAnalytics? TwitterAnalytics
);

public record EventRawMedia(
    Guid Id,
    string OriginalFileName,
    string StorageFileName,
    string Url,
    long Size,
    string ContentType,
    DateTime UploadedAt
);

public record EventRawSocialPost(
    Guid Id,
    Guid EventId,
    SocialPlatform Platform,
    string AuthorId,
    string AuthorName,
    string AuthorHandle,
    long AuthorFollowersCount,
    long ImpressionsCount,
    string? AuthorProfileImageUrl,
    string ContentText,
    DateTime CreatedAt,
    string? Url,
    int LikesCount,
    int SharesCount,
    int CommentsCount,
    string RawJsonPayload,
    string PlatformSpecificDataJson,
    DateTime FetchedAt
);

public record EventRawTwitchSnapshot(
    int Id,
    Guid EventId,
    string ChannelName,
    int ViewerCount,
    int PeakViewerCount,
    string GameName,
    int ChatMessageCount,
    DateTimeOffset CapturedAt
);
