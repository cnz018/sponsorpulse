using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Infrastructure.Persistence;

namespace SponsorPulse.Infrastructure.Services;

public class TwitchAuthStateManager(
    IMemoryCache memoryCache,
    IDbContextFactory<SponsorPulseDbContext> dbFactory,
    ILogger<TwitchAuthStateManager> logger,
    IHttpClientFactory httpFactory,
    IConfiguration configuration
) : ITwitchAuthStateService
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly IDbContextFactory<SponsorPulseDbContext> _dbFactory = dbFactory;
    private readonly ILogger<TwitchAuthStateManager> _logger = logger;
    private readonly IHttpClientFactory _httpFactory = httpFactory;
    private readonly IConfiguration _configuration = configuration;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<string> CreateStateAsync()
    {
        var state = Guid.NewGuid().ToString("N");
        var token = new TwitchAuthToken { State = state, CreatedAt = DateTimeOffset.UtcNow };

        await PersistTokenAsync(token);

        return state;
    }

    public async Task SaveAuthTokenAsync(TwitchAuthToken token)
    {
        await PersistTokenAsync(token);
        if (!string.IsNullOrEmpty(token.TwitchUserId))
        {
            _memoryCache.Remove(GetTwitchUserKey(token.TwitchUserId));
        }
    }

    public async Task<TwitchAuthToken?> FindByStateAsync(string state)
    {
        using var dbContext = _dbFactory.CreateDbContext();

        return await dbContext.TwitchAuthTokens.FirstOrDefaultAsync(t => t.State == state);
    }

    public async Task<TwitchAuthToken?> FindByTwitchUserIdAsync(string twitchUserId)
    {
        using var dbContext = _dbFactory.CreateDbContext();

        return await dbContext.TwitchAuthTokens.FirstOrDefaultAsync(t =>
            t.TwitchUserId == twitchUserId
        );
    }

    public async Task<string?> GetValidAccessTokenAsync(string twitchUserId)
    {
        var token = await FindByTwitchUserIdAsync(twitchUserId);

        if (token is null)
            return null;

        if (token.ExpiresAt.HasValue && token.ExpiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(1))
            return token.AccessToken;

        if (string.IsNullOrEmpty(token.RefreshToken))
            return token.AccessToken;

        try
        {
            var clientId = _configuration["Twitch:ClientId"];
            var clientSecret = _configuration["Twitch:ClientSecret"];
            using var client = _httpFactory.CreateClient();
            var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = token.RefreshToken!,
                }
            );

            var resp = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to refresh Twitch token for user {User}: {Status}",
                    twitchUserId,
                    resp.StatusCode
                );
                return token.AccessToken;
            }

            var data = await resp.Content.ReadFromJsonAsync<RefreshTokenResponse>();

            if (data is null || string.IsNullOrEmpty(data.AccessToken))
                return token.AccessToken;

            token.AccessToken = data.AccessToken;
            token.RefreshToken = data.RefreshToken ?? token.RefreshToken;
            token.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(data.ExpiresIn);

            await PersistTokenAsync(token);

            return token.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception refreshing Twitch token for {User}", twitchUserId);
            return token.AccessToken;
        }
    }

    public async Task<string?> GetValidAccessTokenForUserAsync(Guid userId)
    {
        using var dbContext = _dbFactory.CreateDbContext();
        var token = await dbContext.TwitchAuthTokens.SingleOrDefaultAsync(t => t.UserId == userId);

        return token?.TwitchUserId is null
            ? null
            : await GetValidAccessTokenAsync(token.TwitchUserId);
    }

    public async Task<List<TwitchAuthToken>> ListAllAsync()
    {
        using var dbContext = _dbFactory.CreateDbContext();

        return await dbContext.TwitchAuthTokens.AsNoTracking().ToListAsync();
    }

    public async Task RemoveByTwitchUserIdAsync(string twitchUserId)
    {
        using var dbContext = _dbFactory.CreateDbContext();
        var existing = await dbContext.TwitchAuthTokens.FirstOrDefaultAsync(t =>
            t.TwitchUserId == twitchUserId
        );

        if (existing is not null)
        {
            dbContext.TwitchAuthTokens.Remove(existing);
            await dbContext.SaveChangesAsync();
        }

        _memoryCache.Remove(GetTwitchUserKey(twitchUserId));
        _memoryCache.Remove(GetLinkedAccountKey(PlatformType.Twitch, twitchUserId));
    }

    public async Task UpsertLinkedAccountAsync(LinkedAccount linkedAccount)
    {
        using var dbContext = _dbFactory.CreateDbContext();
        var existing = await dbContext.LinkedAccounts.FirstOrDefaultAsync(l =>
            l.Platform == linkedAccount.Platform && l.PlatformUserId == linkedAccount.PlatformUserId
        );

        if (existing is not null)
        {
            existing.PlatformUsername = linkedAccount.PlatformUsername;
            existing.AccessToken = linkedAccount.AccessToken;
            existing.RefreshToken = linkedAccount.RefreshToken;
            existing.TokenExpiresAt = linkedAccount.TokenExpiresAt;
            existing.UserId = linkedAccount.UserId;

            dbContext.LinkedAccounts.Update(existing);

            linkedAccount = existing;
        }
        else
        {
            dbContext.LinkedAccounts.Add(linkedAccount);
        }

        await dbContext.SaveChangesAsync();
        _memoryCache.Set(
            GetLinkedAccountKey(linkedAccount.Platform, linkedAccount.PlatformUserId),
            linkedAccount,
            CacheDuration
        );
    }

    public async Task<LinkedAccount?> FindLinkedAccountAsync(
        PlatformType platform,
        string platformUserId
    )
    {
        var cacheKey = GetLinkedAccountKey(platform, platformUserId);

        if (_memoryCache.TryGetValue(cacheKey, out LinkedAccount? cached))
        {
            return cached;
        }

        using var dbContext = _dbFactory.CreateDbContext();
        var linkedAccount = await dbContext.LinkedAccounts.FirstOrDefaultAsync(l =>
            l.Platform == platform && l.PlatformUserId == platformUserId
        );

        if (linkedAccount is not null)
        {
            _memoryCache.Set(cacheKey, linkedAccount, CacheDuration);
        }

        return linkedAccount;
    }

    private async Task PersistTokenAsync(TwitchAuthToken token)
    {
        using var dbContext = _dbFactory.CreateDbContext();
        var existing = await dbContext.TwitchAuthTokens.FirstOrDefaultAsync(t =>
            t.State == token.State
        );

        if (existing is not null)
        {
            existing.AccessToken = token.AccessToken;
            existing.RefreshToken = token.RefreshToken;
            existing.ExpiresAt = token.ExpiresAt;
            existing.TwitchUserId = token.TwitchUserId;
            existing.Scopes = token.Scopes;
            dbContext.TwitchAuthTokens.Update(existing);
        }
        else
        {
            dbContext.TwitchAuthTokens.Add(token);
        }

        await dbContext.SaveChangesAsync();
    }

    private static string GetTwitchUserKey(string twitchUserId) => $"TwitchToken:{twitchUserId}";

    private static string GetLinkedAccountKey(PlatformType platform, string platformUserId) =>
        $"LinkedAccount:{platform}:{platformUserId}";

    private record RefreshTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );
}
