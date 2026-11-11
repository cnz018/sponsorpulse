using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Infrastructure.Persistence;

namespace SponsorPulse.Infrastructure.Api.Extensions;

public static class TwitchAuthExtensions
{
    public static WebApplication MapTwitchAuthEndpoints(this WebApplication app)
    {
        var handler = new TwitchAuthHandler();

        app.MapGet("/auth/twitch/callback", handler.HandleCallback).WithName("TwitchCallback");

        // Management endpoints for connected Twitch accounts
        app.MapGet(
                "/api/twitch/accounts",
                async (ITwitchAuthStateService stateService) =>
                {
                    var list = await stateService.ListAllAsync();
                    var dto = list.Select(t => new
                        {
                            twitchUserId = t.TwitchUserId,
                            scopes = t.Scopes,
                            expiresAt = t.ExpiresAt,
                        })
                        .ToList();

                    return Results.Ok(dto);
                }
            )
            .WithName("ListTwitchAccounts");

        app.MapGet(
                "/api/linkedaccounts",
                async (IDbContextFactory<Persistence.SponsorPulseDbContext> dbFactory) =>
                {
                    await using var dbContext = await dbFactory.CreateDbContextAsync();
                    var linkedAccounts = await dbContext
                        .LinkedAccounts.Select(l => new
                        {
                            l.Id,
                            l.UserId,
                            l.Platform,
                            l.PlatformUserId,
                            l.PlatformUsername,
                            l.AccessToken,
                            l.RefreshToken,
                            l.TokenExpiresAt,
                        })
                        .ToListAsync();

                    return Results.Ok(linkedAccounts);
                }
            )
            .WithName("ListLinkedAccounts");

        app.MapDelete(
                "/api/twitch/accounts/{twitchUserId}",
                async (string twitchUserId, ITwitchAuthStateService stateService) =>
                {
                    await stateService.RemoveByTwitchUserIdAsync(twitchUserId);

                    return Results.Ok(new { success = true });
                }
            )
            .WithName("DeleteTwitchAccount");

        // Enriched details (display name, login, avatar) for UX
        app.MapGet(
                "/api/twitch/accounts/details",
                async (
                    ITwitchAuthStateService stateService,
                    IHttpClientFactory httpFactory,
                    IConfiguration configuration
                ) =>
                {
                    var list = await stateService.ListAllAsync();
                    var result = new List<object>();

                    foreach (var t in list)
                    {
                        string? displayName = null;
                        string? login = null;
                        string? profileImage = null;

                        if (!string.IsNullOrEmpty(t.TwitchUserId))
                        {
                            var token = await stateService.GetValidAccessTokenAsync(t.TwitchUserId);

                            if (!string.IsNullOrEmpty(token))
                            {
                                try
                                {
                                    using var client = httpFactory.CreateClient();

                                    client.DefaultRequestHeaders.Remove("Client-ID");
                                    client.DefaultRequestHeaders.Add(
                                        "Client-ID",
                                        configuration["Twitch:ClientId"]
                                    );
                                    client.DefaultRequestHeaders.Remove("Authorization");
                                    client.DefaultRequestHeaders.Add(
                                        "Authorization",
                                        $"Bearer {token}"
                                    );
                                    string BaseUrl = configuration["Twitch:UserInfo"];
                                    var userResp = await client.GetAsync(
                                        $"{BaseUrl}/users?id={Uri.EscapeDataString(t.TwitchUserId)}"
                                    );

                                    if (userResp.IsSuccessStatusCode)
                                    {
                                        var doc =
                                            await userResp.Content.ReadFromJsonAsync<JsonDocument>();

                                        if (
                                            doc != null
                                            && doc.RootElement.TryGetProperty("data", out var data)
                                            && data.ValueKind == JsonValueKind.Array
                                            && data.GetArrayLength() > 0
                                        )
                                        {
                                            var first = data[0];
                                            if (
                                                first.TryGetProperty("display_name", out var dn)
                                                && dn.ValueKind == JsonValueKind.String
                                            )
                                                displayName = dn.GetString();
                                            if (
                                                first.TryGetProperty("login", out var lg)
                                                && lg.ValueKind == JsonValueKind.String
                                            )
                                                login = lg.GetString();
                                            if (
                                                first.TryGetProperty(
                                                    "profile_image_url",
                                                    out var pi
                                                )
                                                && pi.ValueKind == JsonValueKind.String
                                            )
                                                profileImage = pi.GetString();
                                        }
                                    }
                                }
                                catch
                                {
                                    // ignore profile enrich failures for UX
                                }
                            }
                        }

                        result.Add(
                            new
                            {
                                twitchUserId = t.TwitchUserId,
                                displayName,
                                login,
                                profileImageUrl = profileImage,
                                scopes = t.Scopes,
                                expiresAt = t.ExpiresAt,
                            }
                        );
                    }

                    return Results.Ok(result);
                }
            )
            .WithName("ListTwitchAccountsDetails");

        return app;
    }
}

public class TwitchAuthHandler
{
    private const string TwitchAuthorizationBaseUrl = "https://id.twitch.tv/oauth2/authorize";
    private const string TwitchTokenExchangeUrl = "https://id.twitch.tv/oauth2/token";

    public async Task<string> LoginAsync(
        ITwitchAuthStateService stateService,
        IConfiguration configuration,
        CancellationToken cancellationToken = default
    )
    {
        var clientId = configuration["Twitch:ClientId"];
        var redirectUri = configuration["Twitch:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
            return string.Empty;

        var state = await stateService.CreateStateAsync();
        var scopes = configuration["Twitch:Scopes"] ?? "user:read:email";

        return string.Format(
            "{0}?client_id={1}&redirect_uri={2}&response_type=code&scope={3}&state={4}",
            TwitchAuthorizationBaseUrl,
            Uri.EscapeDataString(clientId),
            Uri.EscapeDataString(redirectUri),
            Uri.EscapeDataString(scopes),
            Uri.EscapeDataString(state)
        );
    }

    public async Task<IResult> HandleCallback(
        HttpRequest request,
        IHttpClientFactory httpClientFactory,
        ITwitchAuthStateService stateService,
        IConfiguration configuration,
        ILogger<TwitchAuthHandler> logger,
        CancellationToken cancellationToken = default
    )
    {
        var code = request.Query["code"].FirstOrDefault();
        var state = request.Query["state"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return Results.BadRequest(new { error = "Missing code or state in callback." });

        var stateRecord = await stateService.FindByStateAsync(state);

        if (stateRecord is null)
            return Results.BadRequest(new { error = "Invalid state." });

        var clientId = configuration["Twitch:ClientId"];
        var clientSecret = configuration["Twitch:ClientSecret"];
        var redirectUri = configuration["Twitch:RedirectUri"];

        if (
            string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(redirectUri)
        )
            return Results.BadRequest(new { error = "Missing Twitch client configuration." });

        try
        {
            using var client = httpClientFactory.CreateClient();
            var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["code"] = code,
                    ["grant_type"] = "authorization_code",
                    ["redirect_uri"] = redirectUri,
                }
            );

            var tokenResponse = await client.PostAsync(
                TwitchTokenExchangeUrl,
                content,
                cancellationToken
            );

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var body = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

                logger.LogError(
                    "Token exchange failed: {Status} {Body}",
                    tokenResponse.StatusCode,
                    body
                );
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }

            var tokenData =
                await tokenResponse.Content.ReadFromJsonAsync<AuthorizationCodeTokenResponse>(
                    cancellationToken
                );

            if (tokenData is null || string.IsNullOrWhiteSpace(tokenData.AccessToken))
                return Results.StatusCode(StatusCodes.Status500InternalServerError);

            using var apiClient = httpClientFactory.CreateClient();
            apiClient.DefaultRequestHeaders.Remove("Client-ID");
            apiClient.DefaultRequestHeaders.Add("Client-ID", clientId);
            apiClient.DefaultRequestHeaders.Remove("Authorization");
            apiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenData.AccessToken}");

            var userInfoUrl =
                configuration["Twitch:UserInfo"] ?? "https://api.twitch.tv/helix/users";
            var userRequest = await apiClient.GetAsync(userInfoUrl, cancellationToken);

            if (!userRequest.IsSuccessStatusCode)
                return Results.BadRequest(new { error = "Failed to retrieve user information." });

            var userData = await userRequest.Content.ReadFromJsonAsync<UserDataWrapper>(
                cancellationToken
            );
            var userId = userData?.Data?.FirstOrDefault()?.Id;

            var tokenRecord = new TwitchAuthToken
            {
                State = state,
                AccessToken = tokenData.AccessToken,
                RefreshToken = tokenData.RefreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenData.ExpiresIn),
                TwitchUserId = userId,
                Scopes = tokenData.Scope is { Count: > 0 }
                    ? string.Join(' ', tokenData.Scope)
                    : null,
            };

            try
            {
                var principal = request.HttpContext?.User;
                var nameId =
                    principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? principal?.FindFirst("sub")?.Value;

                if (!string.IsNullOrWhiteSpace(nameId) && Guid.TryParse(nameId, out var appUserId))
                {
                    tokenRecord.UserId = appUserId;
                }
            }
            catch
            {
                // Non-fatal — linking is best-effort
            }

            await stateService.SaveAuthTokenAsync(tokenRecord);

            try
            {
                var platformUsername =
                    userData?.Data?.FirstOrDefault()?.Login ?? userData?.Data?.FirstOrDefault()?.Id;

                var linkedAccount = new LinkedAccount
                {
                    UserId = tokenRecord.UserId,
                    Platform = PlatformType.Twitch,
                    PlatformUserId = userId ?? string.Empty,
                    PlatformUsername = platformUsername ?? string.Empty,
                    AccessToken = tokenData.AccessToken,
                    RefreshToken = tokenData.RefreshToken,
                    TokenExpiresAt = DateTimeOffset
                        .UtcNow.AddSeconds(tokenData.ExpiresIn)
                        .UtcDateTime,
                };

                await stateService.UpsertLinkedAccountAsync(linkedAccount);

                if (!string.IsNullOrEmpty(userId))
                {
                    logger.LogInformation(
                        "Lancement automatique de l'abonnement EventSub pour l'utilisateur {UserId}...",
                        userId
                    );
                    await Task.Run(async () =>
                    {
                        try
                        {
                            bool isSubscribed =
                                await TwitchWebhookExtensions.SubscribeEventSubAsync(
                                    userId,
                                    httpClientFactory,
                                    configuration,
                                    logger
                                );

                            if (isSubscribed)
                            {
                                logger.LogInformation(
                                    "Abonnement EventSub automatisé avec succès pour l'utilisateur {UserId}.",
                                    userId
                                );
                            }
                            else
                            {
                                logger.LogWarning(
                                    "L'abonnement automatique a échoué lors du callback pour {UserId}. L'utilisateur devra cliquer sur 'Synchroniser' dans les paramètres.",
                                    userId
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(
                                ex,
                                "Erreur asynchrone lors de la tentative d'abonnement automatique pour {UserId}.",
                                userId
                            );
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist LinkedAccount for Twitch callback");
            }

            return Results.Redirect("/settings?platform=twitch&status=success", true, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in twitch callback");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<HttpResponseMessage> RevokeTokenAsync(
        string twitchUserId,
        IConfiguration configuration,
        IDbContextFactory<SponsorPulseDbContext> dbFactory,
        ILogger<TwitchAuthHandler>? logger = null,
        IHttpClientFactory? httpClientFactory = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(twitchUserId))
            return CreateErrorResponse(
                HttpStatusCode.BadRequest,
                "Missing twitchUserId parameter."
            );

        await using var dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);
        var tokenRecord = await dbContext.TwitchAuthTokens.FirstOrDefaultAsync(
            token => token.TwitchUserId == twitchUserId,
            cancellationToken
        );

        if (tokenRecord is null || string.IsNullOrWhiteSpace(tokenRecord.AccessToken))
            return CreateErrorResponse(
                HttpStatusCode.NotFound,
                "Twitch account not found or no access token."
            );

        var clientId = configuration["Twitch:ClientId"];
        var revocationUrl = configuration["Twitch:RevocationUrl"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(revocationUrl))
            return CreateErrorResponse(
                HttpStatusCode.BadRequest,
                "Missing Twitch client configuration."
            );

        try
        {
            using var client = httpClientFactory?.CreateClient() ?? new HttpClient();
            var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["token"] = tokenRecord.AccessToken,
                }
            );

            var revokeResponse = await client.PostAsync(revocationUrl, content, cancellationToken);

            if (!revokeResponse.IsSuccessStatusCode)
            {
                var body = await revokeResponse.Content.ReadAsStringAsync(cancellationToken);
                logger?.LogError(
                    "Token revocation failed: {Status} {Body}",
                    revokeResponse.StatusCode,
                    body
                );

                return revokeResponse;
            }

            dbContext.TwitchAuthTokens.Remove(tokenRecord);

            var linkedAccount = await dbContext.LinkedAccounts.FirstOrDefaultAsync(
                account =>
                    account.Platform == PlatformType.Twitch
                    && account.PlatformUserId == twitchUserId,
                cancellationToken
            );

            if (linkedAccount is not null)
                dbContext.LinkedAccounts.Remove(linkedAccount);

            await dbContext.SaveChangesAsync(cancellationToken);

            return revokeResponse;
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Error revoking Twitch token for user {TwitchUserId}",
                twitchUserId
            );
            return CreateErrorResponse(
                HttpStatusCode.InternalServerError,
                "Unable to revoke Twitch token."
            );
        }
    }

    public async Task<IResult> RevokeAsync(
        string twitchUserId,
        HttpRequest request,
        IHttpClientFactory httpClientFactory,
        ITwitchAuthStateService stateService,
        IConfiguration configuration,
        ILogger<TwitchAuthHandler> logger
    )
    {
        if (string.IsNullOrWhiteSpace(twitchUserId))
            return Results.BadRequest(new { error = "Missing twitchUserId parameter." });

        var tokenRecord = await stateService.FindByTwitchUserIdAsync(twitchUserId);

        if (tokenRecord is null || string.IsNullOrWhiteSpace(tokenRecord.AccessToken))
            return Results.NotFound(new { error = "Twitch account not found or no access token." });

        var clientId = configuration["Twitch:ClientId"];
        var revocationUrl = configuration["Twitch:RevocationUrl"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(revocationUrl))
            return Results.BadRequest(new { error = "Missing Twitch client configuration." });

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("content-type", "application/x-www-form-urlencoded");
            var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["token"] = tokenRecord.AccessToken,
                }
            );

            var revokeResponse = await client.PostAsync(revocationUrl, content);

            if (!revokeResponse.IsSuccessStatusCode)
            {
                var body = await revokeResponse.Content.ReadAsStringAsync();

                logger.LogError(
                    "Token revocation failed: {Status} {Body}",
                    revokeResponse.StatusCode,
                    body
                );

                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }

            await stateService.RemoveByTwitchUserIdAsync(twitchUserId);

            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error revoking Twitch token for user {TwitchUserId}",
                twitchUserId
            );
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static HttpResponseMessage CreateErrorResponse(
        HttpStatusCode statusCode,
        string message
    )
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { error = message }),
                Encoding.UTF8,
                "application/json"
            ),
        };

        return response;
    }

    private record AuthorizationCodeTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] List<string>? Scope,
        [property: JsonPropertyName("token_type")] string TokenType
    );

    private record UserDataWrapper([property: JsonPropertyName("data")] List<UserData> Data);

    private record UserData(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("login")] string? Login,
        [property: JsonPropertyName("display_name")] string? DisplayName
    );
}

internal class TwitchSubscriptionHelper
{
    internal static async Task SubscribeEventSubAsync(
        string userId,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TwitchAuthHandler> logger
    )
    {
        throw new NotImplementedException();
    }
}
