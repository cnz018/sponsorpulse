using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;

namespace SponsorPulse.Application.Common.Interfaces;

/// <summary>
/// Interface pour le service de collecte et d'analyse de données Twitter (X).
/// Utilise Xpoz.ai pour la récupération des données brutes.
/// </summary>
public interface ITwitterService
{
    /// <summary>
    /// Récupère les métriques de performance Twitter pour une requête spécifique sur une période donnée.
    /// </summary>
    /// <param name="query">Hashtag ou mot-clé à rechercher (ex: #MonEvenement ou "ma marque")</param>
    /// <param name="startDate">Date de début de la période d'analyse</param>
    /// <param name="endDate">Date de fin de la période d'analyse</param>
    /// <returns>Résultat contenant les TwitterAnalytics ou une erreur</returns>
    Task<Result<TwitterAnalytics>> GetTwitterMetricsAsync(
        string query,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    );
}
