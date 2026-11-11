using SponsorPulse.Domain.Enums;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;

namespace SponsorPulse.Infrastructure.Xpoz.Strategies;

public interface IXpozPlatformStrategy
{
    SocialPlatform Platform { get; }

    Task<Result<SocialFetchResult>> FetchAndFormatDataAsync(
        string query,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    );
}
