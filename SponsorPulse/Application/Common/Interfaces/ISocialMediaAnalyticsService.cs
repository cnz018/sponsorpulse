using SponsorPulse.Domain.Enums;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;

namespace SponsorPulse.Application.Common.Interfaces;

public interface ISocialMediaAnalyticsService
{
    Task<Result<SocialFetchResult>> GetPlatformMetricsAsync(
        SocialPlatform platform,
        string query,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    );
}
