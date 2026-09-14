using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Common.Models;
using SponsorPulse.Application.Services;
using SponsorPulse.Doimain.Enums;
using SponsorPulse.Domain.Models.Twitch;
using SponsorPulse.Domain.Primitives;
using SponsorPulse.Infrastructure.Persistence;
using SponsorPulse.Infrastructure.Services;


namespace SponsorPulse_Functions.Twitch;

public class TwitchTrackerFunction(ILogger<TwitchTrackerFunction> logger,
IDbContextFactory<SponsorPulseAnalyticsDbContext> dbContextFactory, 
IPlatformTrackingStrategyResolver strategyResolver,
IConfiguration configuration)
{
    private readonly ILogger<TwitchTrackerFunction> _logger = logger;
    private readonly IDbContextFactory<SponsorPulseAnalyticsDbContext> _dbFactory = dbContextFactory;
    private readonly IPlatformTrackingStrategyResolver _strategyResolver = strategyResolver;
    private readonly IConfiguration configuration = configuration;
    private static StreamPlatform CurrentPlatform => StreamPlatform.Twitch;

    /// <summary>
    /// S'exécute automatiquement selon le planning défini par l'expression CRON.
    /// Format CRON (6 champs) : {secondes} {minutes} {heures} {jours} {mois} {jours-semaine}
    /// Ex: "*/10 * * * * *" = Toutes les 10 secondes.
    /// En production pour le live : "0 */5 * * * *" = Toutes les 5 minutes.
    /// </summary>
    [Function("TwitchTrackerFunction")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
    {
        string targetChannel = configuration["Twitch_TargetChannel"] ?? "kenBogard";

        _logger.LogInformation("[TwitchTracker] Analyse du live pour : {Channel}", targetChannel);

        try
        {
            // 1. Récupération des métriques via ton service existant
            IPlatformTrackingStrategy strategy = _strategyResolver.GetStrategy(CurrentPlatform);

            // 2. Capture des métriques via la stratégie résolue
            var resultObject = await strategy.CaptureAsync(targetChannel);

            if (resultObject is Result<TwitchMetrics> result && result.IsSuccess && result.Value is not null)
            {
                var metrics = result.Value;

                var snapshot = new TwitchStreamSnapshot
                {
                    ChannelName = targetChannel,
                    ViewerCount = metrics.ViewerCount,
                    GameName = "Non spécifié",
                    CapturedAt = DateTimeOffset.UtcNow
                };

                using var dbContext = _dbFactory.CreateDbContext();

                dbContext.TwitchStreamSnapshots.Add(snapshot);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Snapshot {platform} enregistré ! (ID: {Id})", CurrentPlatform, snapshot.Id);
            }
            else
            {
                _logger.LogWarning("Impossible d'obtenir les métriques live pour {Channel}.", targetChannel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erreur critique lors de l'exécution du tracking.");
        }
    }
}