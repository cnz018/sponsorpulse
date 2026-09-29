using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Models.Twitch;

namespace SponsorPulse.Application.Services;

public sealed class TwitchMetricCalculator
{
    public TwitchMetrics Calculate(IEnumerable<TwitchStreamSnapshot> snapshots)
    {
        var orderedSnapshots = snapshots.OrderBy(snapshot => snapshot.CapturedAt).ToList();
        if (orderedSnapshots.Count == 0)
        {
            return TwitchMetrics.Empty;
        }

        var averageViewers = orderedSnapshots.Average(snapshot => snapshot.ViewerCount);
        var peakViewers = orderedSnapshots.Max(snapshot =>
            Math.Max(snapshot.PeakViewerCount, snapshot.ViewerCount)
        );
        var streamStartedAt =
            orderedSnapshots
                .Select(snapshot => snapshot.StreamStartedAt)
                .FirstOrDefault(startedAt => startedAt.HasValue) ?? orderedSnapshots[0].CapturedAt;
        var streamEndedAt = orderedSnapshots[^1].CapturedAt;
        var viewerHours = 0d;

        for (var index = 1; index < orderedSnapshots.Count; index++)
        {
            var previousSnapshot = orderedSnapshots[index - 1];
            var elapsedHours = (
                orderedSnapshots[index].CapturedAt - previousSnapshot.CapturedAt
            ).TotalHours;
            viewerHours += previousSnapshot.ViewerCount * Math.Max(0, elapsedHours);
        }

        var lastSnapshot = orderedSnapshots[^1];
        var totalChatMessages = orderedSnapshots.Sum(snapshot => snapshot.ChatMessageCount);

        return new TwitchMetrics
        {
            IsLive = false,
            ViewerCount = (int)Math.Round(averageViewers),
            PeakViewers = peakViewers,
            StreamDuration =
                streamEndedAt > streamStartedAt ? streamEndedAt - streamStartedAt : TimeSpan.Zero,
            StartedAt = streamStartedAt,
            GameName = lastSnapshot.GameName,
            Chat = new ChatMetrics(totalChatMessages, 0, 0, 0, []),
            AvgViewers = averageViewers,
            ViewerHours = viewerHours,
        };
    }
}
