using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SponsorPulse.Application.Common.Models;
using SponsorPulse.Application.Services;
using SponsorPulse.Doimain.Enums;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Domain.Models.Twitch;
using SponsorPulse.Domain.Primitives;
using SponsorPulse.Infrastructure.Persistence;

namespace SponsorPulse.Functions;

public class ManualTwitchTrackingFunction(
    IPlatformTrackingStrategyResolver strategyResolver,
    IDbContextFactory<SponsorPulseAnalyticsDbContext> dbFactory,
    ILogger<ManualTwitchTrackingFunction> logger
)
{
    private readonly IPlatformTrackingStrategyResolver _strategyResolver = strategyResolver;
    private readonly IDbContextFactory<SponsorPulseAnalyticsDbContext> _dbFactory = dbFactory;
    private readonly ILogger<ManualTwitchTrackingFunction> _logger = logger;
    private static StreamPlatform CurrentPlatform => StreamPlatform.Twitch;
    private static JsonSerializerOptions _jsonOptions =>
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Endpoint HTTP permettant de déclencher manuellement la collecte pour un canal donné.
    /// URL appelée : POST http://localhost:7075/api/collect/twitch/
    /// </summary>
    [Function("ManualTwitchTrackingFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "collect/twitch")]
            HttpRequestData req
    )
    {
        _logger.LogInformation(
            "[Trigger Manuel] Exécution demandée pour le canal Twitch via l'endpoint manuel."
        );

        if (req.Body is null)
        {
            var badbodyResponse = req.CreateResponse(HttpStatusCode.BadRequest);

            _logger.LogWarning("[Trigger Manuel] Requête reçue sans corps pour le canal Twitch.");

            await badbodyResponse.WriteStringAsync("Le corps de la requête est vide.");

            return badbodyResponse;
        }

        string? broadcasterId = null;
        Guid? requestUserId = null;

        try
        {
            if (req.Body.CanSeek)
            {
                req.Body.Position = 0;
            }

            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(body))
            {
                var request = JsonSerializer.Deserialize<TrackingRequest>(body, _jsonOptions);
                broadcasterId = request?.BroadcasterId;
                requestUserId = request?.UserId;

                _logger.LogInformation(
                    "[Trigger Manuel] Corps JSON lu pour le canal Twitch. broadcasterId présent: {HasBroadcasterId}",
                    !string.IsNullOrWhiteSpace(broadcasterId)
                );
                _logger.LogInformation(
                    "[Trigger Manuel] Corps JSON lu pour le canal Twitch. userId présent: {HasUserId}",
                    requestUserId.HasValue
                );
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[Trigger Manuel] Corps JSON invalide pour le canal Twitch.");
        }

        broadcasterId ??= req.Query["broadcasterId"];

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            _logger.LogWarning(
                "[Trigger Manuel] Requête reçue sans broadcasterId pour le canal Twitch."
            );

            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync(
                "Le paramètre broadcasterId est obligatoire."
            );

            return badRequestResponse;
        }

        _logger.LogInformation("[Trigger Manuel] Requête reçue complète pour le canal Twitch.");
        _logger.LogInformation("[Trigger Manuel] broadcasterId: {BroadcasterId}", broadcasterId);
        _logger.LogInformation("[Trigger Manuel] userId: {UserId}", requestUserId);
        // 1. Obtenir la stratégie Twitch via le resolver
        var strategy = _strategyResolver.GetStrategy(CurrentPlatform);
        var resultObject = await strategy.CaptureAsync(broadcasterId, requestUserId);

        if (
            resultObject is not Result<TwitchMetrics> result
            || !result.IsSuccess
            || result.Value is null
        )
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);

            await badResponse.WriteStringAsync("Impossible de récupérer les métriques Twitch.");

            return badResponse;
        }

        var metrics = result.Value;

        // 2. Créer et enregistrer le snapshot analytique
        var snapshot = new TwitchStreamSnapshot
        {
            ChannelName = broadcasterId,
            ViewerCount = metrics.ViewerCount,
            GameName = "Non spécifié",
            CapturedAt = DateTimeOffset.UtcNow,
        };

        using var dbContext = _dbFactory.CreateDbContext();

        dbContext.TwitchStreamSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Snapshot manuel sauvegardé en BDD (ID: {Id})", snapshot.Id);

        // 3. Réponse HTTP 200 OK
        var response = req.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(metrics);

        return response;
    }

    private sealed record TrackingRequest(string? BroadcasterId, Guid? UserId);
}
