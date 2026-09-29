using System;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Services;
using SponsorPulse.Doimain.Enums;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Models.Twitch;
using SponsorPulse.Domain.Primitives;
using SponsorPulse.Infrastructure.Persistence;
using SponsorPulse.Infrastructure.Services;

namespace SponsorPulse_Functions.Twitch;

public class TwitchTrackerFunction(
    ILogger<TwitchTrackerFunction> logger,
    IDbContextFactory<SponsorPulseAnalyticsDbContext> analyticsDbFactory,
    IDbContextFactory<SponsorPulseDbContext> eventDbFactory,
    IPlatformTrackingStrategyResolver strategyResolver
)
{
    private readonly ILogger<TwitchTrackerFunction> _logger = logger;
    private readonly IDbContextFactory<SponsorPulseAnalyticsDbContext> _analyticsDbFactory =
        analyticsDbFactory;
    private readonly IDbContextFactory<SponsorPulseDbContext> _eventDbFactory = eventDbFactory;
    private readonly IPlatformTrackingStrategyResolver _strategyResolver = strategyResolver;
    private static StreamPlatform CurrentPlatform => StreamPlatform.Twitch;
    private static readonly TimeSpan StartTolerance = TimeSpan.FromMinutes(30);

    [Function("TwitchTrackerFunction")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.Subtract(StartTolerance);
        var windowEnd = now.Add(StartTolerance);

        using var eventDbContext = _eventDbFactory.CreateDbContext();
        var eventsToTrack = await eventDbContext
            .Events.Where(streamEvent =>
                streamEvent.StreamPlatform == "Twitch"
                && (
                    streamEvent.Status == EventStatus.Live
                    || (
                        streamEvent.Status == EventStatus.Scheduled
                        && streamEvent.StartedAt.HasValue
                        && streamEvent.StartedAt.Value >= windowStart
                        && streamEvent.StartedAt.Value <= windowEnd
                    )
                )
            )
            .ToListAsync();

        var strategy = _strategyResolver.GetStrategy(CurrentPlatform);
        foreach (var streamEvent in eventsToTrack)
        {
            try
            {
                var resultObject = await strategy.CaptureAsync(
                    streamEvent.BroadcasterId,
                    streamEvent.OwnerId
                );

                if (
                    resultObject is not Result<TwitchMetrics> result
                    || !result.IsSuccess
                    || result.Value is null
                )
                {
                    _logger.LogWarning(
                        "Could not capture Twitch metrics for event {EventId}.",
                        streamEvent.Id
                    );
                    continue;
                }

                var metrics = result.Value;
                if (!metrics.IsLive)
                {
                    if (streamEvent.Status == EventStatus.Live)
                    {
                        streamEvent.Status = EventStatus.Completed;
                        await eventDbContext.SaveChangesAsync();
                        _logger.LogInformation(
                            "Twitch stream ended; event {EventId} tracking is complete.",
                            streamEvent.Id
                        );
                    }

                    continue;
                }

                streamEvent.Status = EventStatus.Live;
                streamEvent.ViewerCount = metrics.ViewerCount;
                streamEvent.PeakViewers = Math.Max(
                    streamEvent.PeakViewers ?? 0,
                    metrics.ViewerCount
                );
                streamEvent.GameName = metrics.GameName;
                await eventDbContext.SaveChangesAsync();

                using var analyticsDbContext = _analyticsDbFactory.CreateDbContext();
                var snapshot = new TwitchStreamSnapshot
                {
                    EventId = streamEvent.Id,
                    StreamStartedAt = metrics.StartedAt.ToUniversalTime(),
                    ChannelName = streamEvent.BroadcasterId,
                    ViewerCount = metrics.ViewerCount,
                    PeakViewerCount = streamEvent.PeakViewers.Value,
                    GameName = metrics.GameName,
                    CapturedAt = DateTimeOffset.UtcNow,
                };
                analyticsDbContext.TwitchStreamSnapshots.Add(snapshot);
                await analyticsDbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Twitch snapshot {SnapshotId} recorded for event {EventId}.",
                    snapshot.Id,
                    streamEvent.Id
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Twitch tracking failed for event {EventId}.",
                    streamEvent.Id
                );
            }
        }
    }

    [Function("ForceTwitchEventTrackingFunction")]
    public async Task<HttpResponseData> ForceEventTracking(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "events/{eventId}/twitch/force")]
            HttpRequestData request,
        string eventId
    )
    {
        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            return request.CreateResponse(HttpStatusCode.BadRequest);
        }

        using var dbContext = _eventDbFactory.CreateDbContext();
        var streamEvent = await dbContext.Events.FirstOrDefaultAsync(candidate =>
            candidate.Id == parsedEventId && candidate.StreamPlatform == "Twitch"
        );
        if (streamEvent is null)
        {
            return request.CreateResponse(HttpStatusCode.NotFound);
        }

        streamEvent.Status = EventStatus.Live;
        await dbContext.SaveChangesAsync();

        return request.CreateResponse(HttpStatusCode.Accepted);
    }
}
