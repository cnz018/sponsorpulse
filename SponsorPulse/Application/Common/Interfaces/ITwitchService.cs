using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;

namespace SponsorPulse.Application.Common.Interfaces;

public interface ITwitchService
{
    // channelNameOrUrl: can be a channel login, a full twitch URL, or a video id/url.
    // userAccessToken: optional user OAuth token (Authorization Code flow) to access owner-only analytics.
    Task<Result<TwitchMetrics>> GetStreamMetricsAsync(
        string channelNameOrUrl,
        string? userAccessToken = null
    );

    // Analytics endpoints: return a CSV download URL (or error). If `userAccessToken` is not provided,
    // implementation may attempt to resolve a valid token via an injected auth state service using `ownerTwitchUserId`.
    Task<Result<string>> GetExtensionAnalyticsCsvUrlAsync(
        string extensionClientId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? userAccessToken = null,
        string? ownerTwitchUserId = null
    );
    Task<Result<string>> GetGameAnalyticsCsvUrlAsync(
        string gameId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? userAccessToken = null,
        string? ownerTwitchUserId = null
    );
    Task<Result<CsvTable>> GetExtensionAnalyticsAsync(
        string extensionClientId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? userAccessToken = null,
        string? ownerTwitchUserId = null
    );
    Task<Result<CsvTable>> GetGameAnalyticsAsync(
        string gameId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? userAccessToken = null,
        string? ownerTwitchUserId = null
    );
}
