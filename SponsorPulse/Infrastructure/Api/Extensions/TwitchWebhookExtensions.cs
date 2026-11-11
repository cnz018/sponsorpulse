using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Infrastructure.Persistence; // Ton DbContext SQLite
using SponsorPulse.Models;

namespace SponsorPulse.Infrastructure.Api.Extensions;

public static class TwitchWebhookExtensions
{
    private const int DEFAUTL_TOLERANCE_MINUTES = 30;
    private static readonly string STREAM_ONLINE = "stream.online";
    private static readonly string STREAM_OFFLINE = "stream.offline";

    private record SubscribeRequest(string BroadcasterUserId);

    public static WebApplication MapTwitchWebhookEndpoints(this WebApplication app)
    {
        app.MapPost(
                "/api/twitch/subscribe",
                async (
                    SubscribeRequest request,
                    IHttpClientFactory httpFactory,
                    IConfiguration configuration,
                    ILogger<Program> logger
                ) =>
                {
                    if (string.IsNullOrEmpty(request.BroadcasterUserId))
                    {
                        return Results.BadRequest(
                            new { error = "L'ID Twitch du streamer est requis." }
                        );
                    }

                    logger.LogInformation(
                        "Relance manuelle de l'abonnement EventSub demandée pour : {Id}",
                        request.BroadcasterUserId
                    );

                    var success = await SubscribeEventSubAsync(
                        request.BroadcasterUserId,
                        httpFactory,
                        configuration,
                        logger
                    );

                    return success
                        ? Results.Ok(new { success = true })
                        : Results.BadRequest(
                            new { error = "L'abonnement auprès de Twitch a échoué." }
                        );
                }
            )
            .WithName("RetryTwitchSubscription");

        app.MapGet(
            "/api/webhooks/twitch",
            (ILogger<Program> logger) => logger.LogInformation("Requete recu dans le get")
        );

        app.MapPost(
            "/api/webhooks/twitch",
            async (
                TwitchWebhookPayload payload,
                IDbContextFactory<SponsorPulseDbContext> dbFactory,
                IConfiguration configuration,
                ILogger<Program> logger
            ) =>
            {
                logger.LogInformation("Requete reçu");
                // 1. Gestion du challenge de validation de Twitch
                if (!string.IsNullOrEmpty(payload.Challenge))
                {
                    logger.LogInformation("Validation du challenge Twitch réussie.");
                    // Twitch demande de renvoyer le challenge sous forme de texte brut avec un statut 200 OK
                    return Results.Text(payload.Challenge, "text/plain");
                }

                if (string.IsNullOrEmpty(payload.Subscription?.Type) || payload.Event is null)
                {
                    logger.LogWarning(
                        "Webhook reçu pour un type d'événement inattendu ou événement null : {Type} Event: {Event}",
                        payload.Subscription?.Type,
                        payload.Event.BroadcasterUserId
                    );

                    return Results.BadRequest();
                }

                var streamEvent = payload.Event;
                if (payload.Subscription?.Type == STREAM_OFFLINE)
                {
                    logger.LogInformation(
                        "Le streamer {Name} (ID: {Id}) a coupé son live.",
                        streamEvent.BroadcasterUserName,
                        streamEvent.BroadcasterUserId
                    );

                    // On recherche l'événement qui est actuellement actif ("EnDirect") pour ce streamer
                    using var dbContext1 = await dbFactory.CreateDbContextAsync();
                    var activeEvent = await dbContext1
                        .Events.Include(e => e.Owner)
                        .Include(e => e.Owner!.LinkedAccounts)
                        .FirstOrDefaultAsync(e =>
                            e.Owner!.LinkedAccounts.Any(la =>
                                la.PlatformUserId == streamEvent.BroadcasterUserId
                            )
                            && e.Status == EventStatus.Live
                        );

                    if (activeEvent is null)
                    {
                        logger.LogWarning(
                            "Le live de {Name} s'est arrêté, mais aucun événement 'EnDirect' n'était enregistré pour lui en base.",
                            streamEvent.BroadcasterUserName
                        );

                        return Results.NotFound();
                    }

                    logger.LogInformation(
                        "Événement en direct trouvé. Clôture de l'événement '{Title}' (ID: {EventId}).",
                        activeEvent.Name,
                        activeEvent.Id
                    );

                    // On fait basculer le statut à Terminé
                    activeEvent.Status = EventStatus.Completed;

                    // Sauvegarde du changement de statut dans la base SQLite locale
                    await dbContext1.SaveChangesAsync();

                    logger.LogInformation(
                        "L'événement '{Title}' a été marqué comme Terminé avec succès.",
                        activeEvent.Name
                    );

                    return Results.Ok();
                }
                // 2. Traitement de l'événement stream.online

                logger.LogInformation(
                    "Le streamer {Name} (ID: {Id}) a lancé un live à {Time}",
                    streamEvent.BroadcasterUserName,
                    streamEvent.BroadcasterUserId,
                    streamEvent.StartedAt
                );

                // 3. Récupération de la tolérance configurée (par défaut 30 minutes si absent du appsettings)
                int toleranceMinutes = configuration.GetValue<int>(
                    "Twitch:Webhook:ToleranceMinutes",
                    DEFAUTL_TOLERANCE_MINUTES
                );

                // 4. Recherche de l'événement correspondant en base de données
                // On cherche un événement "Planifié" pour ce streamer spécifique
                using var dbContext = await dbFactory.CreateDbContextAsync();
                var candidateEvents = await dbContext
                    .Events.AsSplitQuery()
                    .Include(e => e.Owner)
                    .Include(e => e.Owner.LinkedAccounts)
                    .SelectMany(
                        e => e.Owner.LinkedAccounts,
                        (e, la) => new { Event = e, LinkedAccount = la }
                    )
                    .Where(e =>
                        e.Event.StartedAt.HasValue
                        && e.Event.StartedAt.Value
                            >= streamEvent.StartedAt.AddMinutes(-toleranceMinutes)
                        && e.Event.StartedAt.Value
                            <= streamEvent.StartedAt.AddMinutes(toleranceMinutes)
                        && e.LinkedAccount.PlatformUserId == streamEvent.BroadcasterUserId
                        && e.Event.Status == EventStatus.Scheduled
                    )
                    .ToListAsync();

                Event? matchingEvent = null;

                foreach (var ev in candidateEvents)
                {
                    // On calcule la différence de temps absolue entre le début réel du live et l'heure planifiée
                    var timeDifference = (
                        streamEvent.StartedAt - ev.Event.StartedAt!.Value
                    ).Duration();

                    if (timeDifference.TotalMinutes <= toleranceMinutes)
                    {
                        matchingEvent = ev.Event;
                        break; // Match trouvé, on arrête la recherche
                    }
                }

                // 5. Mise à jour de l'événement s'il correspond
                if (matchingEvent is null)
                {
                    logger.LogWarning(
                        "Aucun événement planifié ne correspond au live de {Name} dans la fenêtre de +/- {Tolerance} minutes.",
                        streamEvent.BroadcasterUserName,
                        toleranceMinutes
                    );

                    return Results.Accepted(); // On retourne quand même 200 OK à Twitch pour accuser réception
                }

                logger.LogInformation(
                    "Match trouvé ! L'événement '{Title}' (ID: {EventId}) passe en direct.",
                    matchingEvent.Name,
                    matchingEvent.Id
                );

                matchingEvent.Status = EventStatus.Live;

                // On enregistre les modifications dans SQLite
                await dbContext.SaveChangesAsync();

                // TODO: Déclencher ici ton moteur de capture de statistiques toutes les 5 minutes

                // Twitch exige toujours un retour 200 OK (ou 204) rapide pour accuser réception
                return Results.Ok();
            }
        );

        return app;
    }

    public static async Task<bool> SubscribeEventSubAsync(
        string broadcasterUserId,
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger logger
    )
    {
        try
        {
            using var client = httpFactory.CreateClient();
            var clientId = configuration["Twitch:ClientId"];
            var clientSecret = configuration["Twitch:ClientSecret"];
            var tokenUrl = configuration["Twitch:TokenUrl"];
            var webhookBaseUrl = configuration["Twitch:Webhook:CallbackUrl"];
            var webhookSubscriptionUrl = configuration["Twitch:Webhook:SubscriptionUrl"];

            if (
                string.IsNullOrEmpty(clientId)
                || string.IsNullOrEmpty(clientSecret)
                || string.IsNullOrEmpty(webhookBaseUrl)
            )
            {
                logger.LogError(
                    "La configuration de l'application est incomplète (ClientId, ClientSecret ou WebhookBaseUrl introuvables)."
                );
                return false;
            }

            // Étape A : Récupérer un App Access Token auprès de Twitch (Client Credentials Flow obligatoire pour EventSub)
            var tokenPayload = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["grant_type"] = "client_credentials",
                }
            );

            var tokenResponse = await client.PostAsync(tokenUrl, tokenPayload);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Impossible d'obtenir un jeton d'application auprès de Twitch. Statut: {Code}",
                    tokenResponse.StatusCode
                );
                return false;
            }

            using var tokenDoc = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
            var appAccessToken = tokenDoc?.RootElement.GetProperty("access_token").GetString();

            if (string.IsNullOrEmpty(appAccessToken))
            {
                logger.LogError("Le jeton récupéré auprès de Twitch est vide.");
                return false;
            }

            // Étape B : Configurer les en-têtes requis pour appeler Helix (l'API Twitch)
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Client-ID", clientId);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                appAccessToken
            );

            string callbackUrl = $"{webhookBaseUrl.TrimEnd('/')}/api/webhooks/twitch";
            string webhookSecret =
                configuration["Twitch:WebhookSecret"] ?? "ChaineSecuriseeParDefaut123!";
            string[] topics = { STREAM_ONLINE, STREAM_OFFLINE };

            bool standardSuccess = true;

            // Étape C : Envoyer une demande d'abonnement pour chaque topic (Online et Offline)
            foreach (var topic in topics)
            {
                var subscriptionBody = new
                {
                    type = topic,
                    version = "1",
                    condition = new { broadcaster_user_id = broadcasterUserId },
                    transport = new
                    {
                        method = "webhook",
                        callback = callbackUrl,
                        secret = webhookSecret,
                    },
                };

                var response = await client.PostAsJsonAsync(
                    webhookSubscriptionUrl,
                    subscriptionBody
                );

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation(
                        "Demande d'abonnement EventSub enregistrée pour le topic '{Topic}'. Attente du challenge Twitch...",
                        topic
                    );
                }
                else
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    logger.LogError(
                        "Échec de la demande d'abonnement pour '{Topic}'. Code HTTP: {Code}. Détails: {Details}",
                        topic,
                        response.StatusCode,
                        errorDetails
                    );
                    standardSuccess = false;
                }
            }

            return standardSuccess;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Une exception s'est produite lors de la configuration des abonnements EventSub."
            );
            return false;
        }
    }
}
