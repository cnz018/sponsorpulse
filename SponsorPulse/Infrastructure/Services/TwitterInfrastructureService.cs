using System.Text.Json;
using Microsoft.Extensions.Options;
using SponsorPulse.Application.Common.Configuration;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;
using SponsorPulse.Infrastructure.Xpoz.Models;

namespace SponsorPulse.Infrastructure.Services;

/// <summary>
/// Service d'analyse Twitter utilisant Xpoz.ai pour la collecte de données.
/// Implémente la robustesse avec retry exponentiel et traitement parallèle des données.
/// </summary>
public class TwitterInfrastructureService(
    HttpClient httpClient,
    IOptions<XpozSettings> xpozOptions,
    ILogger<TwitterInfrastructureService> logger
) : ITwitterService
{
    private readonly XpozSettings _xpozSettings = xpozOptions.Value;

    /// <summary>
    /// Dictionnaire de mots-clés pour l'analyse de sentiment simplifiée
    /// </summary>
    private static readonly Dictionary<string, List<string>> SentimentKeywords = new()
    {
        {
            "Positif",
            new List<string>
            {
                "amazing",
                "awesome",
                "excellent",
                "great",
                "love",
                "fantastic",
                "wonderful",
                "best",
                "perfect",
                "incredible",
                "super",
                "génial",
                "excellent",
                "formidable",
                "merveilleux",
                "meilleur",
            }
        },
        {
            "Négatif",
            new List<string>
            {
                "bad",
                "terrible",
                "awful",
                "horrible",
                "hate",
                "poor",
                "worst",
                "disappointing",
                "useless",
                "mauvais",
                "horrible",
                "nul",
                "décevant",
                "pire",
            }
        },
    };

    public async Task<Result<TwitterAnalytics>> GetTwitterMetricsAsync(
        string query,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Result<TwitterAnalytics>.Failure("La requête (query) ne peut pas être vide");
            }

            if (startDate >= endDate)
            {
                return Result<TwitterAnalytics>.Failure(
                    "La date de début doit être antérieure à la date de fin"
                );
            }

            logger.LogInformation(
                "Récupération des métriques Twitter pour la requête '{Query}' du {StartDate:O} au {EndDate:O}",
                query,
                startDate,
                endDate
            );

            // Récupération des tweets via Xpoz.ai avec retry exponentiel
            var (success, tweets, error) = await FetchTwitterPostsWithRetryAsync(
                query,
                startDate,
                endDate,
                cancellationToken
            );

            if (!success)
            {
                logger.LogError("Erreur lors de la récupération des tweets: {Error}", error);
                return Result<TwitterAnalytics>.Failure(error!);
            }

            if (tweets.Count == 0)
            {
                logger.LogWarning("Aucun tweet trouvé pour la requête '{Query}'", query);
                return Result<TwitterAnalytics>.Success(
                    new TwitterAnalytics
                    {
                        TotalTweets = 0,
                        TotalEngagement = 0,
                        TopTweets = new List<TopTweet>(),
                        Period = new DateRange { StartDate = startDate, EndDate = endDate },
                        EstimatedImpressions = 0,
                        AdValueEquivalent = 0m,
                        SentimentRatio = new Dictionary<string, double>
                        {
                            { "Positif", 0 },
                            { "Neutre", 100 },
                            { "Négatif", 0 },
                        },
                        TopInfluencer = new TopInfluencer(),
                        ViralMultiplier = 0,
                    }
                );
            }

            // Traitement parallèle des données brutes
            var analytics = await ProcessTwitterDataAsync(tweets, startDate, endDate);

            logger.LogInformation(
                "Analyse Twitter complétée: {TotalTweets} tweets, {TotalEngagement} engagements",
                analytics.TotalTweets,
                analytics.TotalEngagement
            );

            return Result<TwitterAnalytics>.Success(analytics);
        }
        catch (OperationCanceledException)
        {
            return Result<TwitterAnalytics>.Failure("La requête a été annulée");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur non gérée lors de la récupération des métriques Twitter");
            return Result<TwitterAnalytics>.Failure($"Erreur: {ex.Message}");
        }
    }

    /// <summary>
    /// Récupère les tweets avec retry exponentiel automatique
    /// </summary>
    private async Task<(
        bool Success,
        List<XpozTweet> Tweets,
        string? Error
    )> FetchTwitterPostsWithRetryAsync(
        string query,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken
    )
    {
        int retryCount = 0;

        while (retryCount < _xpozSettings.MaxRetries)
        {
            try
            {
                var requestUrl =
                    $"{_xpozSettings.BaseUrl}/twitter/search/getTwitterPostsByKeywords";

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = JsonContent.Create(
                        new
                        {
                            keywords = query,
                            startDate = startDate.ToString("O"),
                            endDate = endDate.ToString("O"),
                            bucket = _xpozSettings.DefaultBucket,
                        }
                    ),
                };

                request.Headers.Add("x-api-key", _xpozSettings.ApiKey);

                var response = await httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var xpozResponse = JsonSerializer.Deserialize<XpozTwitterResponse>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    return (true, xpozResponse?.Posts ?? new List<XpozTweet>(), null);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (
                        false,
                        new List<XpozTweet>(),
                        "Authentification échouée: vérifiez votre clé API Xpoz"
                    );
                }

                // Retry pour les erreurs temporaires
                retryCount++;
                if (retryCount < _xpozSettings.MaxRetries)
                {
                    var delayMs = CalculateExponentialBackoffDelay(retryCount);
                    logger.LogWarning(
                        "Tentative {RetryNumber} échouée (HTTP {StatusCode}). Nouvelle tentative dans {DelayMs}ms",
                        retryCount,
                        response.StatusCode,
                        delayMs
                    );
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                retryCount++;
                if (retryCount < _xpozSettings.MaxRetries)
                {
                    var delayMs = CalculateExponentialBackoffDelay(retryCount);
                    logger.LogWarning(
                        ex,
                        "Erreur réseau à la tentative {RetryNumber}. Nouvelle tentative dans {DelayMs}ms",
                        retryCount,
                        delayMs
                    );
                    await Task.Delay(delayMs, cancellationToken);
                }
                else
                {
                    return (
                        false,
                        new List<XpozTweet>(),
                        $"Erreur réseau après {_xpozSettings.MaxRetries} tentatives: {ex.Message}"
                    );
                }
            }
        }

        return (
            false,
            new List<XpozTweet>(),
            $"Impossible de récupérer les tweets après {_xpozSettings.MaxRetries} tentatives"
        );
    }

    /// <summary>
    /// Traite les données brutes Xpoz de manière parallèle
    /// </summary>
    private async Task<TwitterAnalytics> ProcessTwitterDataAsync(
        List<XpozTweet> tweets,
        DateTime startDate,
        DateTime endDate
    )
    {
        return await Task.Run(() =>
        {
            // Calcul du volume total et engagement
            var totalTweets = tweets.Count;
            var totalEngagement = tweets
                .AsParallel()
                .Sum(t => t.Metrics.Likes + t.Metrics.Retweets + t.Metrics.Replies);

            // Top 3 tweets avec le plus d'engagement
            var topTweets = tweets
                .OrderByDescending(t => t.Metrics.Likes + t.Metrics.Retweets + t.Metrics.Replies)
                .Take(3)
                .Select(t => new TopTweet
                {
                    AuthorName = t.Author.DisplayName,
                    AuthorHandle = t.Author.Username,
                    AuthorFollowersCount = t.Author.FollowersCount,
                    Text = t.Text,
                    Url = t.Url ?? $"https://twitter.com/{t.Author.Username}/status/{t.Id}",
                    Likes = t.Metrics.Likes,
                    Retweets = t.Metrics.Retweets,
                    Replies = t.Metrics.Replies,
                    CreatedAt = t.CreatedAt,
                })
                .ToList();

            // Calcul du reach (impressions estimées)
            // Déduplique les auteurs pour ne pas compter deux fois leur reach
            var uniqueAuthors = tweets
                .GroupBy(t => t.Author.Id)
                .Select(g => g.First().Author)
                .ToList();

            var estimatedImpressions = uniqueAuthors.AsParallel().Sum(a => a.FollowersCount);

            // Top influencer (auteur avec le plus de followers ayant tweeté)
            var topInfluencer = uniqueAuthors
                .OrderByDescending(a => a.FollowersCount)
                .FirstOrDefault();

            // Analyse de sentiment simplifiée
            var sentimentRatio = CalculateSentimentRatio(tweets);

            // Calcul du viral multiplier
            var totalRetweets = tweets.AsParallel().Sum(t => t.Metrics.Retweets);
            var viralMultiplier = totalTweets > 0 ? (double)totalRetweets / totalTweets : 0;

            // Calcul de l'AVE (Advertising Value Equivalent)
            var adValueEquivalent = CalculateAdValueEquivalent(estimatedImpressions);

            return new TwitterAnalytics
            {
                TotalTweets = totalTweets,
                TotalEngagement = totalEngagement,
                TopTweets = topTweets,
                Period = new DateRange { StartDate = startDate, EndDate = endDate },
                EstimatedImpressions = estimatedImpressions,
                AdValueEquivalent = adValueEquivalent,
                SentimentRatio = sentimentRatio,
                TopInfluencer =
                    topInfluencer != null
                        ? new TopInfluencer
                        {
                            Name = topInfluencer.DisplayName,
                            Handle = topInfluencer.Username,
                            FollowersCount = topInfluencer.FollowersCount,
                            TweetCount = tweets.Count(t => t.Author.Id == topInfluencer.Id),
                        }
                        : new TopInfluencer(),
                ViralMultiplier = viralMultiplier,
            };
        });
    }

    /// <summary>
    /// Analyse le sentiment des tweets basée sur des mots-clés
    /// </summary>
    private static Dictionary<string, double> CalculateSentimentRatio(List<XpozTweet> tweets)
    {
        if (tweets.Count == 0)
        {
            return new Dictionary<string, double>
            {
                { "Positif", 0 },
                { "Neutre", 100 },
                { "Négatif", 0 },
            };
        }

        var sentiments = tweets.AsParallel().Select(t => DetectSentiment(t.Text)).ToList();

        var positiveCount = sentiments.Count(s => s == "Positif");
        var negativeCount = sentiments.Count(s => s == "Négatif");
        var neutralCount = sentiments.Count(s => s == "Neutre");
        var total = sentiments.Count;

        return new Dictionary<string, double>
        {
            { "Positif", Math.Round((positiveCount * 100.0) / total, 2) },
            { "Neutre", Math.Round((neutralCount * 100.0) / total, 2) },
            { "Négatif", Math.Round((negativeCount * 100.0) / total, 2) },
        };
    }

    /// <summary>
    /// Détecte le sentiment d'un texte de tweet
    /// </summary>
    private static string DetectSentiment(string text)
    {
        var lowerText = text.ToLower();

        var positiveCount = SentimentKeywords["Positif"]
            .Count(keyword => lowerText.Contains(keyword));
        var negativeCount = SentimentKeywords["Négatif"]
            .Count(keyword => lowerText.Contains(keyword));

        if (positiveCount > negativeCount && positiveCount > 0)
        {
            return "Positif";
        }

        if (negativeCount > positiveCount && negativeCount > 0)
        {
            return "Négatif";
        }

        return "Neutre";
    }

    /// <summary>
    /// Calcule l'AVE (Advertising Value Equivalent) en euros
    /// Formule: (EstimatedImpressions * CPM) / 1000
    /// </summary>
    private decimal CalculateAdValueEquivalent(long estimatedImpressions)
    {
        if (estimatedImpressions == 0)
        {
            return 0m;
        }

        return (estimatedImpressions * _xpozSettings.CpmEur) / 1000m;
    }

    /// <summary>
    /// Calcule le délai d'attente avec backoff exponentiel
    /// Formule: InitialDelay * 2^(RetryCount-1)
    /// </summary>
    private int CalculateExponentialBackoffDelay(int retryCount)
    {
        return _xpozSettings.InitialRetryDelayMs * (int)Math.Pow(2, retryCount - 1);
    }
}
