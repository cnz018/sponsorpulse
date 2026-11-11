using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Domain.Enums;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;
using SponsorPulse.Infrastructure.Repositories;
using SponsorPulse.Infrastructure.Xpoz.Strategies;

namespace SponsorPulse.Infrastructure.Services;

public class SocialMediaAnalyticsService(
    IEnumerable<IXpozPlatformStrategy> strategies,
    ISocialPostRepository postRepository
) : ISocialMediaAnalyticsService
{
    public async Task<Result<SocialFetchResult>> GetPlatformMetricsAsync(
        SocialPlatform platform,
        string query,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    )
    {
        var strategy = strategies.SingleOrDefault(candidate => candidate.Platform == platform);
        if (strategy is null)
        {
            return Result<SocialFetchResult>.Failure(
                $"Aucune stratégie n'est enregistrée pour la plateforme {platform}."
            );
        }

        var result = await strategy.FetchAndFormatDataAsync(
            query,
            startDate,
            endDate,
            cancellationToken
        );
        if (!result.IsSuccess || result.Value is null)
        {
            return result;
        }

        await postRepository.SavePostsAsync(result.Value.Posts, cancellationToken);
        return result;
    }
}
