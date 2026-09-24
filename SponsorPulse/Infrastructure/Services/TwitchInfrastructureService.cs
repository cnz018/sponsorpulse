using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;

namespace SponsorPulse.Infrastructure.Services;

public class TwitchInfrastructureService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<TwitchInfrastructureService> logger,
    ITwitchAuthStateService authStateService
) : ITwitchService
{
    private readonly ITwitchAuthStateService _authStateService = authStateService;
    private const string BaseUrl = "https://api.twitch.tv/helix";
    private const string AuthUrl = "https://id.twitch.tv/oauth2/token";

    public async Task<Result<TwitchMetrics>> GetStreamMetricsAsync(
        string channelNameOrUrl,
        string? userAccessToken = null
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(channelNameOrUrl))
            {
                logger.LogWarning("Channel name cannot be empty.");

                return Result<TwitchMetrics>.Failure("Channel name  cannot be empty.");
            }

            var clientId = configuration["Twitch:ClientId"];
            var clientSecret = configuration["Twitch:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                logger.LogError("Twitch credentials are missing.");

                return Result<TwitchMetrics>.Failure("Configuration error. Missing credentials.");
            }

            // 1. Setup HttpClient and authentication headers
            using var client = httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Remove("Client-ID");
            client.DefaultRequestHeaders.Add("Client-ID", clientId);

            // If a user access token is provided (Authorization Code flow), use it to access owner-only analytics.
            if (!string.IsNullOrEmpty(userAccessToken))
            {
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {userAccessToken}");
            }
            else
            {
                // App access (Client Credentials)
                using var authClient = httpClientFactory.CreateClient();
                var authResponse = await authClient.PostAsync(
                    $"{AuthUrl}?client_id={clientId}&client_secret={clientSecret}&grant_type=client_credentials",
                    null
                );

                if (!authResponse.IsSuccessStatusCode)
                {
                    logger.LogError(
                        "Authentication failed. Status: {StatusCode}",
                        authResponse.StatusCode
                    );

                    return Result<TwitchMetrics>.Failure("Authentication failed.");
                }

                var authData = await authResponse.Content.ReadFromJsonAsync<TwitchAuthResponse>();

                if (string.IsNullOrEmpty(authData?.AccessToken))
                {
                    return Result<TwitchMetrics>.Failure("Failed to retrieve Access Token.");
                }

                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authData.AccessToken}");
            }

            string broadcasterId = "kenbogard";

            logger.LogInformation(
                "[TwitchTracker] Analyse du live pour : {Channel}",
                broadcasterId
            );
            var streamReq = await client.GetAsync($"{BaseUrl}/streams?user_id={broadcasterId}");

            if (!streamReq.IsSuccessStatusCode)
            {
                logger.LogInformation(" No LIVE stream for {Channel}", channelNameOrUrl);

                return Result<TwitchMetrics>.Failure("No active stream");
            }

            var streamData = await streamReq.Content.ReadFromJsonAsync<TwitchStreamResponse>();
            var liveStream = streamData?.Data?.FirstOrDefault();

            if (liveStream is null)
            {
                logger.LogInformation(" No data stream for {Channel}", channelNameOrUrl);

                return Result<TwitchMetrics>.Failure("No active stream");
            }

            logger.LogInformation("Found LIVE stream for {Channel}", channelNameOrUrl);

            return Result<TwitchMetrics>.Success(
                new TwitchMetrics
                {
                    ViewerCount = liveStream.ViewerCount,
                    PeakViewers = liveStream.ViewerCount, // Live peak is current
                    StreamDuration = DateTimeOffset.UtcNow - liveStream.StartedAt,
                    StartedAt = liveStream.StartedAt,
                    GameName = liveStream.GameName,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in Twitch service.");

            return Result<TwitchMetrics>.Failure(ex.Message);
        }
    }

    private static TimeSpan ParseDuration(string duration)
    {
        // Simple parser for 1h30m20s format.
        // Using XmlConvert is a robust trick for 'PT' formatted durations, but Twitch format is slightly different (without PT)
        // However, Twitch duration is like "3h20m5s". Adding PT makes it standard ISO8601 for XmlConvert.
        try
        {
            // Normalize: 3h20m -> PT3H20M
            var normalized = "PT" + duration.ToUpper();
            return System.Xml.XmlConvert.ToTimeSpan(normalized);
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    private static (string? login, string? videoId) ParseTwitchInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (null, null);

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            var seg = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (seg.Length >= 1)
            {
                // URL like /videos/{id}
                if (seg[0].Equals("videos", StringComparison.OrdinalIgnoreCase) && seg.Length > 1)
                    return (null, seg[1]);

                // Otherwise assume last segment is the channel login
                return (seg[^1], null);
            }

            return (null, null);
        }

        // If input is numeric, treat as video id
        if (long.TryParse(input, out _))
            return (null, input);

        return (input.Trim(), null);
    }

    public async Task<Result<string>> GetExtensionAnalyticsCsvUrlAsync(
        string extensionClientId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? userAccessToken = null,
        string? ownerTwitchUserId = null
    )
    {
        try
        {
            if (string.IsNullOrEmpty(userAccessToken) && !string.IsNullOrEmpty(ownerTwitchUserId))
            {
                userAccessToken = await _authStateService.GetValidAccessTokenAsync(
                    ownerTwitchUserId
                );
            }

            if (string.IsNullOrEmpty(userAccessToken))
                return Result<string>.Failure(
                    "User access token required to retrieve extension analytics."
                );

            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Remove("Client-ID");
            client.DefaultRequestHeaders.Add("Client-ID", configuration["Twitch:ClientId"]);
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {userAccessToken}");

            var url =
                $"{BaseUrl}/analytics/extensions?extension_id={Uri.EscapeDataString(extensionClientId)}&started_at={Uri.EscapeDataString(startedAt.ToString("O"))}&ended_at={Uri.EscapeDataString(endedAt.ToString("O"))}";
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                return Result<string>.Failure($"Analytics endpoint returned {resp.StatusCode}");
            }

            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            if (doc == null)
                return Result<string>.Failure("Empty analytics response.");

            // Try to find a URL in the JSON response
            if (
                doc.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array
                && data.GetArrayLength() > 0
            )
            {
                var first = data[0];
                if (
                    first.TryGetProperty("url", out var urlProp)
                    && urlProp.ValueKind == JsonValueKind.String
                )
                {
                    return Result<string>.Success(urlProp.GetString()!);
                }
            }

            // Fallback: search for any 'url' property recursively
            string? found = FindUrlInJson(doc.RootElement);
            return found != null
                ? Result<string>.Success(found)
                : Result<string>.Failure("No CSV URL found in analytics response.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching extension analytics");
            return Result<string>.Failure(ex.Message);
        }
    }

    public async Task<Result<string>> GetGameAnalyticsCsvUrlAsync(
        string gameId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? userAccessToken = null,
        string? ownerTwitchUserId = null
    )
    {
        try
        {
            if (string.IsNullOrEmpty(userAccessToken) && !string.IsNullOrEmpty(ownerTwitchUserId))
            {
                userAccessToken = await _authStateService.GetValidAccessTokenAsync(
                    ownerTwitchUserId
                );
            }

            if (string.IsNullOrEmpty(userAccessToken))
                return Result<string>.Failure(
                    "User access token required to retrieve game analytics."
                );

            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Remove("Client-ID");
            client.DefaultRequestHeaders.Add("Client-ID", configuration["Twitch:ClientId"]);
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {userAccessToken}");

            var url =
                $"{BaseUrl}/analytics/games?game_id={Uri.EscapeDataString(gameId)}&started_at={Uri.EscapeDataString(startedAt.ToString("O"))}&ended_at={Uri.EscapeDataString(endedAt.ToString("O"))}";
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                return Result<string>.Failure($"Analytics endpoint returned {resp.StatusCode}");
            }

            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            if (doc == null)
                return Result<string>.Failure("Empty analytics response.");

            if (
                doc.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array
                && data.GetArrayLength() > 0
            )
            {
                var first = data[0];
                if (
                    first.TryGetProperty("url", out var urlProp)
                    && urlProp.ValueKind == JsonValueKind.String
                )
                {
                    return Result<string>.Success(urlProp.GetString()!);
                }
            }

            string? found = FindUrlInJson(doc.RootElement);
            return found != null
                ? Result<string>.Success(found)
                : Result<string>.Failure("No CSV URL found in analytics response.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching game analytics");
            return Result<string>.Failure(ex.Message);
        }
    }

    public async Task<Result<CsvTable>> GetExtensionAnalyticsAsync(
        string extensionClientId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? userAccessToken = null,
        string? ownerTwitchUserId = null
    )
    {
        var urlResult = await GetExtensionAnalyticsCsvUrlAsync(
            extensionClientId,
            startedAt,
            endedAt,
            userAccessToken,
            ownerTwitchUserId
        );
        if (!urlResult.IsSuccess)
            return Result<CsvTable>.Failure(urlResult.ErrorMessage ?? "Unknown error");

        var csvUrl = urlResult.Value;
        using var req = new HttpRequestMessage(HttpMethod.Get, csvUrl);
        if (!string.IsNullOrEmpty(userAccessToken))
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                userAccessToken
            );

        using var client = httpClientFactory.CreateClient();
        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return Result<CsvTable>.Failure($"Failed to download CSV: {resp.StatusCode}");

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Encoding encoding = Encoding.UTF8;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            encoding = Encoding.UTF8;
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            encoding = Encoding.Unicode;

        var csvText = encoding.GetString(bytes);
        var table = ParseCsv(csvText);
        return Result<CsvTable>.Success(table);
    }

    public async Task<Result<CsvTable>> GetGameAnalyticsAsync(
        string gameId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? userAccessToken = null,
        string? ownerTwitchUserId = null
    )
    {
        var urlResult = await GetGameAnalyticsCsvUrlAsync(
            gameId,
            startedAt,
            endedAt,
            userAccessToken,
            ownerTwitchUserId
        );
        if (!urlResult.IsSuccess)
            return Result<CsvTable>.Failure(urlResult.ErrorMessage ?? "Unknown error");

        var csvUrl = urlResult.Value;
        using var req = new HttpRequestMessage(HttpMethod.Get, csvUrl);
        if (!string.IsNullOrEmpty(userAccessToken))
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                userAccessToken
            );

        using var client = httpClientFactory.CreateClient();
        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return Result<CsvTable>.Failure($"Failed to download CSV: {resp.StatusCode}");

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Encoding encoding = Encoding.UTF8;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            encoding = Encoding.UTF8;
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            encoding = Encoding.Unicode;

        var csvText = encoding.GetString(bytes);
        var table = ParseCsv(csvText);
        return Result<CsvTable>.Success(table);
    }

    private CsvTable ParseCsv(string csvText)
    {
        var lines = csvText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var table = new CsvTable
        {
            Raw = csvText,
            Headers = new List<string>(),
            Rows = new List<List<string>>(),
        };

        int idx = 0;
        while (idx < lines.Length && string.IsNullOrWhiteSpace(lines[idx]))
            idx++;
        if (idx >= lines.Length)
            return table;

        table.Headers.AddRange(SplitCsvLine(lines[idx]));
        idx++;

        for (; idx < lines.Length; idx++)
        {
            var line = lines[idx];
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = SplitCsvLine(line).ToList();
            table.Rows.Add(cols);
        }

        return table;
    }

    private IEnumerable<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private static string? FindUrlInJson(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (prop.NameEquals("url") && prop.Value.ValueKind == JsonValueKind.String)
                    return prop.Value.GetString();

                var nested = FindUrlInJson(prop.Value);
                if (nested != null)
                    return nested;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                var nested = FindUrlInJson(item);
                if (nested != null)
                    return nested;
            }
        }

        return null;
    }

    // DTOs
    record TwitchAuthResponse([property: JsonPropertyName("access_token")] string AccessToken);

    record TwitchUserResponse([property: JsonPropertyName("data")] List<UserData> Data);

    record UserData([property: JsonPropertyName("id")] string Id);

    record TwitchStreamResponse([property: JsonPropertyName("data")] List<StreamData> Data);

    record StreamData(
        [property: JsonPropertyName("viewer_count")] int ViewerCount,
        [property: JsonPropertyName("started_at")] DateTimeOffset StartedAt,
        [property: JsonPropertyName("game_name")] string GameName
    );

    record TwitchVideoResponse([property: JsonPropertyName("data")] List<VideoData> Data);

    record VideoData(
        [property: JsonPropertyName("view_count")] int ViewCount,
        [property: JsonPropertyName("duration")] string Duration,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt
    );
}
