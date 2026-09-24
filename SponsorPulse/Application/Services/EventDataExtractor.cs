using Microsoft.EntityFrameworkCore;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Common.Models;
using SponsorPulse.Infrastructure.Persistence;

namespace SponsorPulse.Application.Services;

public sealed class EventDataExtractor(
    IDbContextFactory<SponsorPulseDbContext> eventDbContextFactory,
    IDbContextFactory<SponsorPulseAnalyticsDbContext> analyticsDbContextFactory
) : IEventDataExtractor
{
    public async Task<EventRawData?> ExtractAsync(
        string slug,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var normalizedSlug = slug.Trim();

        await using var eventDbContext = await eventDbContextFactory.CreateDbContextAsync(
            cancellationToken
        );

        var eventData = await eventDbContext
            .Events.AsNoTracking()
            .Where(item => item.Slug.ToUpper() == normalizedSlug.ToUpper())
            .Select(item => new EventRawEvent(
                item.Id,
                item.Name,
                item.Description,
                item.Date,
                item.StreamPlatform,
                item.BroadcasterId,
                item.Slug,
                item.OwnerId,
                item.Status,
                item.ViewerCount,
                item.PeakViewers,
                item.StreamDuration,
                item.StartedAt,
                item.GameName,
                item.TwitterHashtags,
                item.SocialStartDate,
                item.SocialEndDate,
                item.TwitterAnalytics
            ))
            .SingleOrDefaultAsync(cancellationToken);

        if (eventData is null)
            return null;

        var media = await eventDbContext
            .Events.AsNoTracking()
            .Where(item => item.Slug.ToUpper() == normalizedSlug.ToUpper())
            .SelectMany(item => item.Media)
            .Select(item => new EventRawMedia(
                item.Id,
                item.OriginalFileName,
                item.StorageFileName,
                item.Url,
                item.Size,
                item.ContentType,
                item.UploadedAt
            ))
            .ToListAsync(cancellationToken);

        await using var analyticsDbContext = await analyticsDbContextFactory.CreateDbContextAsync(
            cancellationToken
        );

        var socialPosts = await analyticsDbContext
            .SocialPosts.AsNoTracking()
            .Where(item => item.EventId == eventData.Id)
            .Select(item => new EventRawSocialPost(
                item.Id,
                item.EventId,
                item.Platform,
                item.AuthorId,
                item.AuthorName,
                item.AuthorHandle,
                item.AuthorFollowersCount,
                item.ImpressionsCount,
                item.AuthorProfileImageUrl,
                item.ContentText,
                item.CreatedAt,
                item.Url,
                item.LikesCount,
                item.SharesCount,
                item.CommentsCount,
                item.RawJsonPayload,
                item.PlatformSpecificDataJson,
                item.FetchedAt
            ))
            .ToListAsync(cancellationToken);

        var twitchSnapshots = await analyticsDbContext
            .TwitchStreamSnapshots.AsNoTracking()
            .Where(item => item.EventId == eventData.Id)
            .Select(item => new EventRawTwitchSnapshot(
                item.Id,
                item.EventId,
                item.ChannelName,
                item.ViewerCount,
                item.PeakViewerCount,
                item.GameName,
                item.ChatMessageCount,
                item.CapturedAt
            ))
            .ToListAsync(cancellationToken);

        return new EventRawData(eventData, media, socialPosts, twitchSnapshots);
    }
}
