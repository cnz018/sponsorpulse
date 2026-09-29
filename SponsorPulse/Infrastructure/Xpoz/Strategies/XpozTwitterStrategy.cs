using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SponsorPulse.Application.Common.Configuration;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Domain.Enums;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;
using SponsorPulse.Infrastructure.Xpoz.Models;

namespace SponsorPulse.Infrastructure.Xpoz.Strategies;

public class XpozTwitterStrategy(
    HttpClient httpClient,
    IOptions<XpozSettings> xpozOptions,
    ILogger<XpozTwitterStrategy> logger
) : IXpozPlatformStrategy
{
    private const string JsonRpcVersion = "2.0";
    private const string RpcMethodName = "tools/call";
    private const string TwitterToolName = "getTwitterPostsByKeywords";
    private const string ResponseContentType = "application/json";
    private const string McpEndpoint = "https://mcp.xpoz.ai/mcp";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly XpozSettings settings = xpozOptions.Value;

    public SocialPlatform Platform => SocialPlatform.Twitter;

    public async Task<Result<SocialFetchResult>> FetchAndFormatDataAsync(
        string query,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<SocialFetchResult>.Failure("La requête sociale ne peut pas être vide.");
        }

        if (startDate >= endDate)
        {
            return Result<SocialFetchResult>.Failure(
                "La date de début doit être antérieure à la date de fin."
            );
        }

        try
        {
            var response = await FetchPostsWithRetryAsync(
                query,
                startDate,
                endDate,
                cancellationToken
            );
            var posts = response.Posts.Select(MapPost).ToList();

            return Result<SocialFetchResult>.Success(
                new SocialFetchResult
                {
                    Platform = Platform,
                    Query = query,
                    Period = new DateRange
                    {
                        StartDate = DateHelper.ToUtc(startDate, DateTimeKind.Utc),
                        EndDate = DateHelper.ToUtc(endDate, DateTimeKind.Utc),
                    },
                    TotalCount = response.TotalCount > 0 ? response.TotalCount : posts.Count,
                    Posts = posts,
                }
            );
        }
        catch (OperationCanceledException exception)
        {
            logger.LogError(
                exception,
                "La récupération des publications Twitter Xpoz a été annulé."
            );

            return Result<SocialFetchResult>.Failure("La récupération Xpoz a été annulée.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Erreur pendant la récupération des publications Twitter Xpoz."
            );

            return Result<SocialFetchResult>.Failure(exception.Message);
        }
    }

    private async Task<XpozTwitterResponse> FetchPostsWithRetryAsync(
        string query,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken
    )
    {
        // var maxRetries = Math.Max(1, settings.MaxRetries);

        // for (var attempt = 1; attempt <= maxRetries; attempt++)
        // {
        //     try
        //     {
        //         var payload = new
        //         {
        //             jsonrpc = JsonRpcVersion,
        //             method = RpcMethodName,
        //             @params = new
        //             {
        //                 name = TwitterToolName,
        //                 arguments = new
        //                 {
        //                     keywords = query,
        //                     startDate = ToUtc(startDate).ToString("O"),
        //                     endDate = ToUtc(endDate).ToString("O"),
        //                     bucket = settings.DefaultBucket,
        //                 },
        //             },
        //         };

        //         using var request = new HttpRequestMessage(
        //             HttpMethod.Post,
        //             $"{settings.BaseUrl.TrimEnd('/')}/{settings.TwitterSearchPath.TrimStart('/')}"
        //         )
        //         {
        //             Content = new StringContent(
        //                 JsonSerializer.Serialize(payload, JsonOptions),
        //                 Encoding.UTF8,
        //                 ResponseContentType
        //             ),
        //         };
        //         request.Headers.Add(settings.ApiKeyHeaderName, settings.ApiKey);

        //         using var response = await httpClient.SendAsync(request, cancellationToken);

        //         if (response.IsSuccessStatusCode)
        //         {
        //             var responsePayload = await response.Content.ReadAsStringAsync(
        //                 cancellationToken
        //             );

        //             return JsonSerializer.Deserialize<XpozTwitterResponse>(
        //                     responsePayload,
        //                     JsonOptions
        //                 ) ?? new XpozTwitterResponse([], 0, null);
        //         }

        //         if (!IsTransient(response.StatusCode) || attempt == maxRetries)
        //         {
        //             var error = await response.Content.ReadAsStringAsync(cancellationToken);
        //             throw new HttpRequestException(
        //                 $"Xpoz a retourné {(int)response.StatusCode}: {error}",
        //                 null,
        //                 response.StatusCode
        //             );
        //         }

        //         await DelayBeforeRetryAsync(attempt, response.StatusCode, cancellationToken);
        //     }
        //     catch (HttpRequestException) when (attempt < maxRetries)
        //     {
        //         await DelayBeforeRetryAsync(attempt, null, cancellationToken);
        //     }
        // }

        // throw new InvalidOperationException(
        //     "La récupération Xpoz a échoué après plusieurs tentatives."
        // );

        // 1. Formater les paramètres de requête
        // var startIso = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
        // var endIso = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // // 2. Construire l'URL avec les paramètres query (sans '/search/')
        // var requestUri =
        //     $"{settings.BaseUrl}/v1/twitter/getTwitterPostsByKeywords?query={Uri.EscapeDataString(query)}&startDate={startIso}&endDate={endIso}";

        // // 3. Exécuter la requête GET
        // var response = await httpClient.GetAsync(requestUri, cancellationToken);

        // if (!response.IsSuccessStatusCode)
        // {
        //     var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        //     throw new HttpRequestException(
        //         $"Xpoz a retourné {(int)response.StatusCode}: {errorContent}"
        //     );
        // }

        // var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

        // return new XpozTwitterResponse([], 0, null);

        var mcpPayload = new McpRequest
        {
            Params = new McpParams
            {
                Name = "getTwitterPostsByKeywords",
                Arguments = new Dictionary<string, object?>
                {
                    { "query", query },
                    { "startDate", startDate.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                    { "endDate", endDate.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                    {
                        "fields",
                        new[]
                        {
                            "id",
                            "authorUsername",
                            "text",
                            "impressionCount",
                            "retweetCount",
                            "likeCount",
                            "quoteCount",
                            "replyCount",
                            "bookmarkCount",
                            "lang",
                            "createdAtDate",
                        }
                    },
                },
            },
        };

        logger.LogInformation("Envoi de la requête MCP Xpoz pour la requête : {Query}", query);

        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                settings.ApiKeyHeaderName,
                settings.ApiKey
            );
        // 2. Envoi en POST vers l'endpoint MCP
        using var request = new HttpRequestMessage(HttpMethod.Post, McpEndpoint)
        {
            Content = JsonContent.Create(mcpPayload),
        };

        // 3. Ajout indispensable des deux en-têtes Accept exigés par Xpoz MCP
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        // 4. Envoi de la requête
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Xpoz a retourné le statut {StatusCode}: {Error}",
                response.StatusCode,
                errorContent
            );
            throw new HttpRequestException(
                $"Xpoz a retourné un statut d'erreur {response.StatusCode} : {errorContent}"
            );
        }

        // 3. Décodage de la réponse MCP JSON-RPC
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Xpoz a retourné le statut HTTP {StatusCode}: {Error}",
                response.StatusCode,
                rawContent
            );
            throw new HttpRequestException(
                $"Xpoz a retourné un statut d'erreur {response.StatusCode} : {rawContent}"
            );
        }

        // 3. Extraction du JSON pur depuis le flux SSE ou texte brut
        var jsonPayload = ExtractJsonFromResponse(rawContent);

        // 4. Désérialisation manuelle et sécurisée
        McpResponse? mcpResponse;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            mcpResponse = JsonSerializer.Deserialize<McpResponse>(jsonPayload, options);
        }
        catch (JsonException ex)
        {
            logger.LogError(
                "Échec de lecture JSON. Contenu brut reçu de Xpoz : {RawContent}",
                rawContent
            );
            throw new InvalidOperationException(
                $"Réponse JSON invalide reçue de Xpoz. Contenu reçu : '{rawContent}'",
                ex
            );
        }

        // 5. Vérification des erreurs au niveau du protocole MCP
        if (mcpResponse?.Error != null)
        {
            logger.LogError(
                "Erreur MCP Xpoz [{Code}] : {Message}",
                mcpResponse.Error.Code,
                mcpResponse.Error.Message
            );
            throw new InvalidOperationException(
                $"Erreur MCP Xpoz [{mcpResponse.Error.Code}] : {mcpResponse.Error.Message}"
            );
        }

        // 6. Extraction des données textuelles de publications
        var rawJsonText = mcpResponse?.Result?.Content?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(rawJsonText))
        {
            logger.LogWarning(
                "Aucun contenu de publication trouvé dans la réponse MCP pour : {Query}",
                query
            );
            return XpozTwitterResponse.Empty;
        }

        var xpozResponse = ParseXpozResponse(rawJsonText);

        if (xpozResponse is null || xpozResponse.Posts is null || xpozResponse.Posts.Count == 0)
        {
            logger.LogInformation("La réponse Xpoz est vide ou ne contient aucun tweet.");
            return XpozTwitterResponse.Empty;
        }

        logger.LogInformation(
            "{Count} tweets désérialisés avec succès depuis Xpoz.",
            xpozResponse.Posts.Count
        );

        return xpozResponse;
    }

    /// <summary>
    /// Désérialise la chaîne JSON brute en objet XpozTwitterResponse.
    /// </summary>
    private XpozTwitterResponse? ParseXpozResponse(string rawJsonText)
    {
        if (string.IsNullOrWhiteSpace(rawJsonText))
            return null;

        var trimmed = rawJsonText.Trim();

        // 1. Si le contenu est du JSON classique (commence par '{')
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                return JsonSerializer.Deserialize<XpozTwitterResponse>(trimmed, options);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Erreur de désérialisation JSON dans ParseXpozResponse.");
                return null;
            }
        }

        // 2. Sinon, on analyse le format texte/CSV personnalisé d'Xpoz
        return ParseCustomTextResponse(trimmed);
    }

    /// <summary>
    /// Analyse le format texte ligne par ligne envoyé par Xpoz (ex: "id","text","authorUsername",...).
    /// </summary>
    private XpozTwitterResponse ParseCustomTextResponse(string rawText)
    {
        var tweets = new List<XpozTweet>();
        var lines = rawText.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Ignorer les lignes d'en-tête ou de structure
            if (
                trimmedLine.StartsWith("status:")
                || trimmedLine.StartsWith("data:")
                || trimmedLine.StartsWith("results")
            )
            {
                continue;
            }

            // Découpage de la ligne CSV en respectant les guillemets
            var columns = ParseCsvLine(trimmedLine);

            // Ordre des champs attendus (10 colonnes) :
            // 0: id
            // 1: authorName
            // 2: text
            // 3: impressionCount
            // 5: retweetCount
            // 6: likeCount
            // 7: quoteCount
            // 8: replyCount
            // 9: bookmarkCount
            // 10: language
            // 11: createdAtDate
            if (columns.Count >= 11)
            {
                var id = columns[0];
                var authorName = columns[1];
                var text = WebUtility.HtmlDecode(columns[2]); // Décodage des entités HTML (ex: &amp;)

                _ = long.TryParse(columns[3], out var impressionCount);

                _ = int.TryParse(columns[4], out var retweetCount);
                _ = int.TryParse(columns[5], out var likeCount);
                _ = int.TryParse(columns[6], out var quoteCount);
                _ = int.TryParse(columns[7], out var replyCount);
                _ = int.TryParse(columns[8], out var bookmarkCount);
                var lang = columns[9];

                DateTime.TryParse(columns[10], out var createdAt);

                // Construction de l'auteur avec le nom fourni
                var author = new XpozAuthor(
                    Id: authorName,
                    Username: authorName,
                    DisplayName: authorName,
                    FollowersCount: -1,
                    ProfileImageUrl: null
                );

                // Construction des métriques réelles extraites de la réponse
                var metrics = new XpozMetrics(
                    Likes: likeCount,
                    Retweets: retweetCount,
                    Replies: replyCount,
                    Quotes: quoteCount,
                    Bookmarks: bookmarkCount, // Utilisation des citations (quotes) ou 0
                    Impressions: impressionCount
                );

                // Instanciation de XpozTweet
                var tweet = new XpozTweet(
                    Id: id,
                    Text: text,
                    Author: author,
                    CreatedAt: createdAt,
                    Url: $"https://twitter.com/{authorName}/status/{id}",
                    Metrics: metrics,
                    Location: string.Empty,
                    Language: lang
                );

                tweets.Add(tweet);
            }
        }

        logger.LogInformation(
            "{Count} tweets extraits et typés depuis le format Xpoz.",
            tweets.Count
        );

        return new XpozTwitterResponse(tweets, tweets.Count, null);
    }

    /// <summary>
    /// Découpe une ligne de texte séparée par des virgules tout en ignorant les virgules situées entre guillemets.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes; // Alterne l'état à chaque guillemet rencontré
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString().Trim());
        return result;
    }

    /// <summary>
    /// Transforme une réponse XpozTwitterResponse en une liste d'entités de domaine SocialPost.
    /// </summary>
    private static List<SocialPost> MapToSocialPosts(
        XpozTwitterResponse xpozResponse,
        string rawJsonPayload
    )
    {
        var socialPosts = new List<SocialPost>();

        foreach (var tweet in xpozResponse.Posts)
        {
            // Génération d'un GUID unique à partir de l'ID String du Tweet
            var postId = Guid.NewGuid();

            var post = new SocialPost
            {
                Id = postId,
                Platform = SocialPlatform.Twitter,
                AuthorId = tweet.Author?.Id ?? string.Empty,
                AuthorName = tweet.Author?.DisplayName ?? "Inconnu",
                AuthorHandle = tweet.Author?.Username ?? "anonyme",
                AuthorFollowersCount = tweet.Author?.FollowersCount ?? 0,
                AuthorProfileImageUrl = tweet.Author?.ProfileImageUrl,
                ContentText = tweet.Text ?? string.Empty,
                CreatedAt = tweet.CreatedAt,
                Url = tweet.Url ?? string.Empty,
                LikesCount = tweet.Metrics?.Likes ?? 0,
                SharesCount = tweet.Metrics?.Retweets ?? 0,
                CommentsCount = tweet.Metrics?.Replies ?? 0,
                ImpressionsCount = tweet.Metrics?.Impressions ?? 0L,
                RawJsonPayload = rawJsonPayload,
            };

            socialPosts.Add(post);
        }

        return socialPosts;
    }

    /// <summary>
    /// Nettoie la réponse brute pour extraire la structure JSON (gère le format classique et le format SSE).
    /// </summary>
    private static string ExtractJsonFromResponse(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
            return string.Empty;

        var trimmed = rawContent.Trim();

        // Cas 1 : La réponse est déjà un objet JSON valide ({ ... })
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        {
            return trimmed;
        }

        // Cas 2 : La réponse est un flux SSE ("data: { ... }")
        var lines = rawContent.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var jsonPart = line.Substring(5).Trim();
                if (jsonPart.StartsWith("{") || jsonPart.StartsWith("["))
                {
                    return jsonPart;
                }
            }
        }

        return trimmed;
    }

    private async Task DelayBeforeRetryAsync(
        int attempt,
        HttpStatusCode? statusCode,
        CancellationToken cancellationToken
    )
    {
        var delayMs = settings.InitialRetryDelayMs * Math.Pow(2, attempt - 1);
        logger.LogWarning(
            "Nouvelle tentative Xpoz Twitter {Attempt} après HTTP {StatusCode}, délai {DelayMs} ms.",
            attempt,
            statusCode,
            delayMs
        );
        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode
            is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or >= HttpStatusCode.InternalServerError;

    private static SocialPost MapPost(XpozTweet tweet) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Platform = SocialPlatform.Twitter,
            AuthorId = tweet.Author.Id,
            AuthorName = tweet.Author.DisplayName,
            AuthorHandle = tweet.Author.Username,
            AuthorFollowersCount = tweet.Author.FollowersCount,
            AuthorProfileImageUrl = tweet.Author.ProfileImageUrl,
            ContentText = tweet.Text,
            CreatedAt = DateHelper.ToUtc(tweet.CreatedAt, DateTimeKind.Utc),
            Url = tweet.Url ?? $"https://twitter.com/{tweet.Author.Username}/status/{tweet.Id}",
            LikesCount = tweet.Metrics.Likes,
            SharesCount = tweet.Metrics.Retweets,
            CommentsCount = tweet.Metrics.Replies,
            ImpressionsCount = tweet.Metrics.Impressions,
            RawJsonPayload = JsonSerializer.Serialize(tweet, JsonOptions),
            PlatformSpecificData = new Dictionary<string, object>
            {
                ["Bookmarks"] = tweet.Metrics.Bookmarks,
                ["Location"] = tweet.Location ?? string.Empty,
                ["Language"] = tweet.Language ?? string.Empty,
            },
        };
}
