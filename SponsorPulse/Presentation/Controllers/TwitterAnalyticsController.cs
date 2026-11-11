using Microsoft.AspNetCore.Mvc;
using SponsorPulse.Application.Common.Interfaces;

namespace SponsorPulse.Presentation.Controllers;

/// <summary>
/// Contrôleur exemple pour l'utilisation du TwitterService.
/// Démontre comment intégrer l'analyse Twitter dans votre application.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TwitterAnalyticsController(
    ITwitterService twitterService,
    ILogger<TwitterAnalyticsController> logger
) : ControllerBase
{
    /// <summary>
    /// Récupère les métriques Twitter pour un hashtag ou une requête spécifique.
    /// </summary>
    /// <param name="query">Hashtag ou requête (ex: #MonEvenement)</param>
    /// <param name="days">Nombre de jours à analyser vers l'arrière (par défaut 7)</param>
    /// <returns>TwitterAnalytics avec l'analyse complète</returns>
    [HttpGet("{query}")]
    public async Task<IActionResult> GetMetrics(
        [FromRoute] string query,
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("La requête ne peut pas être vide");
            }

            if (days < 1 || days > 365)
            {
                return BadRequest("Le nombre de jours doit être entre 1 et 365");
            }

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-days);

            logger.LogInformation(
                "Récupération des métriques Twitter pour: {Query} ({Days} jours)",
                query,
                days
            );

            var result = await twitterService.GetTwitterMetricsAsync(
                query,
                startDate,
                endDate,
                cancellationToken
            );

            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, "La requête a expiré");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la récupération des métriques Twitter");
            return StatusCode(500, "Une erreur serveur s'est produite");
        }
    }

    /// <summary>
    /// Récupère les métriques Twitter avec une plage de dates personnalisée.
    /// </summary>
    [HttpPost("range")]
    public async Task<IActionResult> GetMetricsInRange(
        [FromBody] TwitterAnalyticsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest("Requête invalide");
            }

            logger.LogInformation(
                "Récupération des métriques Twitter pour: {Query} du {Start} au {End}",
                request.Query,
                request.StartDate,
                request.EndDate
            );

            var result = await twitterService.GetTwitterMetricsAsync(
                request.Query,
                request.StartDate,
                request.EndDate,
                cancellationToken
            );

            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, "La requête a expiré");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la récupération des métriques Twitter");
            return StatusCode(500, "Une erreur serveur s'est produite");
        }
    }

    /// <summary>
    /// Récupère les métriques Twitter pour plusieurs requêtes en parallèle.
    /// Utile pour comparer plusieurs hashtags d'un même événement.
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> GetMetricsBatch(
        [FromBody] BatchTwitterAnalyticsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (request?.Queries == null || request.Queries.Count == 0)
            {
                return BadRequest("Au moins une requête est nécessaire");
            }

            logger.LogInformation(
                "Analyse parallèle de {Count} requêtes Twitter",
                request.Queries.Count
            );

            var tasks = request
                .Queries.Select(query =>
                    twitterService.GetTwitterMetricsAsync(
                        query,
                        request.StartDate,
                        request.EndDate,
                        cancellationToken
                    )
                )
                .ToList();

            var results = await Task.WhenAll(tasks);

            var failedResults = results.Where(r => !r.IsSuccess).ToList();
            if (failedResults.Count > 0)
            {
                return BadRequest(
                    new { errors = failedResults.Select(r => r.ErrorMessage).ToList() }
                );
            }

            var analytics = results.Select(r => r.Value).ToList();
            return Ok(new { count = analytics.Count, analytics });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, "La requête a expiré");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'analyse en parallèle");
            return StatusCode(500, "Une erreur serveur s'est produite");
        }
    }

    /// <summary>
    /// Compare deux requêtes pour voir la performance relative.
    /// Par exemple: #MonEvenement vs #CompetiteurEvent
    /// </summary>
    [HttpPost("compare")]
    public async Task<IActionResult> CompareQueries(
        [FromBody] CompareQueriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (
                string.IsNullOrWhiteSpace(request?.Query1)
                || string.IsNullOrWhiteSpace(request.Query2)
            )
            {
                return BadRequest("Deux requêtes valides sont requises");
            }

            var result1 = await twitterService.GetTwitterMetricsAsync(
                request.Query1,
                request.StartDate,
                request.EndDate,
                cancellationToken
            );

            var result2 = await twitterService.GetTwitterMetricsAsync(
                request.Query2,
                request.StartDate,
                request.EndDate,
                cancellationToken
            );

            if (!result1.IsSuccess || !result2.IsSuccess)
            {
                return BadRequest(
                    new { error1 = result1.ErrorMessage, error2 = result2.ErrorMessage }
                );
            }

            var analytics1 = result1.Value;
            var analytics2 = result2.Value;

            return Ok(
                new
                {
                    query1 = request.Query1,
                    metrics1 = analytics1,
                    query2 = request.Query2,
                    metrics2 = analytics2,
                    comparison = new
                    {
                        tweetsRatio = analytics1?.TotalTweets > 0
                            ? (double)(analytics2?.TotalTweets ?? 0)
                                / (double)analytics1.TotalTweets
                            : 0,
                        engagementRatio = analytics1?.TotalEngagement > 0
                            ? (double)(analytics2?.TotalEngagement ?? 0)
                                / (double)analytics1.TotalEngagement
                            : 0,
                        aveRatio = analytics1?.AdValueEquivalent > 0
                            ? (double)(analytics2?.AdValueEquivalent ?? 0m)
                                / (double)analytics1.AdValueEquivalent
                            : 0,
                        winner = DetermineWinner(analytics1!, analytics2!),
                    },
                }
            );
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, "La requête a expiré");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la comparaison des requêtes");
            return StatusCode(500, "Une erreur serveur s'est produite");
        }
    }

    private static string DetermineWinner(
        Domain.Models.TwitterAnalytics analytics1,
        Domain.Models.TwitterAnalytics analytics2
    )
    {
        var score1 = analytics1.TotalEngagement * 1.0 + analytics1.EstimatedImpressions * 0.001;
        var score2 = analytics2.TotalEngagement * 1.0 + analytics2.EstimatedImpressions * 0.001;

        if (score1 > score2)
            return "Query1 Winner";
        else if (score2 > score1)
            return "Query2 Winner";
        else
            return "Tie";
    }
}

// DTOs pour les requêtes

/// <summary>
/// Requête pour l'analyse avec plage de dates personnalisée
/// </summary>
public class TwitterAnalyticsRequest
{
    /// <summary>
    /// Hashtag ou requête à analyser (ex: #MonEvent ou "ma marque")
    /// </summary>
    public required string Query { get; set; }

    /// <summary>
    /// Date de début de l'analyse
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Date de fin de l'analyse
    /// </summary>
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Requête pour l'analyse en parallèle de plusieurs hashtags
/// </summary>
public class BatchTwitterAnalyticsRequest
{
    /// <summary>
    /// Liste des requêtes/hashtags à analyser
    /// </summary>
    public required List<string> Queries { get; set; }

    /// <summary>
    /// Date de début pour toutes les analyses
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Date de fin pour toutes les analyses
    /// </summary>
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Requête pour comparer deux hashtags/requêtes
/// </summary>
public class CompareQueriesRequest
{
    /// <summary>
    /// Première requête à comparer
    /// </summary>
    public required string Query1 { get; set; }

    /// <summary>
    /// Deuxième requête à comparer
    /// </summary>
    public required string Query2 { get; set; }

    /// <summary>
    /// Date de début de comparaison
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Date de fin de comparaison
    /// </summary>
    public DateTime EndDate { get; set; }
}
