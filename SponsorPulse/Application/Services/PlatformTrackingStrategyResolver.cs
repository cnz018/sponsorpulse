using System;
using System.Collections.Generic;
using System.Linq;
using SponsorPulse.Doimain.Enums;
using SponsorPulse.Infrastructure.Services;

namespace SponsorPulse.Application.Services;

public interface IPlatformTrackingStrategyResolver
{
    IPlatformTrackingStrategy GetStrategy(StreamPlatform platform);
}

public class PlatformTrackingStrategyResolver(IEnumerable<IPlatformTrackingStrategy> strategies)
    : IPlatformTrackingStrategyResolver
{
    private readonly IEnumerable<IPlatformTrackingStrategy> _strategies = strategies;

    public IPlatformTrackingStrategy GetStrategy(StreamPlatform platform)
    {
        var strategy = _strategies.FirstOrDefault(s => s.Platform == platform);

        return strategy
            ?? throw new NotSupportedException(
                $"Aucune stratégie de tracking enregistrée pour la plateforme : {platform}"
            );
    }
}
