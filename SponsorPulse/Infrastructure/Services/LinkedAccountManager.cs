using System;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Domain.Enums;
using SponsorPulse.Infrastructure.Persistence;

namespace SponsorPulse.Infrastructure.Services;

public interface ILinkedAccountManager
{
    Task<LinkedAccount?> GetAccountAsync(Guid userId, PlatformType platformType);
    Task<LinkedAccount?> FindTwitchLinkedAccountAsync(Guid userId);
    Task<string?> GetValidTwitchAccessTokenAsync(Guid userId);
    Task<bool> UnlinkAccountAsync(Guid userId, PlatformType platformType);
}

public sealed class LinkedAccountManager(
    IDbContextFactory<SponsorPulseDbContext> dbContextFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<LinkedAccountManager> logger
) : ILinkedAccountManager
{
    private readonly IDbContextFactory<SponsorPulseDbContext> _dbContextFactory = dbContextFactory;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<LinkedAccountManager> _logger = logger;

    public async Task<LinkedAccount?> GetAccountAsync(Guid userId, PlatformType platformType)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.LinkedAccounts.FirstOrDefaultAsync(a =>
            a.UserId == userId && a.Platform == platformType
        );
    }

    public async Task<LinkedAccount?> FindTwitchLinkedAccountAsync(Guid userId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.LinkedAccounts.FirstOrDefaultAsync(a =>
            a.Platform == PlatformType.Twitch && a.UserId == userId
        );
    }

    public async Task<string?> GetValidTwitchAccessTokenAsync(Guid userId)
    {
        var account = await FindTwitchLinkedAccountAsync(userId);

        if (account is null)
        {
            return null;
        }

        var refreshNeeded =
            !account.TokenExpiresAt.HasValue
            || account.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(1);

        if (!refreshNeeded)
        {
            return account.AccessToken;
        }

        if (string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            if (string.IsNullOrWhiteSpace(account.AccessToken))
            {
                return null;
            }

            _logger.LogWarning(
                "Twitch token for user {UserId} is expired and has no refresh token.",
                userId
            );
            return account.AccessToken;
        }

        var clientId = _configuration["Twitch:ClientId"] ?? _configuration["Twitch_ClientId"];
        var clientSecret =
            _configuration["Twitch:ClientSecret"] ?? _configuration["Twitch_ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogError("Twitch client credentials are missing; token refresh skipped.");
            return account.AccessToken;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = account.RefreshToken,
                }
            );

            using var response = await client.PostAsync(
                "https://id.twitch.tv/oauth2/token",
                content
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Twitch token refresh failed for user {UserId}: {StatusCode}",
                    userId,
                    response.StatusCode
                );
                return account.AccessToken;
            }

            var token = await response.Content.ReadFromJsonAsync<TwitchRefreshTokenResponse>();

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                _logger.LogWarning(
                    "Twitch returned an invalid refreshed token for user {UserId}.",
                    userId
                );
                return account.AccessToken;
            }

            using var dbContext = _dbContextFactory.CreateDbContext();
            var storedAccount = await dbContext.LinkedAccounts.FirstOrDefaultAsync(a =>
                a.Id == account.Id
            );

            if (storedAccount is null)
            {
                return account.AccessToken;
            }

            storedAccount.AccessToken = token.AccessToken;
            storedAccount.RefreshToken = token.RefreshToken ?? account.RefreshToken;
            storedAccount.TokenExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
            await dbContext.SaveChangesAsync();

            return storedAccount.AccessToken;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Twitch token refresh failed for user {UserId}.", userId);
            return account.AccessToken;
        }
    }

    public async Task<bool> UnlinkAccountAsync(Guid userId, PlatformType platformType)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var account = await dbContext.LinkedAccounts.FirstOrDefaultAsync(a =>
            a.UserId == userId && a.Platform == platformType
        );

        if (account is null)
        {
            return false;
        }

        dbContext.LinkedAccounts.Remove(account);
        await dbContext.SaveChangesAsync();

        return true;
    }

    private sealed record TwitchRefreshTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );
}
