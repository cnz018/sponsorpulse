using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Infrastructure.Persistence;

namespace SponsorPulse.Infrastructure.Services;

public class TwitchAuthStateService(
    IDbContextFactory<SponsorPulseDbContext> dbFactory,
    ILogger<TwitchAuthStateService> logger,
    IHttpClientFactory httpFactory,
    IConfiguration configuration
) : ITwitchAuthStateService
{
    private readonly IDbContextFactory<SponsorPulseDbContext> _dbFactory = dbFactory;
    private readonly ILogger<TwitchAuthStateService> _logger = logger;
    private readonly IHttpClientFactory _httpFactory = httpFactory;
    private readonly IConfiguration _configuration = configuration;

    public async Task<string> CreateStateAsync()
    {
        var state = Guid.NewGuid().ToString("N");
        var token = new TwitchAuthToken { State = state, CreatedAt = DateTimeOffset.UtcNow };

        using var dbcontext = _dbFactory.CreateDbContext();

        dbcontext.TwitchAuthTokens.Add(token);
        await dbcontext.SaveChangesAsync();

        return state;
    }

    public async Task SaveAuthTokenAsync(TwitchAuthToken token)
    {
        using var dbContext = _dbFactory.CreateDbContext();

        var existing = await dbContext.TwitchAuthTokens.SingleOrDefaultAsync(t =>
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

    public async Task<TwitchAuthToken?> FindByStateAsync(string state)
    {
        using var dbContext = _dbFactory.CreateDbContext();
        return await dbContext.TwitchAuthTokens.SingleOrDefaultAsync(t => t.State == state);
    }

    public async Task<TwitchAuthToken?> FindByTwitchUserIdAsync(string twitchUserId)
    {
        using var dbContext = _dbFactory.CreateDbContext();

        return await dbContext.TwitchAuthTokens.SingleOrDefaultAsync(t =>
            t.TwitchUserId == twitchUserId
        );
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
    }

    public async Task UpsertLinkedAccountAsync(LinkedAccount linkedAccount)
    {
        using var dbContext = _dbFactory.CreateDbContext();
        var existing = await dbContext.LinkedAccounts.SingleOrDefaultAsync(l =>
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
        }
        else
        {
            dbContext.LinkedAccounts.Add(linkedAccount);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<LinkedAccount?> FindLinkedAccountAsync(
        PlatformType platform,
        string platformUserId
    )
    {
        using var dbContext = _dbFactory.CreateDbContext();

        return await dbContext.LinkedAccounts.FirstOrDefaultAsync(l =>
            l.Platform == platform && l.PlatformUserId == platformUserId
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
            return token.AccessToken; // cannot refresh

        // Attempt refresh
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
                return token.AccessToken; // return old if present
            }

            var data = await resp.Content.ReadFromJsonAsync<RefreshTokenResponse>();

            if (data is { AccessToken.Length: 0 })
                return token.AccessToken;

            // Update token record
            token.AccessToken = data!.AccessToken;
            token.RefreshToken = data.RefreshToken ?? token.RefreshToken;
            token.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(data.ExpiresIn);

            await SaveAuthTokenAsync(token);

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

    private record RefreshTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );
}
