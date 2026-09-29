using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SponsorPulse.Infrastructure.Services;

public sealed class TwitchAuthenticationHandler(
    TwitchAppTokenProvider appTokenProvider,
    ILinkedAccountManager linkedAccountManager,
    IConfiguration configuration,
    ILogger<TwitchAuthenticationHandler> logger
) : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<Guid> UserIdOption = new("TwitchUserId");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var clientId = configuration["Twitch:ClientId"] ?? configuration["Twitch_ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Twitch application client ID is not configured.");
        }

        string accessToken;
        try
        {
            accessToken = await appTokenProvider.GetValidAccessTokenAsync(cancellationToken);
        }
        catch (TwitchAppCredentialsMissingException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (!request.Options.TryGetValue(UserIdOption, out var userId))
            {
                logger.LogError(
                    exception,
                    "Twitch app token acquisition failed; no user fallback is available."
                );
                throw;
            }

            logger.LogWarning(
                exception,
                "Twitch app token acquisition failed; using the linked user token for {UserId}.",
                userId
            );
            accessToken =
                await linkedAccountManager.GetValidTwitchAccessTokenAsync(userId)
                ?? throw new InvalidOperationException(
                    "No valid linked Twitch user token is available.",
                    exception
                );
        }

        request.Headers.Remove("Client-Id");
        request.Headers.TryAddWithoutValidation("Client-Id", clientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await base.SendAsync(request, cancellationToken);
    }
}

public sealed class TwitchAppTokenProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration
)
{
    private static readonly TimeSpan RefreshBeforeExpiry = TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (HasValidToken())
        {
            return _accessToken!;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (HasValidToken())
            {
                return _accessToken!;
            }

            var clientId = configuration["Twitch:ClientId"] ?? configuration["Twitch_ClientId"];
            var clientSecret =
                configuration["Twitch:ClientSecret"] ?? configuration["Twitch_ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new TwitchAppCredentialsMissingException();
            }

            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["grant_type"] = "client_credentials",
                }
            );
            using var response = await httpClientFactory
                .CreateClient("TwitchOAuth")
                .PostAsync("https://id.twitch.tv/oauth2/token", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TwitchAppTokenResponse>(
                cancellationToken: cancellationToken
            );
            if (
                token is null
                || string.IsNullOrWhiteSpace(token.AccessToken)
                || token.ExpiresIn <= 0
            )
            {
                throw new InvalidOperationException("Twitch returned an invalid app access token.");
            }

            _accessToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool HasValidToken() =>
        !string.IsNullOrWhiteSpace(_accessToken)
        && _expiresAt > DateTimeOffset.UtcNow.Add(RefreshBeforeExpiry);

    private sealed record TwitchAppTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );
}

public sealed class TwitchAppCredentialsMissingException()
    : InvalidOperationException("Twitch application credentials are not configured.");
